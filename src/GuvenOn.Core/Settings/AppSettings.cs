namespace GuvenOn.Core.Settings;

public sealed class AppSettings
{
    /// <summary>"auto", "en", "de" or "tr".</summary>
    public string Language { get; set; } = "auto";

    public bool StartWithWindows { get; set; }

    public string Channel { get; set; } = AppInfo.DefaultChannel;

    public DateTimeOffset? LastUpdateCheck { get; set; }

    public string? SkippedVersion { get; set; }
}
