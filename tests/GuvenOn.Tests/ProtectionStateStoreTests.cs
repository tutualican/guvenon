using GuvenOn.Service.Protection;

namespace GuvenOn.Tests;

[TestClass]
public sealed class ProtectionStateStoreTests
{
    private string _directory = null!;
    private string _path = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "guvenon-tests",
            Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_directory, "service-state.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch
        {
            // Best-effort test cleanup.
        }
    }

    [TestMethod]
    public void MissingStateFileLoadsAsDisabled()
    {
        var state = new ProtectionStateStore(_path).Load();

        Assert.IsFalse(state.Enabled);
        Assert.IsNull(state.PausedUntil);
        Assert.IsNull(state.AppliedAt);
        Assert.IsEmpty(state.LanAllowlist);
    }

    [TestMethod]
    public void EnabledStateRoundTrips()
    {
        var appliedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var store = new ProtectionStateStore(_path);

        store.Save(new WindowsProtectionState(true, null, appliedAt));
        var loaded = store.Load();

        Assert.IsTrue(loaded.Enabled);
        Assert.IsNull(loaded.PausedUntil);
        Assert.AreEqual(appliedAt, loaded.AppliedAt);
    }

    [TestMethod]
    public void PausedStateRoundTrips()
    {
        var until = DateTimeOffset.UtcNow.AddMinutes(10);
        var store = new ProtectionStateStore(_path);

        store.Save(new WindowsProtectionState(true, until, null));
        var loaded = store.Load();

        Assert.IsTrue(loaded.Enabled);
        Assert.AreEqual(until, loaded.PausedUntil);
        Assert.IsNull(loaded.AppliedAt);
    }

    [TestMethod]
    public void LanAllowlistRoundTrips()
    {
        var store = new ProtectionStateStore(_path);
        store.Save(new WindowsProtectionState(true, null, DateTimeOffset.UtcNow)
        {
            LanAllowlist = new[] { "192.168.1.50", "fd00::50" }
        });

        var loaded = store.Load();

        CollectionAssert.AreEqual(
            new[] { "192.168.1.50", "fd00::50" },
            loaded.LanAllowlist.ToArray());
    }

    [TestMethod]
    public void SavingReplacesPreviousState()
    {
        var store = new ProtectionStateStore(_path);
        store.Save(new WindowsProtectionState(true, null, DateTimeOffset.UtcNow));
        store.Save(new WindowsProtectionState(false, null, null));

        var loaded = store.Load();
        Assert.IsFalse(loaded.Enabled);
        Assert.IsFalse(File.Exists(_path + ".tmp"));
    }

    [TestMethod]
    public void MalformedStateFileLoadsAsDisabled()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "{ not valid json");

        var state = new ProtectionStateStore(_path).Load();

        Assert.IsFalse(state.Enabled);
    }
}
