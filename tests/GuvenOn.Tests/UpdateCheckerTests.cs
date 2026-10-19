using System.Net;
using System.Text;
using System.Text.Json;
using GuvenOn.Core;
using GuvenOn.Core.Update;

namespace GuvenOn.Tests;

[TestClass]
public sealed class UpdateCheckerTests
{
    [TestMethod]
    public async Task CheckAsync_ReturnsNewerManifest()
    {
        var manifest = ValidManifest();
        using var http = new HttpClient(new StubHandler(_ => JsonResponse(manifest)));
        var result = await new UpdateChecker(http).CheckAsync();

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.UpdateAvailable);
        Assert.AreEqual(AppInfo.LatestManifestUrl, result.SourceUrl);
    }

    [TestMethod]
    public async Task CheckAsync_FallsBackToCdn()
    {
        var manifest = ValidManifest();
        var calls = 0;
        using var http = new HttpClient(new StubHandler(_ =>
            ++calls == 1
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : JsonResponse(manifest)));

        var result = await new UpdateChecker(http).CheckAsync();

        Assert.IsTrue(result.Success);
        Assert.AreEqual(AppInfo.FallbackManifestUrl, result.SourceUrl);
    }

    [TestMethod]
    public async Task CheckAsync_RejectsInvalidManifest()
    {
        var manifest = ValidManifest();
        manifest.Sha256 = "not-a-hash";
        using var http = new HttpClient(new StubHandler(_ => JsonResponse(manifest)));
        var result = await new UpdateChecker(http).CheckAsync();

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.Error);
    }

    [TestMethod]
    public async Task CheckAsync_RejectsOversizedManifestWithoutContentLength()
    {
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnknownLengthContent(new string(' ', 300 * 1024))
        }));

        var result = await new UpdateChecker(http).CheckAsync();

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.Error);
    }

    [TestMethod]
    public async Task CheckAsync_HonorsRolloutZero()
    {
        var manifest = ValidManifest();
        manifest.Rollout = 0;
        using var http = new HttpClient(new StubHandler(_ => JsonResponse(manifest)));
        var result = await new UpdateChecker(http).CheckAsync();

        Assert.IsTrue(result.Success);
        Assert.IsFalse(result.UpdateAvailable);
    }

    [TestMethod]
    public void TrustedDownloadUrl_MatchesManifestRules()
    {
        Assert.IsTrue(UpdateChecker.IsTrustedDownloadUrl(
            "https://github.com/tutualican/guvenon/releases/download/v0.1.2/GuvenOnSetup-0.1.2.exe"));
        Assert.IsFalse(UpdateChecker.IsTrustedDownloadUrl(
            "https://github.com/tutualican/guvenon/releases/download/v0.1.2/GuvenOnSetup-0.1.2.zip"));
        Assert.IsFalse(UpdateChecker.IsTrustedDownloadUrl(
            "https://github.com/other/guvenon/releases/download/v0.1.2/GuvenOnSetup-0.1.2.exe"));
    }

    private static UpdateManifest ValidManifest() => new()
    {
        Channel = "stable",
        Version = NextVersion(),
        MinVersion = "0.1.0",
        Url = $"https://github.com/tutualican/guvenon/releases/download/v{NextVersion()}/GuvenOnSetup-{NextVersion()}.exe",
        Sha256 = new string('b', 64),
        Size = 78_000_000,
        Rollout = 100
    };

    private static string NextVersion() =>
        $"{AppInfo.Version.Major}.{AppInfo.Version.Minor}.{AppInfo.Version.Patch + 1}";

    private static HttpResponseMessage JsonResponse(UpdateManifest manifest) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(manifest),
            Encoding.UTF8,
            "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class UnknownLengthContent(string body) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(Encoding.UTF8.GetBytes(body)).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
