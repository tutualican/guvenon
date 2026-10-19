using GuvenOn.Core;

namespace GuvenOn.Tests;

[TestClass]
public sealed class AutostartTests
{
    private string _directory = null!;
    private string _executable = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), "guvenon-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _executable = Path.Combine(_directory, "guvenon.exe");
        File.WriteAllBytes(_executable, [0]);
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
    public void QuotedCurrentCommand_IsRecognized()
    {
        Assert.IsTrue(Autostart.IsLaunchCommand($"\"{_executable}\" --tray"));
    }

    [TestMethod]
    public void UnquotedCurrentCommand_IsRecognized()
    {
        Assert.IsTrue(Autostart.IsLaunchCommand($"{_executable} --tray"));
    }

    [TestMethod]
    public void CommandWithoutTrayFlag_IsRejected()
    {
        Assert.IsFalse(Autostart.IsLaunchCommand($"\"{_executable}\""));
        Assert.IsFalse(Autostart.IsLaunchCommand($"\"{_executable}\" --other"));
    }

    [TestMethod]
    public void MissingExecutable_IsRejected()
    {
        var missing = Path.Combine(_directory, "missing", "guvenon.exe");
        Assert.IsFalse(Autostart.IsLaunchCommand($"\"{missing}\" --tray"));
    }

    [TestMethod]
    public void WrongExecutableName_IsRejected()
    {
        var other = Path.Combine(_directory, "other.exe");
        File.WriteAllBytes(other, [0]);
        Assert.IsFalse(Autostart.IsLaunchCommand($"\"{other}\" --tray"));
    }

    [TestMethod]
    public void RelativeExecutable_IsRejected()
    {
        Assert.IsFalse(Autostart.IsLaunchCommand("\"guvenon.exe\" --tray"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("\"")]
    [DataRow("--tray")]
    public void MalformedCommand_IsRejected(string command)
    {
        Assert.IsFalse(Autostart.IsLaunchCommand(command));
    }
}
