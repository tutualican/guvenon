using GuvenOn.Core;
using GuvenOn.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddWindowsService(options => options.ServiceName = AppInfo.ServiceName);
builder.Services.AddHostedService<ProtectionWorker>();

using var host = builder.Build();
await host.RunAsync();
return 0;
