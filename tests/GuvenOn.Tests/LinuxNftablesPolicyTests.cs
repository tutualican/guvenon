using System.Net;
using GuvenOn.Core.Protection;

namespace GuvenOn.Tests;

[TestClass]
public sealed class LinuxNftablesPolicyTests
{
    [TestMethod]
    public void Ruleset_ContainsTrustedDnsPermitsBeforeDrops()
    {
        var rules = LinuxNftablesPolicy.BuildRuleset(ProtectionPolicy.Default);

        StringAssert.Contains(rules, "table inet guvenon");
        StringAssert.Contains(rules, "ip daddr { 1.1.1.1, 1.0.0.1, 9.9.9.9, 149.112.112.112 } udp dport { 53, 853 } accept");
        StringAssert.Contains(rules, "ip6 daddr { 2606:4700:4700::1111, 2606:4700:4700::1001, 2620:fe::fe, 2620:fe::9 } udp dport { 53, 853 } accept");
        StringAssert.Contains(rules, "udp dport { 53, 853 } drop");
        StringAssert.Contains(rules, "ip daddr { 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 169.254.0.0/16, 224.0.0.0/4, 255.255.255.255 } drop");
        StringAssert.Contains(rules, "ip saddr { 10.0.0.0/8");
        StringAssert.Contains(rules, "ip6 daddr fc00::/7 drop");
        StringAssert.Contains(rules, "ip6 saddr fc00::/7 drop");

        var permitIndex = rules.IndexOf("udp dport { 53, 853 } accept", StringComparison.Ordinal);
        var dropIndex = rules.IndexOf("udp dport { 53, 853 } drop", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, permitIndex);
        Assert.IsGreaterThan(permitIndex, dropIndex);
    }

    [TestMethod]
    public void Ruleset_OmitsLanAndDohRulesWhenDisabled()
    {
        var policy = new ProtectionPolicy(
            new[] { IPAddress.Parse("1.1.1.1") },
            BlockLan: false,
            BlockLegacyDiscovery: false,
            BlockDnsOverTls: false);

        var rules = LinuxNftablesPolicy.BuildRuleset(policy);

        StringAssert.Contains(rules, "udp dport { 53 } accept");
        StringAssert.Contains(rules, "udp dport { 53 } drop");
        Assert.IsFalse(rules.Contains("10.0.0.0/8", StringComparison.Ordinal));
        Assert.IsFalse(rules.Contains("5353", StringComparison.Ordinal));
        Assert.IsFalse(rules.Contains("853", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Ruleset_CanUseTemporaryTableForSyntaxCheck()
    {
        var rules = LinuxNftablesPolicy.BuildRuleset(ProtectionPolicy.Default, "guvenon_check");

        StringAssert.StartsWith(rules, "table inet guvenon_check {");
        Assert.IsFalse(rules.Contains("table inet guvenon {", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Ruleset_KeepsDhcpAndLoopbackBeforeLanDrops()
    {
        var rules = LinuxNftablesPolicy.BuildRuleset(ProtectionPolicy.Default);

        StringAssert.Contains(rules, "oifname \"lo\" accept");
        StringAssert.Contains(rules, "iifname \"lo\" accept");
        StringAssert.Contains(rules, "udp sport 68 udp dport 67 accept");
        StringAssert.Contains(rules, "udp sport 67 udp dport 68 accept");

        var dhcpIndex = rules.IndexOf("udp sport 68 udp dport 67 accept", StringComparison.Ordinal);
        var lanDropIndex = rules.IndexOf("ip daddr { 10.0.0.0/8", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, dhcpIndex);
        Assert.IsGreaterThan(dhcpIndex, lanDropIndex);
    }
}
