using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace GuvenOn.Core.Protection;

/// <summary>
/// Small named-pipe client used by the tray app and by the service's diagnostic
/// CLI. It intentionally has no retry loop: a missing service should surface as
/// a clear "unavailable" state in the UI, not as a hidden hang.
/// </summary>
public static class ProtectionClient
{
    public static async Task<ControlResponse> SendAsync(
        ControlRequest request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(2);

        using var pipe = new NamedPipeClientStream(
            ".",
            ControlProtocol.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(effectiveTimeout);

        await pipe.ConnectAsync(effectiveTimeout, timeoutSource.Token).ConfigureAwait(false);

        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        };
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);

        var payload = JsonSerializer.Serialize(request, ControlProtocol.JsonOptions);
        await writer.WriteLineAsync(payload.AsMemory(), timeoutSource.Token).ConfigureAwait(false);

        var line = await reader.ReadLineAsync(timeoutSource.Token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new IOException("The service closed the control channel without a response.");
        }

        return JsonSerializer.Deserialize<ControlResponse>(line, ControlProtocol.JsonOptions)
               ?? throw new IOException("The service returned an empty control response.");
    }

    public static Task<ControlResponse> StatusAsync(CancellationToken cancellationToken = default) =>
        SendAsync(ControlRequest.Status, cancellationToken: cancellationToken);

    public static Task<ControlResponse> EnableAsync(
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null) =>
        SendAsync(ControlRequest.Enable, timeout, cancellationToken);

    public static Task<ControlResponse> DisableAsync(
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null) =>
        SendAsync(ControlRequest.Disable, timeout, cancellationToken);

    public static Task<ControlResponse> PauseAsync(
        int minutes,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null) =>
        SendAsync(new ControlRequest(ProtectionCommand.Pause, minutes), timeout, cancellationToken);

    public static Task<ControlResponse> SelfTestAsync(
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null) =>
        SendAsync(new ControlRequest(ProtectionCommand.SelfTest), timeout, cancellationToken);
}
