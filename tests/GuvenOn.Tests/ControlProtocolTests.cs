using System.Text.Json;
using GuvenOn.Core.Protection;

namespace GuvenOn.Tests;

[TestClass]
public sealed class ControlProtocolTests
{
    [TestMethod]
    public void Request_RoundTrips()
    {
        var request = new ControlRequest(ProtectionCommand.Pause, 10);
        var json = JsonSerializer.Serialize(request, ControlProtocol.JsonOptions);
        var loaded = JsonSerializer.Deserialize<ControlRequest>(json, ControlProtocol.JsonOptions);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(ProtectionCommand.Pause, loaded.Command);
        Assert.AreEqual(10, loaded.Minutes);
    }

    [TestMethod]
    public void Response_RoundTripsStatus()
    {
        var status = new ProtectionStatus(
            Enabled: true,
            EngineActive: true,
            DnsGuard: true,
            LanGuard: true,
            ActiveSince: DateTimeOffset.Parse("2026-06-01T12:00:00Z"),
            PausedUntil: null,
            FilterCount: 24,
            LastError: null);

        var response = new ControlResponse(true, status);
        var json = JsonSerializer.Serialize(response, ControlProtocol.JsonOptions);
        var loaded = JsonSerializer.Deserialize<ControlResponse>(json, ControlProtocol.JsonOptions);

        Assert.IsNotNull(loaded);
        Assert.IsTrue(loaded.Ok);
        Assert.IsNotNull(loaded.Status);
        Assert.IsTrue(loaded.Status.EngineActive);
        Assert.AreEqual(24, loaded.Status.FilterCount);
    }

    [TestMethod]
    public void StatusRequest_IsStatusCommand()
    {
        Assert.AreEqual(ProtectionCommand.Status, ControlRequest.Status.Command);
        Assert.AreEqual(ProtectionCommand.Enable, ControlRequest.Enable.Command);
        Assert.AreEqual(ProtectionCommand.Disable, ControlRequest.Disable.Command);
        Assert.AreEqual(ProtectionCommand.Pause, ControlRequest.PauseTenMinutes.Command);
        Assert.AreEqual(10, ControlRequest.PauseTenMinutes.Minutes);
    }
}
