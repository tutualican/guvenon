using GuvenOn.Core;
using GuvenOn.Service.Protection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GuvenOn.Service;

/// <summary>
/// Keeps the service alive and reports its state. The WFP engine is owned by
/// <see cref="ProtectionManager"/> and is only applied after an explicit user
/// command, so a fresh install never changes networking on its own.
/// </summary>
public sealed class ProtectionWorker : BackgroundService
{
    private readonly ILogger<ProtectionWorker> _logger;
    private readonly ProtectionManager _manager;

    public ProtectionWorker(
        ILogger<ProtectionWorker> logger,
        ProtectionManager manager)
    {
        _logger = logger;
        _manager = manager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "{Product} service {Version} started. EngineActive={EngineActive}; Enabled={Enabled}; PausedUntil={PausedUntil}.",
            AppInfo.ProductName,
            AppInfo.VersionString,
            _manager.Status.EngineActive,
            _manager.Status.Enabled,
            _manager.Status.PausedUntil);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var status = _manager.Status;
                _logger.LogDebug(
                    "Heartbeat. EngineActive={EngineActive}; Filters={FilterCount}; PausedUntil={PausedUntil}.",
                    status.EngineActive,
                    status.FilterCount,
                    status.PausedUntil);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }

        _logger.LogInformation("{Product} service stopped.", AppInfo.ProductName);
    }
}
