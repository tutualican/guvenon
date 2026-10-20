using GuvenOn.Service.Protection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GuvenOn.Tests;

[TestClass]
public sealed class ProtectionManagerTests
{
    [TestMethod]
    public void SelfTestWhenInactiveReportsError()
    {
        using var manager = new ProtectionManager(NullLogger<ProtectionManager>.Instance);

        var status = manager.SelfTest();

        Assert.IsFalse(status.EngineActive);
        Assert.AreEqual("Protection is not active.", status.LastError);
    }

    [TestMethod]
    public void SetLanAllowlistRejectsInvalidAddressWithoutChangingState()
    {
        using var manager = new ProtectionManager(NullLogger<ProtectionManager>.Instance);

        var status = manager.SetLanAllowlist(new[] { "not-an-ip" });

        Assert.IsFalse(status.Enabled);
        Assert.IsEmpty(status.LanAllowlist);
        StringAssert.Contains(status.LastError, "Invalid IP address");
    }
}
