using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using GuvenOn.Core.Protection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GuvenOn.Service.Protection;

/// <summary>
/// Serves the tray app's small control protocol. The pipe accepts authenticated
/// local users so the per-user tray app can talk to the LocalSystem service
/// without elevation; the command surface is limited to the enum in
/// <see cref="ProtectionCommand"/>.
/// </summary>
public sealed class ProtectionControlServer : BackgroundService
{
    private readonly ProtectionManager _manager;
    private readonly ILogger<ProtectionControlServer> _logger;

    public ProtectionControlServer(
        ProtectionManager manager,
        ILogger<ProtectionControlServer> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = CreatePipe();
                await pipe.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);

                await HandleAsync(pipe, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Control pipe request failed.");
            }
        }
    }

    private static NamedPipeServerStream CreatePipe()
    {
        var security = new PipeSecurity();

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        // The tray app runs as the interactive user. This is a local control
        // channel, not a remote service; no network exposure is created.
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            ControlProtocol.PipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 4096,
            outBufferSize: 4096,
            pipeSecurity: security);
    }

    private async Task HandleAsync(NamedPipeServerStream pipe, CancellationToken stoppingToken)
    {
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        };

        var line = await reader.ReadLineAsync(stoppingToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(line) || line.Length > ControlProtocol.MaxRequestBytes)
        {
            await WriteAsync(writer, new ControlResponse(false, _manager.Status, "Invalid request."), stoppingToken)
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
            await WriteAsync(writer, new ControlResponse(false, _manager.Status, "Invalid request."), stoppingToken)
                .ConfigureAwait(false);
            return;
        }

        if (request is null)
        {
            await WriteAsync(writer, new ControlResponse(false, _manager.Status, "Invalid request."), stoppingToken)
                .ConfigureAwait(false);
            return;
        }

        var response = Execute(request);
        await WriteAsync(writer, response, stoppingToken).ConfigureAwait(false);
    }

    private ControlResponse Execute(ControlRequest request)
    {
        try
        {
            var status = request.Command switch
            {
                ProtectionCommand.Status => _manager.Status,
                ProtectionCommand.Enable => _manager.Enable(),
                ProtectionCommand.Disable => _manager.Disable(),
                ProtectionCommand.Pause => _manager.Pause(request.Minutes ?? 10),
                ProtectionCommand.SelfTest => _manager.Status,
                _ => throw new ArgumentOutOfRangeException(nameof(request))
            };

            var failed = request.Command != ProtectionCommand.Status && status.LastError is not null;
            return new ControlResponse(!failed, status, failed ? status.LastError : null);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Protection command {Command} failed.", request.Command);
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
