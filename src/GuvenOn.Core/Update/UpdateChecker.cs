using System.Net.Http;
using System.Text.Json;

namespace GuvenOn.Core.Update;

public sealed class UpdateChecker
{
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

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(
                    stream, JsonOptions, cancellationToken);

                if (manifest is null || !SemVer.TryParse(manifest.Version, out var latest))
                {
                    lastError = new InvalidDataException("The update manifest is invalid.");
                    continue;
                }

                var current = AppInfo.Version;
                var available = latest > current;

                if (available && manifest.Revoked?.Contains(AppInfo.VersionString, StringComparer.OrdinalIgnoreCase) == true)
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
}

public sealed record UpdateCheckResult(
    bool Success,
    bool UpdateAvailable,
    UpdateManifest? Manifest,
    string? SourceUrl,
    Exception? Error);
