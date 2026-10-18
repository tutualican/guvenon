using GuvenOn.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GuvenOn.Service;

/// <summary>
/// Placeholder protection loop. Enforcement (WFP filters + NRPT/DoH) is added
/// in the next phase; the service lifecycle, logging, recovery and installer
/// integration are already real.
/// </summary>
public sealed class ProtectionWorker : BackgroundService
{
    private readonly ILogger<ProtectionWorker> _logger;

    public ProtectionWorker(ILogger<ProtectionWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "{Product} service {Version} started. Mode=monitor-only (preview).",
            AppInfo.ProductName,
            AppInfo.VersionString);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                _logger.LogDebug("Heartbeat. Enforcement=off; mode=preview.");
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }

        _logger.LogInformation("{Product} service stopped.", AppInfo.ProductName);
    }
}
