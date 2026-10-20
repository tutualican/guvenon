using System.Text;

namespace GuvenOn.Core.Protection;

/// <summary>
/// Compiles the shared protection policy into a self-contained nftables ruleset.
/// The Linux daemon applies this in its own <c>inet guvenon</c> table; no other
/// firewall table is modified.
/// </summary>
public static class LinuxNftablesPolicy
{
    public const string TableName = "guvenon";

    private static readonly string[] PrivateV4Ranges =
    {
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
        "169.254.0.0/16",
        "224.0.0.0/4",
        "255.255.255.255"
    };

    private static readonly int[] DiscoveryUdpPorts = { 137, 138, 1900, 5353, 5355, 3702 };
    private static readonly int[] DiscoveryTcpPorts = { 139, 445, 2869, 3702 };

    public static string BuildRuleset(ProtectionPolicy policy, string? tableName = null)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var table = string.IsNullOrWhiteSpace(tableName) ? TableName : tableName;
        if (table.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
        {
            throw new ArgumentException("The nftables table name may contain only letters, digits and underscores.", nameof(tableName));
        }

        var builder = new StringBuilder();
        builder.AppendLine($"table inet {table} {{");

        AppendOutputChain(builder, policy);
        AppendInputChain(builder, policy);

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendOutputChain(StringBuilder builder, ProtectionPolicy policy)
    {
        builder.AppendLine("    chain output {");
        builder.AppendLine("        type filter hook output priority 0; policy accept;");
        builder.AppendLine("        oifname \"lo\" accept");
        builder.AppendLine("        udp sport 68 udp dport 67 accept");
        builder.AppendLine("        udp sport 546 udp dport 547 accept");

        AppendDnsPermits(builder, policy, inbound: false);
        AppendDnsDrops(builder, policy);

        if (policy.BlockLan)
        {
            AppendLanAllowlistPermits(builder, policy, inbound: false);
            builder.AppendLine($"        ip daddr {{ {string.Join(", ", PrivateV4Ranges)} }} drop");
            builder.AppendLine("        ip6 daddr fc00::/7 drop");
        }

        if (policy.BlockLegacyDiscovery)
        {
            builder.AppendLine($"        udp dport {{ {string.Join(", ", DiscoveryUdpPorts)} }} drop");
            builder.AppendLine($"        tcp dport {{ {string.Join(", ", DiscoveryTcpPorts)} }} drop");
        }

        builder.AppendLine("    }");
    }

    private static void AppendInputChain(StringBuilder builder, ProtectionPolicy policy)
    {
        builder.AppendLine("    chain input {");
        builder.AppendLine("        type filter hook input priority 0; policy accept;");
        builder.AppendLine("        iifname \"lo\" accept");
        builder.AppendLine("        udp sport 67 udp dport 68 accept");
        builder.AppendLine("        udp sport 547 udp dport 546 accept");

        if (policy.BlockLan)
        {
            AppendLanAllowlistPermits(builder, policy, inbound: true);
            builder.AppendLine($"        ip saddr {{ {string.Join(", ", PrivateV4Ranges)} }} drop");
            builder.AppendLine("        ip6 saddr fc00::/7 drop");
        }

        if (policy.BlockLegacyDiscovery)
        {
            builder.AppendLine($"        udp dport {{ {string.Join(", ", DiscoveryUdpPorts)} }} drop");
            builder.AppendLine($"        tcp dport {{ {string.Join(", ", DiscoveryTcpPorts)} }} drop");
        }

        builder.AppendLine("    }");
    }

    private static void AppendLanAllowlistPermits(
        StringBuilder builder,
        ProtectionPolicy policy,
        bool inbound)
    {
        var v4 = new List<string>();
        var v6 = new List<string>();
        foreach (var value in ProtectionPolicy.NormalizeLanAllowlist(policy.LanAllowlist))
        {
            if (value.Contains(':'))
            {
                v6.Add(value);
            }
            else
            {
                v4.Add(value);
            }
        }

        var direction = inbound ? "saddr" : "daddr";
        if (v4.Count > 0)
        {
            builder.AppendLine($"        ip {direction} {{ {string.Join(", ", v4)} }} accept");
        }

        if (v6.Count > 0)
        {
            builder.AppendLine($"        ip6 {direction} {{ {string.Join(", ", v6)} }} accept");
        }
    }

    private static void AppendDnsPermits(
        StringBuilder builder,
        ProtectionPolicy policy,
        bool inbound)
    {
        var ports = DnsPorts(policy);
        var portSet = string.Join(", ", ports);

        var v4 = policy.DnsServersV4.Select(address => address.ToString()).ToArray();
        var v6 = policy.DnsServersV6.Select(address => address.ToString()).ToArray();

        if (v4.Length > 0)
        {
            var addressSet = string.Join(", ", v4);
            var direction = inbound ? "ip saddr" : "ip daddr";
            builder.AppendLine($"        {direction} {{ {addressSet} }} udp dport {{ {portSet} }} accept");
            builder.AppendLine($"        {direction} {{ {addressSet} }} tcp dport {{ {portSet} }} accept");
        }

        if (v6.Length > 0)
        {
            var addressSet = string.Join(", ", v6);
            var direction = inbound ? "ip6 saddr" : "ip6 daddr";
            builder.AppendLine($"        {direction} {{ {addressSet} }} udp dport {{ {portSet} }} accept");
            builder.AppendLine($"        {direction} {{ {addressSet} }} tcp dport {{ {portSet} }} accept");
        }
    }

    private static void AppendDnsDrops(StringBuilder builder, ProtectionPolicy policy)
    {
        var portSet = string.Join(", ", DnsPorts(policy));
        builder.AppendLine($"        udp dport {{ {portSet} }} drop");
        builder.AppendLine($"        tcp dport {{ {portSet} }} drop");
    }

    private static int[] DnsPorts(ProtectionPolicy policy) =>
        policy.BlockDnsOverTls ? new[] { 53, 853 } : new[] { 53 };
}
