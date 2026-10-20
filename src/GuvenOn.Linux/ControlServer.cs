using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GuvenOn.Core.Protection;

namespace GuvenOn.Linux;

internal sealed class ControlServer
{
    private readonly LinuxProtectionManager _manager;

    public ControlServer(LinuxProtectionManager manager)
    {
        _manager = manager;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(LinuxPaths.RuntimeDirectory);

        var socketPath = LinuxPaths.ControlSocket;
        try
        {
            File.Delete(socketPath);
        }
        catch (FileNotFoundException)
        {
            // Expected on first start.
        }

        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        listener.Listen(8);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(socketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        Log.Info($"Control socket listening at {socketPath}.");

        while (!cancellationToken.IsCancellationRequested)
        {
            Socket client;
            try
            {
                client = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            _ = HandleClientAsync(client, cancellationToken);
        }
    }

    private async Task HandleClientAsync(Socket client, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                using var stream = new NetworkStream(client, ownsSocket: false);
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
                {
                    AutoFlush = true
                };

                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line) || line.Length > ControlProtocol.MaxRequestBytes)
                {
                    await WriteAsync(writer, new ControlResponse(false, _manager.Status, "Invalid request."), cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                ControlRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<ControlRequest>(line, ControlProtocol.JsonOptions);
                }
                catch (JsonException)
                {
                    await WriteAsync(writer, new ControlResponse(false, _manager.Status, "Invalid request."), cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                if (request is null)
                {
                    await WriteAsync(writer, new ControlResponse(false, _manager.Status, "Invalid request."), cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                var response = await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
                await WriteAsync(writer, response, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutdown.
            }
            catch (Exception exception)
            {
                Log.Warning($"Control request failed: {exception.Message}");
            }
        }
    }

    private async Task<ControlResponse> ExecuteAsync(
        ControlRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = request.Command switch
            {
                ProtectionCommand.Status => _manager.Status,
                ProtectionCommand.Enable => await _manager.EnableAsync(cancellationToken).ConfigureAwait(false),
                ProtectionCommand.Disable => await _manager.DisableAsync(cancellationToken).ConfigureAwait(false),
                ProtectionCommand.Pause => await _manager.PauseAsync(request.Minutes ?? 10, cancellationToken)
                    .ConfigureAwait(false),
                ProtectionCommand.SelfTest => await _manager.SelfTestAsync(cancellationToken).ConfigureAwait(false),
                ProtectionCommand.SetLanAllowlist => await _manager.SetLanAllowlistAsync(
                    request.LanAllowlist ?? Array.Empty<string>(),
                    cancellationToken).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(request))
            };

            var failed = request.Command != ProtectionCommand.Status && status.LastError is not null;
            return new ControlResponse(!failed, status, failed ? status.LastError : null);
        }
        catch (Exception exception)
        {
            Log.Warning($"Protection command {request.Command} failed: {exception.Message}");
            return new ControlResponse(false, _manager.Status, exception.Message);
        }
    }

    private static async Task WriteAsync(
        StreamWriter writer,
        ControlResponse response,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(response, ControlProtocol.JsonOptions);
        await writer.WriteLineAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
    }
}
