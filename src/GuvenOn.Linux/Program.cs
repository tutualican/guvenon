using System.Runtime.InteropServices;
using GuvenOn.Core.Protection;
using GuvenOn.Linux;

if (args.Any(argument => argument.Equals("--print-ruleset", StringComparison.OrdinalIgnoreCase)))
{
    Console.Write(LinuxNftablesPolicy.BuildRuleset(ProtectionPolicy.Default));
    return 0;
}

if (args.Any(argument => argument.Equals("--daemon", StringComparison.OrdinalIgnoreCase)))
{
    return await RunDaemonAsync();
}

if (args.Any(argument => argument.Equals("--cleanup", StringComparison.OrdinalIgnoreCase)))
{
    return await RunCleanupAsync();
}

return await Cli.RunAsync(args);

static async Task<int> RunDaemonAsync()
{
    if (OperatingSystem.IsWindows())
    {
        Console.Error.WriteLine("The Guven-ON Linux daemon cannot run on Windows.");
        return 1;
    }

    if (!IsRoot())
    {
        Console.Error.WriteLine("The Guven-ON daemon must run as root (use systemd or sudo).");
        return 1;
    }

    using var cancellation = new CancellationTokenSource();

    using var sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
    {
        context.Cancel = true;
        cancellation.Cancel();
    });

    using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
    {
        context.Cancel = true;
        cancellation.Cancel();
    });

    Log.Info("Guven-ON Linux daemon starting.");

    await using var manager = new LinuxProtectionManager();
    await manager.StartAsync(cancellation.Token).ConfigureAwait(false);

    var server = new ControlServer(manager);
    try
    {
        await server.RunAsync(cancellation.Token).ConfigureAwait(false);
    }
    finally
    {
        Log.Info("Guven-ON Linux daemon stopping.");
        await manager.DisposeAsync().ConfigureAwait(false);
    }

    return 0;
}

static async Task<int> RunCleanupAsync()
{
    if (OperatingSystem.IsWindows())
    {
        return 1;
    }

    if (!IsRoot())
    {
        Console.Error.WriteLine("Cleanup must run as root.");
        return 1;
    }

    await using var manager = new LinuxProtectionManager();
    await manager.CleanupAsync().ConfigureAwait(false);
    await manager.DisposeAsync().ConfigureAwait(false);
    return 0;
}

static bool IsRoot() =>
    string.Equals(Environment.UserName, "root", StringComparison.Ordinal);
