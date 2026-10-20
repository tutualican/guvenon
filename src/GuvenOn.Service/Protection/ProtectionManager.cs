using GuvenOn.Core.Protection;
using Microsoft.Extensions.Logging;

namespace GuvenOn.Service.Protection;

/// <summary>
/// Owns the lifetime of the WFP engine. Commands are serialized because WFP
/// transactions are small and infrequent; a lock keeps the state machine easy to
/// reason about and prevents a pause timer from racing a user command.
/// </summary>
public sealed class ProtectionManager : IDisposable
{
    private readonly ILogger<ProtectionManager> _logger;
    private readonly object _gate = new();

    private WfpProtectionEngine? _engine;
    private NetworkDnsConfigurator? _dnsConfigurator;
    private ProtectionStatus _status = ProtectionStatus.Off;
    private Timer? _resumeTimer;
    private bool _disposed;

    public ProtectionManager(ILogger<ProtectionManager> logger)
    {
        _logger = logger;
        NetworkDnsConfigurator.RestorePersisted(logger, out _);
    }

    public ProtectionStatus Status
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public ProtectionStatus Enable()
    {
        lock (_gate)
        {
            return EnableLocked();
        }
    }

    public ProtectionStatus Disable()
    {
        lock (_gate)
        {
            var cleanupError = StopEngine();
            _status = cleanupError is null
                ? ProtectionStatus.Off
                : ProtectionStatus.Off with { LastError = cleanupError };
            return _status;
        }
    }

    public ProtectionStatus Pause(int minutes)
    {
        lock (_gate)
        {
            var duration = TimeSpan.FromMinutes(Math.Clamp(minutes, 1, 120));
            var cleanupError = StopEngine();

            var until = DateTimeOffset.UtcNow.Add(duration);
            _status = new ProtectionStatus(
                Enabled: true,
                EngineActive: false,
                DnsGuard: false,
                LanGuard: false,
                ActiveSince: null,
                PausedUntil: until,
                FilterCount: 0,
                LastError: cleanupError);

            _resumeTimer?.Dispose();
            _resumeTimer = new Timer(
                _ => ResumeFromPause(until),
                state: null,
                dueTime: duration,
                period: Timeout.InfiniteTimeSpan);

            _logger.LogInformation("Protection paused until {Until}.", until);
            return _status;
        }
    }

    private void ResumeFromPause(DateTimeOffset expectedUntil)
    {
        lock (_gate)
        {
            if (_disposed || _status.PausedUntil != expectedUntil)
            {
                return;
            }

            _logger.LogInformation("Pause elapsed; re-applying protection.");
            _ = EnableLocked();
        }
    }

    private ProtectionStatus EnableLocked()
    {
        if (_disposed)
        {
            return _status;
        }

        if (_engine is not null)
        {
            return _status;
        }

        _resumeTimer?.Dispose();
        _resumeTimer = null;

        try
        {
            // Assign before applying so the outer catch can clean up both
            // resources even if the second step throws. StopEngine tolerates
            // partially initialized objects.
            _engine = new WfpProtectionEngine(ProtectionPolicy.Default);
            _dnsConfigurator = new NetworkDnsConfigurator(_logger);

            _engine.Apply();
            var changedAdapters = _dnsConfigurator.Apply(ProtectionPolicy.Default);

            _status = new ProtectionStatus(
                Enabled: true,
                EngineActive: true,
                DnsGuard: true,
                LanGuard: ProtectionPolicy.Default.BlockLan,
                ActiveSince: DateTimeOffset.UtcNow,
                PausedUntil: null,
                FilterCount: _engine.FilterCount,
                LastError: null);

            _logger.LogInformation(
                "Protection enabled with {FilterCount} WFP filters and trusted DNS on {AdapterCount} adapters.",
                _engine.FilterCount,
                changedAdapters);
        }
        catch (Exception exception)
        {
            var cleanupError = StopEngine();
            var message = cleanupError is null
                ? exception.Message
                : $"{exception.Message} Cleanup: {cleanupError}";

            _status = new ProtectionStatus(
                Enabled: false,
                EngineActive: false,
                DnsGuard: false,
                LanGuard: false,
                ActiveSince: null,
                PausedUntil: null,
                FilterCount: 0,
                LastError: message);

            _logger.LogError(exception, "Protection could not be enabled.");
        }

        return _status;
    }

    private string? StopEngine()
    {
        _resumeTimer?.Dispose();
        _resumeTimer = null;

        string? cleanupError = null;

        if (_dnsConfigurator is not null)
        {
            try
            {
                _dnsConfigurator.Dispose();
            }
            catch (Exception exception)
            {
                cleanupError = exception.Message;
                _logger.LogWarning(exception, "DNS restoration failed.");
            }
            finally
            {
                _dnsConfigurator = null;
            }
        }

        if (_engine is not null)
        {
            try
            {
                _engine.Dispose();
            }
            catch (Exception exception)
            {
                cleanupError = cleanupError is null
                    ? exception.Message
                    : $"{cleanupError} {exception.Message}";
                _logger.LogWarning(exception, "WFP engine cleanup failed.");
            }
            finally
            {
                _engine = null;
            }
        }

        return cleanupError;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopEngine();
            _status = ProtectionStatus.Off;
        }
    }
}
