namespace GuvenOn.Core.Update;

public sealed class UpdateManifest
{
    public string Channel { get; set; } = AppInfo.DefaultChannel;
    public string Version { get; set; } = string.Empty;
    public string? MinVersion { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long Size { get; set; }
    public int Rollout { get; set; } = 100;
    public List<string> Revoked { get; set; } = new();
    public DateTimeOffset? Published { get; set; }
    public Dictionary<string, string>? Notes { get; set; }

    /// <summary>Base64 ECDSA P-256 signature over the canonical manifest body. Not enforced yet.</summary>
    public string? Signature { get; set; }
}
