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
    private IReadOnlyList<string> _lanAllowlist = Array.Empty<string>();
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
    /// Verifies that the active policy still has every persistent filter in the
    /// WFP store. Status alone only describes what the service intended to do.
    /// </summary>
    public ProtectionStatus SelfTest()
    {
        lock (_gate)
        {
            if (!_status.EngineActive || _engine is null)
            {
                _status = _status with { LastError = "Protection is not active." };
                return _status;
            }

            try
            {
                var missing = WfpProtectionEngine.CountMissingPersistentFilters(_engine.FilterCount);
                if (missing == 0)
                {
                    _status = _status with { LastError = null };
                }
                else
                {
                    _status = _status with
                    {
                        LastError = $"{missing} persistent WFP filters are missing."
                    };
                }
            }
            catch (Exception exception)
            {
                _status = _status with { LastError = $"Self-test failed: {exception.Message}" };
            }

            return _status;
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
            string? allowlistError = null;
            try
            {
                _lanAllowlist = ProtectionPolicy.NormalizeLanAllowlist(state.LanAllowlist);
            }
            catch (Exception exception)
            {
                _lanAllowlist = Array.Empty<string>();
                allowlistError = $"Saved local device allowlist is invalid: {exception.Message}";
                _logger.LogWarning(exception, "Saved local device allowlist is invalid.");
            }

            if (!state.Enabled)
            {
                var cleanupError = Combine(
                    CleanupPersistentFilters(),
                    RestoreDnsSnapshot(),
                    allowlistError);
                _status = ProtectionStatus.Off with
                {
                    LastError = cleanupError,
                    LanAllowlist = _lanAllowlist
                };
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
                    LastError: Combine(cleanupError, allowlistError))
                {
                    LanAllowlist = _lanAllowlist
                };

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
            var stateError = SaveState(new WindowsProtectionState(false, null, null)
            {
                LanAllowlist = _lanAllowlist
            });
            var cleanupError = StopEngine();
            _status = ProtectionStatus.Off with
            {
                LastError = Combine(cleanupError, stateError),
                LanAllowlist = _lanAllowlist
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
            var stateError = SaveState(new WindowsProtectionState(true, until, null)
            {
                LanAllowlist = _lanAllowlist
            });

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
                LastError: Combine(cleanupError, stateError))
            {
                LanAllowlist = _lanAllowlist
            };

            ScheduleResume(until);
            _logger.LogInformation("Protection paused until {Until}.", until);
            return _status;
        }
    }

    public ProtectionStatus SetLanAllowlist(IReadOnlyList<string> addresses)
    {
        lock (_gate)
        {
            IReadOnlyList<string> normalized;
            try
            {
                normalized = ProtectionPolicy.NormalizeLanAllowlist(addresses);
            }
            catch (Exception exception)
            {
                _status = _status with { LastError = exception.Message };
                return _status;
            }

            var pausedUntil = _status.PausedUntil is { } until && until > DateTimeOffset.UtcNow
                ? until
                : (DateTimeOffset?)null;
            var stateError = SaveState(new WindowsProtectionState(
                _status.Enabled,
                pausedUntil,
                _status.ActiveSince)
            {
                LanAllowlist = normalized
            });
            if (stateError is not null)
            {
                _status = _status with { LastError = stateError };
                return _status;
            }

            var previous = _lanAllowlist;

            if (_engine is null)
            {
                _lanAllowlist = normalized;
                _status = _status with
                {
                    LanAllowlist = normalized,
                    LastError = null
                };
                return _status;
            }

            WfpProtectionEngine? candidate = null;
            try
            {
                _lanAllowlist = normalized;
                candidate = new WfpProtectionEngine(BuildPolicy());
                candidate.Apply();

                var oldEngine = _engine;
                _engine = candidate;
                candidate = null;

                oldEngine.Detach();
                oldEngine.Dispose();

                _status = _status with
                {
                    FilterCount = _engine.FilterCount,
                    LanAllowlist = normalized,
                    LastError = null
                };
                _logger.LogInformation(
                    "Local device allowlist updated with {Count} entries.",
                    normalized.Count);
                return _status;
            }
            catch (Exception exception)
            {
                candidate?.Dispose();
                _lanAllowlist = previous;
                var rollbackError = SaveState(new WindowsProtectionState(
                    _status.Enabled,
                    pausedUntil,
                    _status.ActiveSince)
                {
                    LanAllowlist = previous
                });
                _status = _status with
                {
                    LanAllowlist = previous,
                    LastError = Combine(
                        $"Local device allowlist could not be applied: {exception.Message}",
                        rollbackError)
                };
                _logger.LogError(exception, "Local device allowlist could not be applied.");
                return _status;
            }
        }
    }

    private ProtectionPolicy BuildPolicy() =>
        ProtectionPolicy.Default with { LanAllowlist = _lanAllowlist };

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
        var stateError = SaveState(new WindowsProtectionState(true, null, since)
        {
            LanAllowlist = _lanAllowlist
        });
        if (stateError is not null)
        {
            _status = ProtectionStatus.Off with
            {
                LastError = stateError,
                LanAllowlist = _lanAllowlist
            };
            return _status;
        }

        try
        {
            // Assign before applying so the outer catch can clean up both
            // resources even if the second step throws. StopEngine tolerates
            // partially initialized objects.
            var policy = BuildPolicy();
            _engine = new WfpProtectionEngine(policy);
            _dnsConfigurator = new NetworkDnsConfigurator(_logger);

            _engine.Apply();
            var changedAdapters = _dnsConfigurator.Apply(policy);

            _status = new ProtectionStatus(
                Enabled: true,
                EngineActive: true,
                DnsGuard: true,
                LanGuard: policy.BlockLan,
                ActiveSince: since,
                PausedUntil: null,
                FilterCount: _engine.FilterCount,
                LastError: null)
            {
                LanAllowlist = _lanAllowlist
            };

            _logger.LogInformation(
                "Protection enabled with {FilterCount} persistent WFP filters and trusted DNS on {AdapterCount} adapters.",
                _engine.FilterCount,
                changedAdapters);
        }
        catch (Exception exception)
        {
            var cleanupError = StopEngine();
            var disableStateError = SaveState(new WindowsProtectionState(false, null, null)
            {
                LanAllowlist = _lanAllowlist
            });
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
                LastError: message)
            {
                LanAllowlist = _lanAllowlist
            };

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
