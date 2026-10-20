using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GuvenOn.Core;
using GuvenOn.Core.Protection;

namespace GuvenOn.Linux;

internal static class Cli
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        if (args[0] is "version" or "--version")
        {
            Console.WriteLine($"{AppInfo.ProductName} {AppInfo.VersionString} (Linux alpha)");
            return 0;
        }

        ControlRequest request;
        switch (args[0])
        {
            case "status":
                request = ControlRequest.Status;
                break;
            case "enable":
                request = ControlRequest.Enable;
                break;
            case "disable":
            case "panic":
                request = ControlRequest.Disable;
                break;
            case "pause":
            {
                var minutes = 10;
                if (args.Length > 1 && (!int.TryParse(args[1], out minutes) || minutes is < 1 or > 120))
                {
                    Console.Error.WriteLine("Pause duration must be between 1 and 120 minutes.");
                    return 2;
                }

                request = new ControlRequest(ProtectionCommand.Pause, minutes);
                break;
            }
            case "selftest":
                request = new ControlRequest(ProtectionCommand.SelfTest);
                break;
            default:
                Console.Error.WriteLine($"Unknown command: {args[0]}");
                PrintHelp();
                return 2;
        }

        ControlResponse response;
        try
        {
            response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SocketException or IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("The Guven-ON daemon is not reachable.");
            Console.Error.WriteLine("Start it with: sudo systemctl start guvenon");
            Console.Error.WriteLine("If it is running, run this command with sudo.");
            return 3;
        }

        if (response.Status is not null)
        {
            PrintStatus(response.Status);
        }

        if (!response.Ok)
        {
            if (!string.IsNullOrWhiteSpace(response.Error))
            {
                Console.Error.WriteLine($"Error: {response.Error}");
            }

            return 1;
        }

        return 0;
    }

    private static async Task<ControlResponse> SendAsync(
        ControlRequest request,
        CancellationToken cancellationToken)
    {
        using var socket = new Socket(
            AddressFamily.Unix,
            SocketType.Stream,
            ProtocolType.Unspecified);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(5));

        await socket.ConnectAsync(
            new UnixDomainSocketEndPoint(LinuxPaths.ControlSocket),
            timeoutSource.Token).ConfigureAwait(false);

        using var stream = new NetworkStream(socket, ownsSocket: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        };

        var payload = JsonSerializer.Serialize(request, ControlProtocol.JsonOptions);
        await writer.WriteLineAsync(payload.AsMemory(), timeoutSource.Token).ConfigureAwait(false);

        var line = await reader.ReadLineAsync(timeoutSource.Token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new IOException("The daemon closed the control channel without a response.");
        }

        return JsonSerializer.Deserialize<ControlResponse>(line, ControlProtocol.JsonOptions)
               ?? throw new IOException("The daemon returned an empty control response.");
    }

    private static void PrintStatus(ProtectionStatus status)
    {
        var state = status switch
        {
            { EngineActive: true } => "active",
            { PausedUntil: { } until } when until > DateTimeOffset.UtcNow => "paused",
            { LastError: not null } => "error",
            _ => "off"
        };

        Console.WriteLine($"{AppInfo.ProductName} {AppInfo.VersionString} (Linux alpha)");
        Console.WriteLine($"Protection:  {state}");
        Console.WriteLine($"DNS guard:   {(status.DnsGuard ? "on" : "off")}");
        Console.WriteLine($"LAN guard:   {(status.LanGuard ? "on" : "off")}");
        Console.WriteLine($"Filters:     {status.FilterCount}");

        if (status.ActiveSince is { } activeSince)
        {
            Console.WriteLine($"Since:       {activeSince.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        }

        if (status.PausedUntil is { } pausedUntil)
        {
            Console.WriteLine($"Resumes:     {pausedUntil.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
        }

        if (!string.IsNullOrWhiteSpace(status.LastError))
        {
            Console.WriteLine($"Last error:  {status.LastError}");
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Guven-ON Linux alpha");
        Console.WriteLine();
        Console.WriteLine("Usage: guvenonctl <command>");
        Console.WriteLine();
        Console.WriteLine("  status          Show protection state");
        Console.WriteLine("  enable          Apply trusted DNS and the nftables guards");
        Console.WriteLine("  disable         Remove the guards and restore DNS");
        Console.WriteLine("  panic           Same as disable");
        Console.WriteLine("  pause [minutes] Temporarily allow a captive portal (1-120, default 10)");
        Console.WriteLine("  selftest        Check that the nftables and DNS guards are present");
        Console.WriteLine("  version         Show the installed version");
        Console.WriteLine();
        Console.WriteLine("Run with sudo; the daemon is root-only by design.");
    }
}
