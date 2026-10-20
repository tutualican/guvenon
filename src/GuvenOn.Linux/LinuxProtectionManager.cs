using GuvenOn.Core.Protection;

namespace GuvenOn.Linux;

internal sealed class LinuxProtectionManager : IAsyncDisposable
{
    private readonly StateStore _stateStore = new();
    private readonly NftablesEngine _nft = new();
    private readonly LinuxDnsConfigurator _dns = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _statusLock = new();

    private ProtectionStatus _status = ProtectionStatus.Off;
    private IReadOnlyList<string> _lanAllowlist = Array.Empty<string>();
    private Timer? _resumeTimer;
    private bool _disposed;

    public ProtectionStatus Status
    {
        get
        {
            lock (_statusLock)
            {
                return _status;
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
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
            Log.Warning(allowlistError);
        }

        if (!state.Enabled)
        {
            // Remove a table or DNS marker left behind by a crash.
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var cleanupError = await SafeRestoreAsync(cancellationToken).ConfigureAwait(false);
                SetStatus(ProtectionStatus.Off with
                {
                    LastError = cleanupError ?? allowlistError,
                    LanAllowlist = _lanAllowlist
                });
            }
            finally
            {
                _gate.Release();
            }

            return;
        }

        if (state.PausedUntil is { } pausedUntil && pausedUntil > DateTimeOffset.UtcNow)
        {
            // A crash during pause can leave the nftables table or DNS marker
            // behind. Clear both again so a captive portal is not blocked.
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            string? cleanupError;
            try
            {
                cleanupError = await SafeRestoreAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }

            SetStatus(new ProtectionStatus(
                Enabled: true,
                EngineActive: false,
                DnsGuard: false,
                LanGuard: false,
                ActiveSince: null,
                PausedUntil: pausedUntil,
                FilterCount: 0,
                LastError: cleanupError ?? allowlistError)
            {
                LanAllowlist = _lanAllowlist
            });

            ScheduleResume(pausedUntil);
            Log.Info($"Protection is paused until {pausedUntil:O}.");
            return;
        }

        await EnableAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProtectionStatus> EnableAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CancelResume();
            return await EnableInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ProtectionStatus> EnableInternalAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return Status;
        }

        var policy = BuildPolicy();

        try
        {
            await _nft.ApplyAsync(policy, cancellationToken).ConfigureAwait(false);
            await _dns.ApplyAsync(policy, cancellationToken).ConfigureAwait(false);

            var now = DateTimeOffset.UtcNow;
            _stateStore.Save(new LinuxState(true, null, _dns.Method, now)
            {
                LanAllowlist = _lanAllowlist
            });
            SetStatus(new ProtectionStatus(
                Enabled: true,
                EngineActive: true,
                DnsGuard: _dns.IsConfigured,
                LanGuard: policy.BlockLan,
                ActiveSince: now,
                PausedUntil: null,
                FilterCount: _nft.RuleCount,
                LastError: null)
            {
                LanAllowlist = _lanAllowlist
            });

            Log.Info($"Protection enabled with {_nft.RuleCount} nftables rules.");
            return Status;
        }
        catch (Exception exception)
        {
            var cleanupError = await SafeRestoreAsync(cancellationToken).ConfigureAwait(false);
            _stateStore.Save(new LinuxState(false, null, null, null)
            {
                LanAllowlist = _lanAllowlist
            });
            var message = cleanupError is null ? exception.Message : $"{exception.Message} Cleanup: {cleanupError}";
            SetStatus(new ProtectionStatus(
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
            });

            Log.Error($"Protection could not be enabled: {message}");
            return Status;
        }
    }

    public async Task<ProtectionStatus> DisableAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CancelResume();
            var cleanupError = await SafeRestoreAsync(cancellationToken).ConfigureAwait(false);
            _stateStore.Save(new LinuxState(false, null, null, null)
            {
                LanAllowlist = _lanAllowlist
            });
            SetStatus(ProtectionStatus.Off with
            {
                LastError = cleanupError,
                LanAllowlist = _lanAllowlist
            });

            Log.Info("Protection disabled.");
            return Status;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ProtectionStatus> PauseAsync(
        int minutes,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CancelResume();
            var cleanupError = await SafeRestoreAsync(cancellationToken).ConfigureAwait(false);
            var until = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(minutes, 1, 120));

            _stateStore.Save(new LinuxState(true, until, null, null)
            {
                LanAllowlist = _lanAllowlist
            });
            SetStatus(new ProtectionStatus(
                Enabled: true,
                EngineActive: false,
                DnsGuard: false,
                LanGuard: false,
                ActiveSince: null,
                PausedUntil: until,
                FilterCount: 0,
                LastError: cleanupError)
            {
                LanAllowlist = _lanAllowlist
            });

            ScheduleResume(until);
            Log.Info($"Protection paused until {until:O}.");
            return Status;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ProtectionStatus> SetLanAllowlistAsync(
        IReadOnlyList<string> addresses,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IReadOnlyList<string> normalized;
            try
            {
                normalized = ProtectionPolicy.NormalizeLanAllowlist(addresses);
            }
            catch (Exception exception)
            {
                var invalid = Status with { LastError = exception.Message };
                SetStatus(invalid);
                return invalid;
            }

            var previous = _lanAllowlist;
            var status = Status;
            var pausedUntil = status.PausedUntil is { } until && until > DateTimeOffset.UtcNow
                ? until
                : (DateTimeOffset?)null;
            var dnsMethod = status.EngineActive ? _dns.Method : null;

            _stateStore.Save(new LinuxState(
                status.Enabled,
                pausedUntil,
                dnsMethod,
                status.ActiveSince)
            {
                LanAllowlist = normalized
            });
            _lanAllowlist = normalized;

            if (!status.EngineActive)
            {
                var updated = status with
                {
                    LanAllowlist = normalized,
                    LastError = null
                };
                SetStatus(updated);
                return updated;
            }

            try
            {
                await _nft.ApplyAsync(BuildPolicy(), cancellationToken).ConfigureAwait(false);
                var updated = status with
                {
                    FilterCount = _nft.RuleCount,
                    LanAllowlist = normalized,
                    LastError = null
                };
                SetStatus(updated);
                Log.Info($"Local device allowlist updated with {normalized.Count} entries.");
                return updated;
            }
            catch (Exception exception)
            {
                _lanAllowlist = previous;
                _stateStore.Save(new LinuxState(
                    status.Enabled,
                    pausedUntil,
                    dnsMethod,
                    status.ActiveSince)
                {
                    LanAllowlist = previous
                });

                try
                {
                    await _nft.ApplyAsync(BuildPolicy(), cancellationToken).ConfigureAwait(false);
                    var rolledBack = status with
                    {
                        FilterCount = _nft.RuleCount,
                        LanAllowlist = previous,
                        LastError = $"Local device allowlist could not be applied: {exception.Message}"
                    };
                    SetStatus(rolledBack);
                    return rolledBack;
                }
                catch (Exception restoreException)
                {
                    var cleanupError = await SafeRestoreAsync(cancellationToken).ConfigureAwait(false);
                    _stateStore.Save(new LinuxState(false, null, null, null)
                    {
                        LanAllowlist = previous
                    });
                    var failed = ProtectionStatus.Off with
                    {
                        LanAllowlist = previous,
                        LastError = string.Join(
                            " ",
                            new[]
                            {
                                $"Local device allowlist could not be applied: {exception.Message}",
                                $"The previous guard could not be restored: {restoreException.Message}",
                                cleanupError
                            }.Where(message => !string.IsNullOrWhiteSpace(message)))
                    };
                    SetStatus(failed);
                    return failed;
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private ProtectionPolicy BuildPolicy() =>
        ProtectionPolicy.Default with { LanAllowlist = _lanAllowlist };

    public async Task<ProtectionStatus> SelfTestAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var status = Status;
            if (!status.EngineActive)
            {
                return status;
            }

            try
            {
                var tablePresent = await _nft.IsTablePresentAsync(cancellationToken).ConfigureAwait(false);
                var dnsConfigured = _dns.IsConfigured;

                if (!tablePresent || !dnsConfigured)
                {
                    var message = !tablePresent
                        ? "The nftables table is missing."
                        : "The trusted DNS configuration is missing.";
                    status = status with
                    {
                        EngineActive = false,
                        DnsGuard = false,
                        LanGuard = false,
                        LastError = message
                    };
                    SetStatus(status);
                }
            }
            catch (Exception exception)
            {
                status = status with
                {
                    EngineActive = false,
                    DnsGuard = false,
                    LanGuard = false,
                    LastError = exception.Message
                };
                SetStatus(status);
            }

            return status;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Used by package removal when the daemon may not be running. Restores DNS
    /// and removes the nftables table without leaving an enabled state behind.
    /// </summary>
    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CancelResume();
            _ = await SafeRestoreAsync(cancellationToken).ConfigureAwait(false);
            _stateStore.Save(new LinuxState(false, null, null, null)
            {
                LanAllowlist = _lanAllowlist
            });
            SetStatus(ProtectionStatus.Off with { LanAllowlist = _lanAllowlist });
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string?> SafeRestoreAsync(CancellationToken cancellationToken)
    {
        string? error = null;

        // Remove the packet filter first. Restoring DNS may restart
        // systemd-resolved, and blocking port 53 during that restart would
        // make name resolution fail unnecessarily.
        try
        {
            await _nft.RemoveAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Log.Warning($"nftables cleanup failed: {exception.Message}");
        }

        try
        {
            await _dns.RestoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            error = error is null ? exception.Message : $"{error} {exception.Message}";
            Log.Warning($"DNS restore failed: {exception.Message}");
        }

        return error;
    }

    private void ScheduleResume(DateTimeOffset until)
    {
        _resumeTimer?.Dispose();
        var delay = until - DateTimeOffset.UtcNow;
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        _resumeTimer = new Timer(
            _ => _ = ResumeFromTimerAsync(until),
            state: null,
            dueTime: delay,
            period: Timeout.InfiniteTimeSpan);
    }

    private async Task ResumeFromTimerAsync(DateTimeOffset expectedUntil)
    {
        try
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed || Status.PausedUntil != expectedUntil)
                {
                    return;
                }

                CancelResume();
                Log.Info("Pause elapsed; re-applying protection.");
                _ = await EnableInternalAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception exception)
        {
            Log.Error($"Automatic resume failed: {exception.Message}");
        }
    }

    private void CancelResume()
    {
        _resumeTimer?.Dispose();
        _resumeTimer = null;
    }

    private void SetStatus(ProtectionStatus status)
    {
        lock (_statusLock)
        {
            _status = status;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CancelResume();
            _ = await SafeRestoreAsync(CancellationToken.None).ConfigureAwait(false);
            // Keep the state file so a restart can reapply an enabled policy.
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
