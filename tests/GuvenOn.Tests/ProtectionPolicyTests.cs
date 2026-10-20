using System.Net;
using GuvenOn.Core.Protection;

namespace GuvenOn.Tests;

[TestClass]
public sealed class ProtectionPolicyTests
{
    [TestMethod]
    public void DefaultPolicy_ContainsV4AndV6Resolvers()
    {
        var policy = ProtectionPolicy.Default;

        Assert.IsTrue(policy.DnsServersV4.Any());
        Assert.IsTrue(policy.DnsServersV6.Any());
        Assert.IsTrue(policy.BlockLan);
        Assert.IsTrue(policy.BlockLegacyDiscovery);
        Assert.IsTrue(policy.BlockDnsOverTls);
    }

    [TestMethod]
    public void ToHostOrderUInt32_UsesBigEndianNumericValue()
    {
        Assert.AreEqual(0xC0A80101u, ProtectionPolicy.ToHostOrderUInt32(IPAddress.Parse("192.168.1.1")));
        Assert.AreEqual(0x0A000001u, ProtectionPolicy.ToHostOrderUInt32(IPAddress.Parse("10.0.0.1")));
        Assert.AreEqual(uint.MaxValue, ProtectionPolicy.ToHostOrderUInt32(IPAddress.Parse("255.255.255.255")));
    }

    [TestMethod]
    public void ToHostOrderUInt32_RejectsIpv6()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => ProtectionPolicy.ToHostOrderUInt32(IPAddress.Parse("2001:db8::1")));
    }

    [TestMethod]
    public void NetmaskFromPrefix_ProducesExpectedMasks()
    {
        Assert.AreEqual(0xFFFFFFFFu, ProtectionPolicy.NetmaskFromPrefix(32));
        Assert.AreEqual(0xFFFFFF00u, ProtectionPolicy.NetmaskFromPrefix(24));
        Assert.AreEqual(0xFFF00000u, ProtectionPolicy.NetmaskFromPrefix(12));
        Assert.AreEqual(0xFF000000u, ProtectionPolicy.NetmaskFromPrefix(8));
        Assert.AreEqual(0u, ProtectionPolicy.NetmaskFromPrefix(0));
    }

    [TestMethod]
    public void NetmaskFromPrefix_RejectsOutOfRange()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ProtectionPolicy.NetmaskFromPrefix(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ProtectionPolicy.NetmaskFromPrefix(33));
    }

    [TestMethod]
    public void NormalizeLanAllowlist_TrimsDeduplicatesAndKeepsBothFamilies()
    {
        var result = ProtectionPolicy.NormalizeLanAllowlist(new[]
        {
            " 192.168.1.50 ",
            "192.168.1.50",
            "fd00::50",
            ""
        });

        CollectionAssert.AreEqual(
            new[] { "192.168.1.50", "fd00::50" },
            result.ToArray());
    }

    [TestMethod]
    public void NormalizeLanAllowlist_RejectsInvalidAndUnsafeAddresses()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => ProtectionPolicy.NormalizeLanAllowlist(new[] { "not-an-ip" }));
        Assert.ThrowsExactly<ArgumentException>(
            () => ProtectionPolicy.NormalizeLanAllowlist(new[] { "192.168.1.0/24" }));
        Assert.ThrowsExactly<ArgumentException>(
            () => ProtectionPolicy.NormalizeLanAllowlist(new[] { "127.0.0.1" }));
        Assert.ThrowsExactly<ArgumentException>(
            () => ProtectionPolicy.NormalizeLanAllowlist(new[] { "224.0.0.1" }));
        Assert.ThrowsExactly<ArgumentException>(
            () => ProtectionPolicy.NormalizeLanAllowlist(new[] { "ff02::1" }));
    }

    [TestMethod]
    public void NormalizeLanAllowlist_EnforcesEntryLimit()
    {
        var values = Enumerable.Range(1, ProtectionPolicy.MaxLanAllowlistEntries + 1)
            .Select(index => $"192.168.1.{index}")
            .ToArray();

        Assert.ThrowsExactly<ArgumentException>(
            () => ProtectionPolicy.NormalizeLanAllowlist(values));
    }

    [TestMethod]
    public void PausedStatus_IsPausedOnlyBeforeDeadline()
    {
        var now = DateTimeOffset.Parse("2026-06-01T12:00:00Z");
        var status = new ProtectionStatus(
            Enabled: true,
            EngineActive: false,
            DnsGuard: false,
            LanGuard: false,
            ActiveSince: null,
            PausedUntil: now.AddMinutes(5),
            FilterCount: 0,
            LastError: null);

        Assert.IsTrue(status.IsPaused(now));
        Assert.IsFalse(status.IsPaused(now.AddMinutes(5)));
        Assert.IsFalse(ProtectionStatus.Off.IsPaused(now));
    }
}
