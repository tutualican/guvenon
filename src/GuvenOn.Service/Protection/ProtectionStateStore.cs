using System.Text.Json;
using GuvenOn.Core;

namespace GuvenOn.Service.Protection;

internal sealed record WindowsProtectionState(
    bool Enabled,
    DateTimeOffset? PausedUntil,
    DateTimeOffset? AppliedAt);

/// <summary>
/// Persists the user's intent across service restarts. This is deliberately
/// separate from the WFP objects: the state file tells the service whether to
/// re-apply or remove filters that may have survived a crash.
/// </summary>
internal sealed class ProtectionStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _path;

    public ProtectionStateStore(string? path = null)
    {
        _path = path ?? AppPaths.ServiceState;
    }

    public WindowsProtectionState Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return Disabled;
            }

            return JsonSerializer.Deserialize<WindowsProtectionState>(
                       File.ReadAllText(_path),
                       JsonOptions)
                   ?? Disabled;
        }
        catch
        {
            return Disabled;
        }
    }

    public void Save(WindowsProtectionState state)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temporaryPath, _path, overwrite: true);
    }

    private static WindowsProtectionState Disabled => new(false, null, null);
}
