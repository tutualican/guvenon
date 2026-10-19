using System.Reflection;

namespace GuvenOn.Core;

public static class AppInfo
{
    public const string ProductName = "Guven-ON";
    public const string ServiceName = "GuvenOnSvc";
    public const string ServiceDisplayName = "Guven-ON Protection Service";
    public const string ServiceDescription =
        "Monitors network conditions for Guven-ON. Enforcement is not enabled in this preview.";
    public const string DefaultChannel = "stable";

    // Update checks do not use the GitHub API, so no token is needed.
    public const string GitHubOwner = "tutualican";
    public const string GitHubRepo = "guvenon";

    public static string VersionString
    {
        get
        {
            var value = typeof(AppInfo).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (string.IsNullOrWhiteSpace(value))
            {
                value = typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            }

            var plus = value.IndexOf('+');
            return plus >= 0 ? value[..plus] : value;
        }
    }

    public static SemVer Version => SemVer.TryParse(VersionString, out var version) ? version : new SemVer(0, 0, 0);

    /// <summary>Resolves through github.com and redirects to the latest release asset.</summary>
    public static string LatestManifestUrl =>
        $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest/download/latest.json";

    /// <summary>Optional CDN mirror. It is tried only when GitHub is unreachable.</summary>
    public static string FallbackManifestUrl => "https://dl.guvenon.app/win/latest.json";

    public static string ReleasesUrl => $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases";

    public static string ReleaseTagUrl(SemVer version) =>
        $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/tag/v{version}";
}
