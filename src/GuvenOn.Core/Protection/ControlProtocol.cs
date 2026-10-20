using System.Text.Json;

namespace GuvenOn.Core.Protection;

/// <summary>
/// The only commands the service accepts. Keep this enum narrow: the pipe is a
/// privileged boundary and must never become a generic command runner.
/// </summary>
public enum ProtectionCommand
{
    Status,
    Enable,
    Disable,
    Pause,
    SelfTest
}

public sealed record ControlRequest(
    ProtectionCommand Command,
    int? Minutes = null)
{
    public static ControlRequest Status { get; } = new(ProtectionCommand.Status);

    public static ControlRequest Enable { get; } = new(ProtectionCommand.Enable);

    public static ControlRequest Disable { get; } = new(ProtectionCommand.Disable);

    public static ControlRequest PauseTenMinutes { get; } = new(ProtectionCommand.Pause, 10);
}

public sealed record ControlResponse(
    bool Ok,
    ProtectionStatus? Status,
    string? Error = null);

public static class ControlProtocol
{
    public const string PipeName = "GuvenOn.Control.v1";
    public const int MaxRequestBytes = 4096;

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
