using System.Text.Json;
using GuvenOn.Core;
using GuvenOn.Core.Protection;
using GuvenOn.Service;
using GuvenOn.Service.Protection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

if (args.Any(argument => argument.Equals("--install", StringComparison.OrdinalIgnoreCase)))
{
    return ServiceInstaller.Install();
}

if (args.Any(argument => argument.Equals("--stop", StringComparison.OrdinalIgnoreCase)))
{
    return ServiceInstaller.Stop();
}

if (args.Any(argument => argument.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
{
    return ServiceInstaller.Uninstall();
}

if (args.Any(argument => argument.Equals("--wfp-layout", StringComparison.OrdinalIgnoreCase)))
{
    var errors = WfpInterop.ValidateLayouts();
    if (errors.Count == 0)
    {
        Console.WriteLine("WFP layout validation passed.");
        return 0;
    }

    foreach (var error in errors)
    {
        Console.Error.WriteLine(error);
    }

    return 1;
}

if (args.Any(argument => argument.Equals("--wfp-cleanup", StringComparison.OrdinalIgnoreCase)))
{
    try
    {
        var removed = WfpProtectionEngine.CleanupPersistent();
        Console.WriteLine($"Removed {removed} persistent WFP filters.");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Persistent WFP cleanup failed: {exception.Message}");
        return 1;
    }
}

if (args.Any(argument => argument.Equals("--restore-network", StringComparison.OrdinalIgnoreCase)))
{
    using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    var logger = loggerFactory.CreateLogger("GuvenOn.Restore");
    var restored = NetworkDnsConfigurator.RestorePersisted(logger, out var snapshotFound);
    if (!snapshotFound)
    {
        Console.WriteLine("No DNS snapshot found.");
        return 0;
    }

    if (!restored)
    {
        Console.Error.WriteLine("The DNS snapshot could not be fully restored.");
        return 1;
    }

    Console.WriteLine("DNS snapshot restored.");
    return 0;
}

if (args.Any(argument => argument.Equals("--status", StringComparison.OrdinalIgnoreCase)))
{
    return await RunControlAsync(ControlRequest.Status);
}

if (args.Any(argument => argument.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
{
    return await RunControlAsync(new ControlRequest(ProtectionCommand.SelfTest));
}

if (args.Any(argument => argument.Equals("--enable", StringComparison.OrdinalIgnoreCase)))
{
    return await RunControlAsync(ControlRequest.Enable);
}

if (args.Any(argument => argument.Equals("--disable", StringComparison.OrdinalIgnoreCase)) ||
    args.Any(argument => argument.Equals("--panic", StringComparison.OrdinalIgnoreCase)))
{
    return await RunControlAsync(ControlRequest.Disable);
}

var pauseIndex = Array.FindIndex(
    args,
    argument => argument.Equals("--pause", StringComparison.OrdinalIgnoreCase));

if (pauseIndex >= 0)
{
    var minutes = 10;
    if (pauseIndex + 1 < args.Length &&
        int.TryParse(args[pauseIndex + 1], out var parsedMinutes))
    {
        minutes = parsedMinutes;
    }

    return await RunControlAsync(new ControlRequest(ProtectionCommand.Pause, minutes));
}

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddWindowsService(options => options.ServiceName = AppInfo.ServiceName);
builder.Services.AddSingleton<ProtectionManager>();
builder.Services.AddHostedService<ProtectionWorker>();
builder.Services.AddHostedService<ProtectionControlServer>();

using var host = builder.Build();

// Restore the previous protection intent before the control pipe starts
// accepting commands. This also cleans up persistent WFP filters left by a
// crashed service instance.
host.Services.GetRequiredService<ProtectionManager>().Start();

await host.RunAsync();
return 0;

static async Task<int> RunControlAsync(ControlRequest request)
{
    try
    {
        var timeout = request.Command == ProtectionCommand.Status
            ? TimeSpan.FromSeconds(2)
            : TimeSpan.FromSeconds(60);
        var response = await ProtectionClient.SendAsync(request, timeout);
        Console.WriteLine(JsonSerializer.Serialize(response, ControlProtocol.JsonOptions));
        return response.Ok ? 0 : 1;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 2;
    }
}
