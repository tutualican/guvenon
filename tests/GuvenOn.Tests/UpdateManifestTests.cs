using GuvenOn.Core.Update;

namespace GuvenOn.Tests;

[TestClass]
public sealed class UpdateManifestTests
{
    private static UpdateManifest ValidManifest() => new()
    {
        Channel = "stable",
        Version = "0.1.1",
        MinVersion = "0.1.0",
        Url = "https://github.com/tutualican/guvenon/releases/download/v0.1.1/GuvenOnSetup-0.1.1.exe",
        Sha256 = new string('a', 64),
        Size = 78_000_000,
        Rollout = 100,
        Revoked = new List<string>()
    };

    [TestMethod]
    public void ValidManifest_IsAccepted()
    {
        Assert.IsTrue(UpdateChecker.TryValidateManifest(ValidManifest(), out var error), error);
    }

    [TestMethod]
    public void CdnUrl_IsAccepted()
    {
        var manifest = ValidManifest();
        manifest.Url = "https://dl.guvenon.app/win/GuvenOnSetup-0.1.1.exe";
        Assert.IsTrue(UpdateChecker.TryValidateManifest(manifest, out var error), error);
    }

    [TestMethod]
    public void GitHubAssetUrl_IsAccepted()
    {
        var manifest = ValidManifest();
        manifest.Url =
            "https://release-assets.githubusercontent.com/github-production-release-asset/123/GuvenOnSetup-0.1.1.exe";
        Assert.IsTrue(UpdateChecker.TryValidateManifest(manifest, out var error), error);
    }

    [TestMethod]
    [DataRow("http://github.com/tutualican/guvenon/releases/download/v0.1.1/GuvenOnSetup-0.1.1.exe")]
    [DataRow("https://example.com/GuvenOnSetup-0.1.1.exe")]
    [DataRow("https://github.com/tutualican/guvenon/releases/download/v0.1.1/GuvenOnSetup-0.1.1.zip")]
    [DataRow("https://github.com/other/guvenon/releases/download/v0.1.1/GuvenOnSetup-0.1.1.exe")]
    [DataRow("https://dl.guvenon.app/other/GuvenOnSetup-0.1.1.exe")]
    [DataRow("https://raw.githubusercontent.com/attacker/guvenon/main/GuvenOnSetup-0.1.1.exe")]
    [DataRow("https://release-assets.githubusercontent.com.evil.example/GuvenOnSetup-0.1.1.exe")]
    [DataRow("")]
    public void UntrustedUrl_IsRejected(string url)
    {
        var manifest = ValidManifest();
        manifest.Url = url;
        Assert.IsFalse(UpdateChecker.TryValidateManifest(manifest, out _));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("abc")]
    [DataRow("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [DataRow("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void InvalidHash_IsRejected(string hash)
    {
        var manifest = ValidManifest();
        manifest.Sha256 = hash;
        Assert.IsFalse(UpdateChecker.TryValidateManifest(manifest, out _));
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(-1L)]
    [DataRow(3L * 1024 * 1024 * 1024)]
    public void InvalidSize_IsRejected(long size)
    {
        var manifest = ValidManifest();
        manifest.Size = size;
        Assert.IsFalse(UpdateChecker.TryValidateManifest(manifest, out _));
    }

    [TestMethod]
    [DataRow("beta")]
    [DataRow("")]
    public void UnsupportedChannel_IsRejected(string channel)
    {
        var manifest = ValidManifest();
        manifest.Channel = channel;
        Assert.IsFalse(UpdateChecker.TryValidateManifest(manifest, out _));
    }

    [TestMethod]
    public void MinimumVersionAheadOfRelease_IsRejected()
    {
        var manifest = ValidManifest();
        manifest.MinVersion = "9.0.0";
        Assert.IsFalse(UpdateChecker.TryValidateManifest(manifest, out _));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("0.1")]
    [DataRow("01.1.0")]
    [DataRow("1.0.0-01")]
    public void InvalidVersion_IsRejected(string version)
    {
        var manifest = ValidManifest();
        manifest.Version = version;
        Assert.IsFalse(UpdateChecker.TryValidateManifest(manifest, out _));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(101)]
    public void InvalidRollout_IsRejected(int rollout)
    {
        var manifest = ValidManifest();
        manifest.Rollout = rollout;
        Assert.IsFalse(UpdateChecker.TryValidateManifest(manifest, out _));
    }
}
