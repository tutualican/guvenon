using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GuvenOn.Core.Update;

public sealed class UpdateChecker
{
    private const int MaxManifestBytes = 256 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public UpdateChecker(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        if (!_http.DefaultRequestHeaders.UserAgent.TryParseAdd($"Guven-ON/{AppInfo.VersionString}"))
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Guven-ON");
        }
    }

    /// <summary>
    /// Checks the latest release manifest. GitHub's
    /// /releases/latest/download/ path is used instead of the REST API, so the
    /// check costs no rate-limit budget and needs no token.
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var sources = new[] { AppInfo.LatestManifestUrl, AppInfo.FallbackManifestUrl };
        Exception? lastError = null;

        foreach (var source in sources)
        {
            try
            {
                using var response = await _http.GetAsync(
                    source, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    lastError = new HttpRequestException($"HTTP {(int)response.StatusCode} from {source}");
                    continue;
                }

                if (response.Content.Headers.ContentLength is > MaxManifestBytes)
                {
                    lastError = new InvalidDataException("The update manifest is too large.");
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var payload = await ReadLimitedAsync(stream, MaxManifestBytes, cancellationToken);
                var manifest = JsonSerializer.Deserialize<UpdateManifest>(payload, JsonOptions);

                string? validationError = null;
                if (manifest is null || !TryValidateManifest(manifest, out validationError))
                {
                    lastError = new InvalidDataException(validationError ?? "The update manifest is invalid.");
                    continue;
                }

                if (!SemVer.TryParse(manifest.Version, out var latest))
                {
                    lastError = new InvalidDataException("The update manifest has an invalid version.");
                    continue;
                }

                var current = AppInfo.Version;
                var available = latest > current && IsInRollout(manifest.Rollout);

                if (available && manifest.Revoked?.Contains(
                        AppInfo.VersionString, StringComparer.OrdinalIgnoreCase) == true)
                {
                    available = false;
                }

                return new UpdateCheckResult(true, available, manifest, source, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                lastError = exception;
            }
        }

        return new UpdateCheckResult(false, false, null, null, lastError);
    }

    /// <summary>
    /// Validates every field the app relies on before a download URL can reach
    /// the UI. The manifest is fetched over TLS from a fixed host, but a bad
    /// release must still fail closed instead of offering an arbitrary link.
    /// </summary>
    public static bool TryValidateManifest(UpdateManifest manifest, out string? error)
    {
        error = null;

        if (!string.Equals(manifest.Channel, AppInfo.DefaultChannel, StringComparison.OrdinalIgnoreCase))
        {
            error = "The update manifest has an unsupported channel.";
            return false;
        }

        if (!SemVer.TryParse(manifest.Version, out var latest))
        {
            error = "The update manifest has an invalid version.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(manifest.MinVersion))
        {
            if (!SemVer.TryParse(manifest.MinVersion, out var minimum) || minimum > latest)
            {
                error = "The update manifest has an invalid minimum version.";
                return false;
            }
        }

        if (!Uri.TryCreate(manifest.Url, UriKind.Absolute, out var download) ||
            !IsTrustedDownloadUri(download))
        {
            error = "The update manifest has an untrusted download URL.";
            return false;
        }

        if (!IsSha256(manifest.Sha256))
        {
            error = "The update manifest has an invalid SHA-256 value.";
            return false;
        }

        if (manifest.Size <= 0 || manifest.Size > 2L * 1024 * 1024 * 1024)
        {
            error = "The update manifest has an invalid file size.";
            return false;
        }

        if (manifest.Rollout is < 0 or > 100)
        {
            error = "The update manifest has an invalid rollout value.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns true only for HTTPS installer links on the release hosts the
    /// product actually uses. Kept public so the UI can apply the same rule
    /// before opening a link.
    /// </summary>
    public static bool IsTrustedDownloadUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && IsTrustedDownloadUri(uri);
    }

    private static bool IsTrustedDownloadUri(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        if (!uri.AbsolutePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsolutePath.StartsWith(
                $"/{AppInfo.GitHubOwner}/{AppInfo.GitHubRepo}/releases/download/",
                StringComparison.OrdinalIgnoreCase);
        }

        if (uri.Host.Equals("dl.guvenon.app", StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsolutePath.StartsWith("/win/", StringComparison.OrdinalIgnoreCase);
        }

        // GitHub redirects release downloads to its own asset hosts. Manifests
        // should normally use the github.com release link, but direct asset
        // links are accepted only on GitHub's known release hosts.
        return uri.Host.Equals("release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSha256(string? value)
    {
        if (value is not { Length: 64 })
        {
            return false;
        }

        foreach (var character in value)
        {
            var isHex =
                (character is >= '0' and <= '9') ||
                (character is >= 'a' and <= 'f') ||
                (character is >= 'A' and <= 'F');
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<byte[]> ReadLimitedAsync(
        Stream stream,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];

        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maxBytes)
            {
                throw new InvalidDataException("The update manifest is too large.");
            }

            buffer.Write(chunk, 0, read);
        }
    }

    private static bool IsInRollout(int rollout)
    {
        if (rollout >= 100)
        {
            return true;
        }

        if (rollout <= 0)
        {
            return false;
        }

        // Stable per-machine bucket so a staged rollout does not flip on every check.
        var input = Encoding.UTF8.GetBytes($"{AppInfo.ProductName}|{Environment.MachineName}");
        var hash = SHA256.HashData(input);
        var bucket = BitConverter.ToUInt32(hash, 0) % 100;
        return bucket < rollout;
    }
}

public sealed record UpdateCheckResult(
    bool Success,
    bool UpdateAvailable,
    UpdateManifest? Manifest,
    string? SourceUrl,
    Exception? Error);
