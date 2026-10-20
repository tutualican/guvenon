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
    private readonly ProtectionStateStore _stateStore = new();
    private readonly object _gate = new();

    private WfpProtectionEngine? _engine;
    private NetworkDnsConfigurator? _dnsConfigurator;
    private ProtectionStatus _status = ProtectionStatus.Off;
    private Timer? _resumeTimer;
    private bool _started;
    private bool _disposed;

    public ProtectionManager(ILogger<ProtectionManager> logger)
    {
        _logger = logger;
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

    /// <summary>
    /// Restores the previous intent before the control pipe starts accepting
    /// commands. An enabled state re-applies the policy; a disabled or paused
    /// state verifies that a crash did not leave persistent filters behind.
    /// </summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            var state = _stateStore.Load();

            if (!state.Enabled)
            {
                var cleanupError = Combine(
                    CleanupPersistentFilters(),
                    RestoreDnsSnapshot());
                _status = ProtectionStatus.Off with { LastError = cleanupError };
                return;
            }

            if (state.PausedUntil is { } pausedUntil && pausedUntil > DateTimeOffset.UtcNow)
            {
                // A crash during pause can leave persistent WFP filters or a DNS
                // marker behind. Clear both so a captive portal is not blocked.
                var cleanupError = Combine(
                    CleanupPersistentFilters(),
                    RestoreDnsSnapshot());

                _status = new ProtectionStatus(
                    Enabled: true,
                    EngineActive: false,
                    DnsGuard: false,
                    LanGuard: false,
                    ActiveSince: null,
                    PausedUntil: pausedUntil,
                    FilterCount: 0,
                    LastError: cleanupError);

                ScheduleResume(pausedUntil);
                _logger.LogInformation("Protection is paused until {Until}.", pausedUntil);
                return;
            }

            _ = EnableLocked(state.AppliedAt);
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
            // Persist the user's intent before removing filters. If the service
            // crashes during teardown, the next start sees "disabled" and
            // finishes the cleanup instead of unexpectedly re-enabling.
            var stateError = SaveState(new WindowsProtectionState(false, null, null));
            var cleanupError = StopEngine();
            _status = ProtectionStatus.Off with
            {
                LastError = Combine(cleanupError, stateError)
            };
            return _status;
        }
    }

    public ProtectionStatus Pause(int minutes)
    {
        lock (_gate)
        {
            var duration = TimeSpan.FromMinutes(Math.Clamp(minutes, 1, 120));
            var until = DateTimeOffset.UtcNow.Add(duration);
            var stateError = SaveState(new WindowsProtectionState(true, until, null));

            // Do not tear down the guard if the pause deadline could not be
            // persisted; otherwise a crash could turn a temporary pause into a
            // permanent one, or an unintended re-enable after restart.
            if (stateError is not null)
            {
                _status = _status with { LastError = stateError };
                return _status;
            }

            var cleanupError = StopEngine();
            _status = new ProtectionStatus(
                Enabled: true,
                EngineActive: false,
                DnsGuard: false,
                LanGuard: false,
                ActiveSince: null,
                PausedUntil: until,
                FilterCount: 0,
                LastError: Combine(cleanupError, stateError));

            ScheduleResume(until);
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

    private void ScheduleResume(DateTimeOffset until)
    {
        _resumeTimer?.Dispose();
        _resumeTimer = new Timer(
            _ => ResumeFromPause(until),
            state: null,
            dueTime: until - DateTimeOffset.UtcNow,
            period: Timeout.InfiniteTimeSpan);
    }

    private ProtectionStatus EnableLocked(DateTimeOffset? activeSince = null)
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

        var now = DateTimeOffset.UtcNow;
        var since = activeSince ?? now;

        // Persist "enabled" before touching WFP or DNS. If the service crashes
        // half-way through Apply, startup will see the user's intent and
        // re-apply instead of cleaning up a possibly partial guard.
        var stateError = SaveState(new WindowsProtectionState(true, null, since));
        if (stateError is not null)
        {
            _status = ProtectionStatus.Off with { LastError = stateError };
            return _status;
        }

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
                ActiveSince: since,
                PausedUntil: null,
                FilterCount: _engine.FilterCount,
                LastError: null);

            _logger.LogInformation(
                "Protection enabled with {FilterCount} persistent WFP filters and trusted DNS on {AdapterCount} adapters.",
                _engine.FilterCount,
                changedAdapters);
        }
        catch (Exception exception)
        {
            var cleanupError = StopEngine();
            var disableStateError = SaveState(new WindowsProtectionState(false, null, null));
            var message = Combine(
                exception.Message,
                cleanupError is null ? null : $"Cleanup: {cleanupError}",
                disableStateError);

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
            var hadFilters = _engine.FilterCount > 0;
            try
            {
                _engine.Dispose();
            }
            catch (Exception exception)
            {
                cleanupError = Combine(cleanupError, exception.Message);
                _logger.LogWarning(exception, "WFP engine cleanup failed; retrying stale filter removal.");
                cleanupError = Combine(cleanupError, CleanupPersistentFilters());
            }
            finally
            {
                _engine = null;
            }

            if (!hadFilters)
            {
                cleanupError = Combine(cleanupError, CleanupPersistentFilters());
            }
        }
        else
        {
            cleanupError = Combine(cleanupError, CleanupPersistentFilters());
        }

        return cleanupError;
    }

    private string? CleanupPersistentFilters()
    {
        try
        {
            var removed = WfpProtectionEngine.CleanupPersistent();
            if (removed > 0)
            {
                _logger.LogInformation("Removed {Count} stale persistent WFP filters.", removed);
            }

            return null;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Stale WFP filter cleanup failed.");
            return exception.Message;
        }
    }

    private string? RestoreDnsSnapshot()
    {
        var restored = NetworkDnsConfigurator.RestorePersisted(_logger, out var snapshotFound);
        if (snapshotFound && !restored)
        {
            return "The previous DNS snapshot could not be fully restored.";
        }

        return null;
    }

    private string? SaveState(WindowsProtectionState state)
    {
        try
        {
            _stateStore.Save(state);
            return null;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Protection state could not be saved.");
            return $"Protection state could not be saved: {exception.Message}";
        }
    }

    private static string? Combine(params string?[] messages)
    {
        var parts = messages
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray();

        return parts.Length == 0 ? null : string.Join(" ", parts);
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
            // Keep the state file so the next service start can re-apply an
            // enabled policy after a reboot or upgrade.
        }
    }
}
