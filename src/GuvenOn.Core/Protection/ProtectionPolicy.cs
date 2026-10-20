using System.Net;
using System.Net.Sockets;

namespace GuvenOn.Core.Protection;

/// <summary>
/// Platform-neutral description of what the protection engine should enforce.
/// The Windows WFP backend and the future Linux nftables backend compile the
/// same policy.
/// </summary>
public sealed record ProtectionPolicy(
    IReadOnlyList<IPAddress> DnsServers,
    bool BlockLan,
    bool BlockLegacyDiscovery,
    bool BlockDnsOverTls)
{
    /// <summary>
    /// Resolver set used for the first protection release. Both Cloudflare and
    /// Quad9 are included so a single provider outage does not stop name
    /// resolution. Queries are still plain DNS; encrypted transport is the next
    /// phase.
    /// </summary>
    public static IReadOnlyList<IPAddress> DefaultDnsServers { get; } = new[]
    {
        IPAddress.Parse("1.1.1.1"),
        IPAddress.Parse("1.0.0.1"),
        IPAddress.Parse("9.9.9.9"),
        IPAddress.Parse("149.112.112.112"),
        IPAddress.Parse("2606:4700:4700::1111"),
        IPAddress.Parse("2606:4700:4700::1001"),
        IPAddress.Parse("2620:fe::fe"),
        IPAddress.Parse("2620:fe::9")
    };

    public static ProtectionPolicy Default { get; } = new(
        DefaultDnsServers,
        BlockLan: true,
        BlockLegacyDiscovery: true,
        BlockDnsOverTls: true);

    public IEnumerable<IPAddress> DnsServersV4 =>
        DnsServers.Where(address => address.AddressFamily == AddressFamily.InterNetwork);

    public IEnumerable<IPAddress> DnsServersV6 =>
        DnsServers.Where(address => address.AddressFamily == AddressFamily.InterNetworkV6);

    /// <summary>
    /// Converts an IPv4 address to the numeric host-order value expected by WFP.
    /// The value is big-endian (192.168.1.1 becomes 0xC0A80101), matching the
    /// address representation used by the Windows Filtering Platform.
    /// </summary>
    public static uint ToHostOrderUInt32(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException("An IPv4 address is required.", nameof(address));
        }

        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) |
               ((uint)bytes[1] << 16) |
               ((uint)bytes[2] << 8) |
               bytes[3];
    }

    public static uint NetmaskFromPrefix(int prefixLength)
    {
        if (prefixLength is < 0 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(prefixLength));
        }

        return prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
    }
}
