using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using NetAdapterManager.Models;
using NetAdapterManager.Services;
using NetAdapterManager.ViewModels;

internal static class Version161Tests
{
    internal sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
    }
    internal static HttpClient ReleaseClient() => new(new Handler((request, _) =>
        Task.FromResult(Response("v1.9.0"))));
    private static HttpResponseMessage Response(string tag = "v1.6.1", bool prerelease = false, bool draft = false, string? url = null) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { tag_name = tag, prerelease, draft,
            html_url = url ?? UpdateService.ReleasesUrl + "/tag/" + tag, body = "Release notes\n<script>plain text</script>" }), Encoding.UTF8, "application/json")
    };
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    internal static async Task RunAsync(Action<string, string> pass, Action<string, string> fail)
    {
        async Task Check(string name, Func<Task> test)
        {
            try { await test(); pass(name, "v1.6.1 isolated files / simulated HTTP; no real network writes."); }
            catch (Exception ex) { fail(name, ex.ToString()); }
        }
        OperationRecord Record() => new() { AdapterName = "SECRET ADAPTER", InterfaceGuid = "SECRET-GUID", MacAddress = "AA-BB-CC-DD-EE-FF",
            ErrorDetail = "SECRET ADAPTER AA-BB-CC-DD-EE-FF 192.0.2.123 C:\\private\\file", Message = "192.0.2.123",
            Before = NetworkConfig.ForPreset("192.0.2.123", 24, null, DnsMode.Static, ["192.0.2.53"]), RolledBack = true, Verified = true };
        await Check("诊断默认隐藏所有身份及自由文本", () =>
        {
            var text = DiagnosticExport.Create([Record()], new());
            foreach (var value in new[] { "SECRET", "AA-BB", "192.0.2", "private" }) Require(!text.Contains(value), "Sensitive marker leaked: " + value);
            using var json = JsonDocument.Parse(text);
            Require(json.RootElement.GetProperty("Operations")[0].GetProperty("RolledBack").GetBoolean(), "Rollback result omitted.");
            return Task.CompletedTask;
        });
        await Check("诊断隐藏选项独立且原记录不变", () =>
        {
            var r = Record();
            var nameHidden = DiagnosticExport.Create([r], new(true, false, false));
            Require(!nameHidden.Contains("SECRET") && nameHidden.Contains("AA-BB") && nameHidden.Contains("192.0.2.123") && !nameHidden.Contains("private"), "Identity policy not isolated.");
            var macHidden = DiagnosticExport.Create([r], new(false, true, false));
            Require(macHidden.Contains("SECRET") && !macHidden.Contains("AA-BB") && macHidden.Contains("192.0.2.123"), "MAC policy not isolated.");
            var ipHidden = DiagnosticExport.Create([r], new(false, false, true));
            Require(ipHidden.Contains("SECRET") && ipHidden.Contains("AA-BB") && !ipHidden.Contains("192.0.2"), "IP policy not isolated.");
            var raw = DiagnosticExport.Create([r], new(false, false, false));
            Require(raw.Contains("private") && raw.Contains("SECRET") && r.AdapterName == "SECRET ADAPTER", "Unredacted export or source altered.");
            return Task.CompletedTask;
        });
        await Check("操作记录数量大小上限与恢复备份", async () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "journal-" + Guid.NewGuid() + ".json");
            var initial = Enumerable.Range(0, OperationJournal.MaximumEntries).Select(i => new OperationRecord { Message = i.ToString() }).ToList();
            await new JsonStore<List<OperationRecord>>(path).SaveAsync(initial);
            var journal = new OperationJournal(path); var r = Record(); r.ErrorDetail = new string('x', 40000);
            await journal.AppendAsync(r);
            var records = await journal.ReadAsync();
            Require(records.Count == OperationJournal.MaximumEntries && records[0].AdapterName == "SECRET ADAPTER" && records[^1].Message == "198", "Retention order incorrect.");
            Require(records[0].ErrorDetail.Length == 8192 && new FileInfo(path).Length <= OperationJournal.MaximumBytes && File.Exists(path + ".bak"), "Journal bounds/backup missing.");
        });
        await Check("诊断隐藏跨字段标识及非结构化驱动数据", () =>
        {
            var r = Record(); r.AdapterName = "Office 192.0.2.123 AA-BB-CC-DD-EE-FF"; r.InterfaceGuid = "safe-guid";
            r.Before!.IPv4Addresses[0].PrefixOrigin = "SECRET ADAPTER";
            r.Kind = "SECRET ADAPTER";
            var exported = DiagnosticExport.Create([r], new(false, true, true));
            Require(!exported.Contains("192.0.2.123") && !exported.Contains("AA-BB") && !exported.Contains("SECRET"), "Identifiers leaked across fields.");
            var noIdentity = DiagnosticExport.Create([r], new(true, false, false));
            Require(!noIdentity.Contains("SECRET ADAPTER"), "Unstructured driver data leaked.");
            return Task.CompletedTask;
        });
        await Check("记录损坏时保存失败不替换原文件", async () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "broken-journal-" + Guid.NewGuid() + ".json");
            await File.WriteAllTextAsync(path, "{broken");
            var rejected = false; try { await new OperationJournal(path).AppendAsync(Record()); } catch (IOException) { rejected = true; }
            Require(rejected && await File.ReadAllTextAsync(path) == "{broken", "Corrupt history replaced.");
        });
        await Check("复杂操作记录按字节上限淘汰旧项", async () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "large-journal-" + Guid.NewGuid() + ".json");
            var entry = Record(); entry.Message = new string('m', 8192); entry.ErrorDetail = new string('e', 8192); entry.Warnings = new string('w', 8192);
            var records = new List<OperationRecord>();
            while (true)
            {
                records.Add(entry);
                if (JsonSerializer.SerializeToUtf8Bytes(records, OperationJournal.Options).Length <= OperationJournal.MaximumBytes) continue;
                records.RemoveAt(records.Count - 1); break;
            }
            Require(records.Count < OperationJournal.MaximumEntries, "Fixture must exercise byte retention, not count retention.");
            var originalCount = records.Count;
            await new JsonStore<List<OperationRecord>>(path).SaveAsync(records);
            var journal = new OperationJournal(path); await journal.AppendAsync(entry);
            Require((await journal.ReadAsync()).Count <= originalCount && new FileInfo(path).Length <= OperationJournal.MaximumBytes, "Byte retention failed.");
        });
        await Check("记录失败不能改变已复核网络结果", async () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "failed-journal-" + Guid.NewGuid() + ".json");
            await File.WriteAllTextAsync(path, "broken");
            var config = NetworkConfig.ForPreset("192.0.2.1", 24, null, DnsMode.Static, []);
            AdapterSnapshot Read() => new() { InterfaceGuid = "test", Config = config.Clone() };
            var network = new NetworkService(_ => Task.FromResult<IReadOnlyList<AdapterSnapshot>>([Read()]), (_, target, _) => config = target.Clone(),
                (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            var vm = new MainViewModel(network, new PresetService(path + ".presets"), new OperationJournal(path)) { ReviewConfiguration = (_, _) => true };
            await vm.InitializeAsync(); vm.IPv4Address = "192.0.2.2"; vm.ApplyCommand.Execute(null); await vm.ActiveOperation;
            Require(config.ManagedIPv4Address == "192.0.2.2" && vm.WarningMessage.Contains(Localization.T("History.SaveFailed")), "History failure changed operation or was hidden.");
        });
        await Check("应用记录包含前后状态目标与复核", async () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "success-journal-" + Guid.NewGuid() + ".json"); var journal = new OperationJournal(path);
            var state = NetworkConfig.ForPreset("192.0.2.1", 24, null, DnsMode.Static, []);
            AdapterSnapshot Read() => new() { InterfaceGuid = "test", Name = "Demo", Config = state.Clone() };
            var network = new NetworkService(_ => Task.FromResult<IReadOnlyList<AdapterSnapshot>>([Read()]), (_, target, _) => state = target.Clone(),
                (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            var vm = new MainViewModel(network, new PresetService(path + ".presets"), journal) { ReviewConfiguration = (_, _) => true };
            await vm.InitializeAsync(); vm.IPv4Address = "192.0.2.2"; vm.ApplyCommand.Execute(null); await vm.ActiveOperation;
            var r = (await journal.ReadAsync()).Single();
            Require(r.Succeeded && r.Verified == true && r.Before!.ManagedIPv4Address == "192.0.2.1" && r.Target!.ManagedIPv4Address == "192.0.2.2"
                && r.Actual!.ManagedIPv4Address == "192.0.2.2", "Operation evidence missing.");
        });
        await Check("应用文本日志单条大小受限", () =>
        { Require(AppLogger.BoundEntry(new string('x', 1000000)).Length < 17000, "Unbounded native error log."); return Task.CompletedTask; });
        foreach (var (tag, expected) in new[] { ("v1.7.0", "Available"), ("v1.6.1", "Current"), ("v1.5.2", "LocalAhead") })
        await Check("更新语义版本比较-" + expected, async () =>
        {
            var count = 0;
            using var client = new HttpClient(new Handler((request, _) =>
            {
                count++; Require(request.RequestUri!.Host == "api.github.com" && request.Headers.Authorization is null && request.Headers.UserAgent.Count > 0, "Unexpected endpoint or credentials.");
                return Task.FromResult(Response(tag));
            }));
            var result = await new UpdateService(client).CheckAsync(new(1, 6, 1, 0));
            Require(count == 1 && result.Status == expected && result.Url!.StartsWith(UpdateService.ReleasesUrl), "Wrong stable release result.");
        });
        foreach (var code in new[] { 403, 429, 404, 500 })
        await Check("更新HTTP状态-" + code, async () =>
        {
            using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)code))));
            var r = await new UpdateService(client).CheckAsync(new(1, 6, 1));
            Require(r.Status == (code is 403 or 429 ? "RateLimited" : code == 404 ? "NoRelease" : "Unavailable") && r.Url is null, "HTTP failure offered a download.");
        });
        foreach (var scenario in new[] { "draft", "pre", "suffix", "host", "tag", "oversize", "json" })
        await Check("更新拒绝异常响应-" + scenario, async () =>
        {
            using var client = new HttpClient(new Handler((_, _) => Task.FromResult(scenario switch
            {
                "draft" => Response(draft: true), "pre" => Response(prerelease: true), "suffix" => Response("v2.0.0-rc1"),
                "host" => Response(url: "https://evil.example/download"), "tag" => Response(url: UpdateService.ReleasesUrl + "/tag/v99.0.0"),
                "oversize" => new(HttpStatusCode.OK) { Content = new StringContent(new string('x', 256 * 1024 + 1)) },
                _ => new(HttpStatusCode.OK) { Content = new StringContent("{}") }
            })));
            var r = await new UpdateService(client).CheckAsync(new(1, 6, 1));
            Require(r.Url is null && r.Status is "Invalid" or "NoRelease", "Invalid/prerelease offered a download.");
        });
        foreach (var kind in new[] { "offline", "timeout", "cancel" })
        await Check("更新网络错误-" + kind, async () =>
        {
            using var client = new HttpClient(new Handler((_, _) => kind == "offline" ? throw new HttpRequestException() : throw new TaskCanceledException()));
            using var cancellation = new CancellationTokenSource(); if (kind == "cancel") cancellation.Cancel();
            var r = await new UpdateService(client).CheckAsync(new(1, 6, 1), cancellation.Token);
            Require(r.Status == (kind == "offline" ? "Offline" : kind == "timeout" ? "Timeout" : "Cancelled"), "Network failure misreported.");
        });
    }
}
