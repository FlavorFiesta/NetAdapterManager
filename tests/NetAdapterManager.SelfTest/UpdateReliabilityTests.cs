using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using NetAdapterManager.Services;

internal static class UpdateReliabilityTests
{
    private const string Feed = "https://updates.example.com/latest.json";
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static HttpResponseMessage Release(string url = UpdateService.ReleasesUrl + "/tag/v1.9.0") => new(HttpStatusCode.OK)
    {
        Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
        { tag_name = "v1.9.0", html_url = url, draft = false, prerelease = false, body = "Notes" }), Encoding.UTF8, "application/json")
    };
    internal static async Task RunAsync(Action<string, string> pass, Action<string, string> fail)
    {
        async Task Check(string name, Func<Task> test)
        {
            try { await test(); pass(name, "Simulated HTTP and isolated settings; no network changes."); }
            catch (Exception ex) { fail(name, ex.ToString()); }
        }
        await Check("旧设置默认备用源和手动禁用", () =>
        {
            var old = System.Text.Json.JsonSerializer.Deserialize<AppPreferences>("{\"Language\":\"en\"}")!;
            Require(PreferencesService.Normalize(old).UpdateFallbackUrl == UpdateService.DefaultFallbackUrl, "Missing default.");
            var disabled = System.Text.Json.JsonSerializer.Deserialize<AppPreferences>("{\"UpdateFallbackUrl\":\"\"}")!;
            Require(PreferencesService.Normalize(disabled).UpdateFallbackUrl == "", "Explicit disable lost.");
            return Task.CompletedTask;
        });
        foreach (var (error, status) in new[] {
            (new HttpRequestException("TLS", new AuthenticationException("chain")), "CertificateError"),
            (new HttpRequestException(HttpRequestError.SecureConnectionError), "CertificateError"),
            (new HttpRequestException(HttpRequestError.NameResolutionError), "DnsError"),
            (new HttpRequestException(HttpRequestError.ProxyTunnelError), "ProxyError"),
            (new HttpRequestException(HttpRequestError.ConnectionError), "Offline") })
            await Check("更新错误分类-" + status, async () =>
            {
                using var client = new HttpClient(new Version161Tests.Handler((_, _) => throw error));
                Require((await new UpdateService(client).CheckAsync(new(1, 6, 2))).Status == status, "Wrong error classification.");
            });
        await Check("主源成功不访问备用源", async () =>
        {
            var count = 0;
            using var client = new HttpClient(new Version161Tests.Handler((request, _) =>
            { count++; Require(request.RequestUri!.Host == "api.github.com", "Unexpected fallback."); return Task.FromResult(Release()); }));
            var result = await new UpdateService(client, Feed).CheckAsync(new(1, 6, 2));
            Require(count == 1 && result.Status == "Available" && !result.UsedFallback, "Wrong primary result.");
        });
        foreach (var cause in new[] { "tls", "timeout", "limited", "server" })
            await Check("失败自动切换备用源-" + cause, async () =>
            {
                var count = 0;
                using var client = new HttpClient(new Version161Tests.Handler((request, _) =>
                {
                    count++;
                    if (request.RequestUri!.Host == "api.github.com") return cause switch
                    {
                        "tls" => throw new HttpRequestException("TLS", new AuthenticationException()),
                        "timeout" => throw new TaskCanceledException(),
                        "limited" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)),
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
                    };
                    Require(request.RequestUri.AbsoluteUri == Feed && !request.Headers.Contains("X-GitHub-Api-Version")
                        && request.Headers.Authorization is null, "Unexpected fallback request.");
                    return Task.FromResult(Release());
                }));
                var result = await new UpdateService(client, Feed).CheckAsync(new(1, 6, 2));
                Require(count == 2 && result.Status == "Available" && result.UsedFallback, "Fallback failed.");
            });
        await Check("备用源拒绝外站下载链接", async () =>
        {
            using var client = new HttpClient(new Version161Tests.Handler((request, _) => Task.FromResult(
                request.RequestUri!.Host == "api.github.com" ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Release("https://evil.example/setup.exe"))));
            var result = await new UpdateService(client, Feed).CheckAsync(new(1, 6, 2));
            Require(result.Status == "Unavailable" && result.Url is null && !result.UsedFallback, "Untrusted download accepted.");
        });
        await Check("取消不继续访问备用源", async () =>
        {
            var count = 0;
            using var cancellation = new CancellationTokenSource();
            using var client = new HttpClient(new Version161Tests.Handler((_, _) => { count++; cancellation.Cancel(); throw new TaskCanceledException(); }));
            var result = await new UpdateService(client, Feed).CheckAsync(new(1, 6, 2), cancellation.Token);
            Require(count == 1 && result.Status == "Cancelled", "Cancellation started a fallback.");
        });
        await Check("取消备用源保留取消状态", async () =>
        {
            using var cancellation = new CancellationTokenSource();
            using var client = new HttpClient(new Version161Tests.Handler((request, _) =>
            {
                if (request.RequestUri!.Host == "api.github.com") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                cancellation.Cancel(); throw new TaskCanceledException();
            }));
            Require((await new UpdateService(client, Feed).CheckAsync(new(1, 6, 2), cancellation.Token)).Status == "Cancelled", "Fallback cancellation lost.");
        });
        await Check("备用源地址校验和持久化", async () =>
        {
            foreach (var url in new[] { "http://updates.example.com/latest.json", "https://user:pass@updates.example.com/latest.json", "https://localhost/latest.json", "https://127.0.0.1/latest.json", Feed + "?token=secret", Feed + "#fragment", "https://updates.example.com:8443/latest.json" })
            {
                Require(!UpdateService.ValidFeedUrl(url), "Unsafe feed accepted: " + url);
                var rejected = false; try { _ = new UpdateService(fallbackUrl: url); } catch (ArgumentException) { rejected = true; }
                Require(rejected, "Constructor accepted invalid feed.");
            }
            var path = Path.Combine(AppContext.BaseDirectory, "update-settings-" + Guid.NewGuid() + ".json");
            try
            {
                var preferences = new PreferencesService(path);
                await preferences.SaveAsync(new AppPreferences { Language = "en", Theme = "dark", UpdateFallbackUrl = Feed });
                var reloaded = new PreferencesService(path); reloaded.Load();
                Require(reloaded.Current.UpdateFallbackUrl == Feed && reloaded.Current.Theme == "dark", "Feed settings lost.");
                Require(PreferencesService.Normalize(new AppPreferences { UpdateFallbackUrl = "http://invalid.example" }).UpdateFallbackUrl == "", "Invalid persisted URL accepted.");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
    }
}
