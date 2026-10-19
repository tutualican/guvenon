using GuvenOn.Core.Settings;

namespace GuvenOn.Tests;

[TestClass]
public sealed class SettingsStoreTests
{
    private string _directory = null!;
    private string _path = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), "guvenon-tests", Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_directory, "settings.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // Best effort.
        }
    }

    [TestMethod]
    public void MissingFile_ReturnsDefaults()
    {
        var settings = new SettingsStore(_path).Load();
        Assert.AreEqual("auto", settings.Language);
        Assert.IsFalse(settings.StartWithWindows);
    }

    [TestMethod]
    public void SaveAndLoad_RoundTrips()
    {
        var store = new SettingsStore(_path);
        store.Save(new AppSettings
        {
            Language = "tr",
            StartWithWindows = true,
            LastUpdateCheck = DateTimeOffset.Parse("2026-01-02T03:04:05Z")
        });

        var loaded = store.Load();
        Assert.AreEqual("tr", loaded.Language);
        Assert.IsTrue(loaded.StartWithWindows);
        Assert.AreEqual(DateTimeOffset.Parse("2026-01-02T03:04:05Z"), loaded.LastUpdateCheck);
    }

    [TestMethod]
    public void CorruptFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "{ not json");
        var settings = new SettingsStore(_path).Load();
        Assert.AreEqual("auto", settings.Language);
    }

    [TestMethod]
    public void Save_CreatesDirectoryAndReplacesFile()
    {
        var store = new SettingsStore(_path);
        store.Save(new AppSettings { Language = "de" });
        store.Save(new AppSettings { Language = "en" });

        Assert.IsTrue(File.Exists(_path));
        Assert.IsFalse(File.Exists(_path + ".tmp"));
        Assert.AreEqual("en", store.Load().Language);
    }
}
