namespace GuvenOn.Core.Protection;

/// <summary>
/// The state the service reports to the tray app. A monitor-only service must
/// never look like an active protection engine, so <see cref="EngineActive"/>
/// is separate from <see cref="Enabled"/>.
/// </summary>
public sealed record ProtectionStatus(
    bool Enabled,
    bool EngineActive,
    bool DnsGuard,
    bool LanGuard,
    DateTimeOffset? ActiveSince,
    DateTimeOffset? PausedUntil,
    int FilterCount,
    string? LastError)
{
    public IReadOnlyList<string> LanAllowlist { get; init; } = Array.Empty<string>();

    public static ProtectionStatus Off { get; } = new(
        Enabled: false,
        EngineActive: false,
        DnsGuard: false,
        LanGuard: false,
        ActiveSince: null,
        PausedUntil: null,
        FilterCount: 0,
        LastError: null);

    public bool IsPaused(DateTimeOffset now) => PausedUntil is { } until && until > now;
}
