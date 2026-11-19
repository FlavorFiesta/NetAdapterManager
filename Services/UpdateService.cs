using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text.Json;

namespace NetAdapterManager.Services;

public sealed record UpdateResult(string Status, string? LatestVersion = null, string? Summary = null, string? Url = null, bool UsedFallback = false);
public sealed class UpdateService
{
    public const string ReleasesUrl = "https://github.com/FlavorFiesta/NetAdapterManager/releases";
    public const string DefaultFallbackUrl = "https://netadaptermanager-updates.482822012.workers.dev/latest.json";
    private const string ApiUrl = "https://api.github.com/repos/FlavorFiesta/NetAdapterManager/releases/latest";
    private readonly HttpClient _client;
    private readonly string? _fallbackUrl;
    private static readonly HttpClient DefaultClient = new(new HttpClientHandler { AllowAutoRedirect = false });
    public UpdateService(HttpClient? client = null, string? fallbackUrl = null)
    {
        _client = client ?? DefaultClient;
        if (!string.IsNullOrWhiteSpace(fallbackUrl) && !ValidFeedUrl(fallbackUrl))
            throw new ArgumentException("The update feed must be a public HTTPS URL without credentials, query or fragment.", nameof(fallbackUrl));
        _fallbackUrl = string.IsNullOrWhiteSpace(fallbackUrl) ? null : fallbackUrl;
    }
    internal static bool ValidFeedUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0
        && uri.Query.Length == 0 && uri.Fragment.Length == 0 && !uri.IsLoopback
        && uri.HostNameType == UriHostNameType.Dns && uri.Host.Contains('.') && !uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
    internal static bool TryVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrEmpty(text)) return false;
        var value = text.StartsWith('v') ? text[1..] : text;
        return value.Split('.').Length == 3 && value.All(c => char.IsAsciiDigit(c) || c == '.') && Version.TryParse(value, out version!);
    }
    internal static bool TrustedUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host == "github.com" && uri.IsDefaultPort && uri.UserInfo.Length == 0
        && uri.Query.Length == 0 && uri.Fragment.Length == 0
        && uri.AbsolutePath.StartsWith("/FlavorFiesta/NetAdapterManager/releases/tag/", StringComparison.Ordinal)
        && !uri.AbsolutePath.Contains('%');
    public async Task<UpdateResult> CheckAsync(Version current, CancellationToken token = default)
    {
        var primary = await CheckSourceAsync(ApiUrl, current, token);
        if (_fallbackUrl is null || token.IsCancellationRequested || primary.Status is not
            ("Offline" or "CertificateError" or "DnsError" or "ProxyError" or "Timeout" or "RateLimited" or "Unavailable"))
            return primary;
        AppLogger.Info("Primary update source failed: " + primary.Status + "; trying configured fallback.");
        var fallback = await CheckSourceAsync(_fallbackUrl, current, token);
        if (fallback.Status is "Available" or "Current" or "LocalAhead") return fallback with { UsedFallback = true };
        if (fallback.Status == "Cancelled") return fallback;
        AppLogger.Info("Fallback update source failed: " + fallback.Status);
        return primary;
    }
    private async Task<UpdateResult> CheckSourceAsync(string endpoint, Version current, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.UserAgent.ParseAdd("NetAdapterManager/" + current.ToString(3));
            request.Headers.Accept.ParseAdd("application/json");
            if (endpoint == ApiUrl) request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            AppLogger.Info($"Update source {new Uri(endpoint).Host}: HTTP {(int)response.StatusCode}");
            if (response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired) return new("ProxyError");
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests) return new("RateLimited");
            if (response.StatusCode == HttpStatusCode.NotFound) return new("NoRelease");
            if (!response.IsSuccessStatusCode) return new("Unavailable");
            if (response.Content.Headers.ContentLength > 256 * 1024) return new("Invalid");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192]; int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) != 0)
            {
                if (buffer.Length + read > 256 * 1024) return new("Invalid");
                buffer.Write(chunk, 0, read);
            }
            using var document = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 32 });
            var root = document.RootElement;
            if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return new("NoRelease");
            var tag = root.GetProperty("tag_name").GetString();
            var url = root.GetProperty("html_url").GetString();
            if (!TryVersion(tag, out var latest) || !TrustedUrl(url) || url != ReleasesUrl + "/tag/" + tag) return new("Invalid");
            var summary = root.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String ? body.GetString() ?? "" : "";
            summary = summary[..Math.Min(summary.Length, 12000)];
            var comparable = new Version(current.Major, current.Minor, Math.Max(0, current.Build));
            return new(latest > comparable ? "Available" : latest == comparable ? "Current" : "LocalAhead", latest.ToString(3), summary, url);
        }
        catch (OperationCanceledException)
        {
            var status = token.IsCancellationRequested ? "Cancelled" : "Timeout";
            AppLogger.Info($"Update source {new Uri(endpoint).Host}: {status}");
            return new(status);
        }
        catch (HttpRequestException ex)
        {
            AppLogger.Error("Update request failed: " + new Uri(endpoint).Host, ex);
            return new(ClassifyNetworkError(ex));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { AppLogger.Error("Invalid update response", ex); return new("Invalid"); }
        catch (IOException ex) { AppLogger.Error("Update response could not be read", ex); return new("Offline"); }
    }
    internal static string ClassifyNetworkError(HttpRequestException error)
    {
        for (Exception? cause = error; cause is not null; cause = cause.InnerException)
            if (cause is AuthenticationException) return "CertificateError";
        return error.HttpRequestError switch
        {
            HttpRequestError.SecureConnectionError => "CertificateError",
            HttpRequestError.NameResolutionError => "DnsError",
            HttpRequestError.ProxyTunnelError => "ProxyError",
            _ => "Offline"
        };
    }
}
