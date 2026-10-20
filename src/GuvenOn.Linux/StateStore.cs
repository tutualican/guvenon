using System.Text.Json;

namespace GuvenOn.Linux;

internal sealed record LinuxState(
    bool Enabled,
    DateTimeOffset? PausedUntil,
    string? DnsMethod,
    DateTimeOffset? AppliedAt)
{
    public IReadOnlyList<string> LanAllowlist { get; init; } = Array.Empty<string>();
}

internal sealed class StateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public LinuxState Load()
    {
        try
        {
            if (!File.Exists(LinuxPaths.StateFile))
            {
                return new LinuxState(false, null, null, null);
            }

            return JsonSerializer.Deserialize<LinuxState>(
                       File.ReadAllText(LinuxPaths.StateFile),
                       JsonOptions)
                   ?? new LinuxState(false, null, null, null);
        }
        catch
        {
            return new LinuxState(false, null, null, null);
        }
    }

    public void Save(LinuxState state)
    {
        Directory.CreateDirectory(LinuxPaths.StateDirectory);
        var temporaryPath = LinuxPaths.StateFile + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temporaryPath, LinuxPaths.StateFile, overwrite: true);
    }

    public void Delete()
    {
        try
        {
            File.Delete(LinuxPaths.StateFile);
        }
        catch
        {
            // Best effort.
        }
    }
}
