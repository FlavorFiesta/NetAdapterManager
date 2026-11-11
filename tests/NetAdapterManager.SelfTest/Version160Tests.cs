using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetAdapterManager.Models;
using NetAdapterManager.Services;
using NetAdapterManager.ViewModels;

internal static class Version160Tests
{
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    internal static async Task RunAsync(Action<string, string> pass, Action<string, string> fail)
    {
        async Task Check(string name, Func<Task> test)
        {
            try { await test(); pass(name, "v1.6.0 isolated; no real network writes."); }
            catch (Exception ex) { fail(name, ex.ToString()); }
        }
        static AdapterSnapshot Snapshot(string guid = "adapter-a") => new() { InterfaceGuid = guid, InterfaceIndex = 7,
            Name = "Ethernet demo", Enabled = true, Ipv4InterfacePresent = true,
            Config = NetworkConfig.ForPreset("192.0.2.10", 24, "192.0.2.1", DnsMode.Static, ["192.0.2.53"]) };
        await Check("失效管理记录替换当前主地址且连续修改不追加", async () =>
        {
            var state = Snapshot(); state.Config.ManagedIPv4Address = "203.0.113.99";
            state.Config.IPv4Addresses.Add(new() { Address = "198.51.100.10", PrefixLength = 24 });
            var target = NetworkConfig.ForPreset("192.0.2.20", 24, null, DnsMode.Static, []);
            var rows = ConfigurationPreview.Create(state, target);
            Require(!rows[1].Target.Contains("192.0.2.10/24") && rows[1].Target.Contains("198.51.100.10/24"), "Preview appended old primary.");
            var network = new NetworkService(_ => Task.FromResult<IReadOnlyList<AdapterSnapshot>>([state]),
                (before, next, exact) => state.Config = NetworkService.BuildExpectedConfiguration(before, next, exact),
                (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            Require((await network.ApplyAsync(state, target, OperationSource.Manual)).Succeeded, "First change failed.");
            target.ManagedIPv4Address = target.IPv4Addresses[0].Address = "192.0.2.30";
            Require((await network.ApplyAsync(state, target, OperationSource.Manual)).Succeeded, "Second change failed.");
            Require(state.Config.IPv4Addresses.Select(a => a.Address).SequenceEqual(new[] { "192.0.2.30", "198.51.100.10" }), "Repeated edits accumulated addresses.");
        });
        await Check("状态区分禁用未连接及已连接并约束DNS", () =>
        {
            var snapshot = Snapshot(); snapshot.ConnectionStatus = "Disconnected";
            var disconnected = new AdapterViewModel(snapshot).EnabledText;
            snapshot.ConnectionStatus = "Up"; var connected = new AdapterViewModel(snapshot).EnabledText;
            snapshot.Enabled = false; var disabled = new AdapterViewModel(snapshot).EnabledText;
            Require(disconnected != connected && disconnected != disabled && connected != disabled, "Status labels indistinguishable.");
            var vm = new MainViewModel(); vm.IsDhcp = true; vm.IsDnsAutomatic = true; vm.IsStatic = true;
            vm.IsDnsAutomatic = true;
            Require(vm.IsDnsStatic, "Static editor accepted automatic DNS.");
            vm.IsDhcp = true; vm.IsDnsAutomatic = true;
            Require(vm.IsDnsAutomatic, "DHCP automatic DNS unavailable.");
            return Task.CompletedTask;
        });
        await Check("变更预览包含保留的附加地址", () =>
        {
            var before = Snapshot(); before.Config.IPv4Addresses.Add(new() { Address = "198.51.100.10", PrefixLength = 24 });
            var rows = ConfigurationPreview.Create(before, NetworkConfig.ForPreset("192.0.2.20", 24, null, DnsMode.Static, ["192.0.2.53"]));
            Require(rows[1].Changed && rows[1].Target.Contains("198.51.100.10/24"), "Preview omitted a preserved address.");
            Require(!rows[3].Changed, "Unchanged DNS was highlighted.");
            Require(before.Config.IPv4Addresses[0].Address == "192.0.2.10", "Preview mutated state.");
            return Task.CompletedTask;
        });
        foreach (var mutation in new[] { "index", "name", "mac", "enabled", "ip", "managed", "removed", "second-read" })
        await Check("预览后状态变化拒绝写入-" + mutation, async () =>
        {
            var reviewed = Snapshot(); var current = Snapshot(); var writes = 0; var reads = 0;
            void Change()
            {
                switch (mutation)
                {
                    case "index": current.InterfaceIndex++; break;
                    case "name": current.Name = "renamed"; break;
                    case "mac": current.MacAddress = "changed"; break;
                    case "enabled": current.Enabled = false; break;
                    case "managed": current.Config.ManagedIPv4Address = "192.0.2.99"; break;
                    default: current.Config.IPv4Addresses[0].Address = "192.0.2.99"; break;
                }
            }
            if (mutation != "second-read") Change();
            var network = new NetworkService(_ =>
            {
                if (++reads == 2 && mutation == "second-read") Change();
                return Task.FromResult<IReadOnlyList<AdapterSnapshot>>(mutation == "removed" ? [] : [current]);
            }, (_, _, _) => writes++, (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            var result = await network.ApplyReviewedAsync(reviewed, new());
            Require(!result.Succeeded && !result.RolledBack && writes == 0, "Stale review wrote configuration.");
        });
        await Check("确认状态未变时使用完整应用复核流程", async () =>
        {
            var state = Snapshot(); var writes = 0;
            AdapterSnapshot Read() => new() { InterfaceGuid = state.InterfaceGuid, InterfaceIndex = state.InterfaceIndex, Name = state.Name,
                Enabled = true, Ipv4InterfacePresent = true, Config = state.Config.Clone() };
            var network = new NetworkService(_ => Task.FromResult<IReadOnlyList<AdapterSnapshot>>([Read()]),
                (_, target, _) => { writes++; state.Config = target.Clone(); }, (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            var result = await network.ApplyReviewedAsync(Read(), NetworkConfig.ForPreset("192.0.2.20", 24, null, DnsMode.Static, []));
            Require(result.Succeeded && writes == 1, "Reviewed write did not verify successfully.");
        });
        await Check("切换网卡保留独立草稿并可还原", () =>
        {
            var vm = new MainViewModel(); var a = new AdapterViewModel(Snapshot()); var b = new AdapterViewModel(Snapshot("adapter-b"));
            vm.SelectedAdapter = a; Require(!vm.HasDraft, "Fresh load marked dirty.");
            vm.IPv4Address = "invalid draft"; Require(vm.HasDraft, "Invalid draft must remain visible.");
            vm.SelectedAdapter = b; Require(vm.IPv4Address == "192.0.2.10", "Draft leaked across adapters.");
            vm.Gateway = "192.0.2.2"; vm.SelectedAdapter = a;
            Require(vm.IPv4Address == "invalid draft" && vm.HasDraft, "Switch lost draft.");
            vm.RestoreCurrentCommand.Execute(null);
            Require(!vm.HasDraft && vm.IPv4Address == "192.0.2.10", "Restore did not load actual state.");
            vm.SelectedAdapter = b; Require(vm.Gateway == "192.0.2.2", "Restore cleared another adapter draft.");
            return Task.CompletedTask;
        });
        static Preset Preset(string name = "Office") => new() { Name = name, AdapterInterfaceGuid = "adapter-a", Config = Snapshot().Config };
        await Check("取消预览保留草稿且不写入", async () =>
        {
            var writes = 0; var reviews = 0;
            var network = new NetworkService(_ => Task.FromResult<IReadOnlyList<AdapterSnapshot>>([Snapshot()]),
                (_, _, _) => writes++, (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            var vm = new MainViewModel(network, new PresetService(Path.Combine(AppContext.BaseDirectory, Guid.NewGuid() + ".json")))
            { ReviewConfiguration = (_, _) => { reviews++; return false; } };
            await vm.InitializeAsync(); vm.IPv4Address = "192.0.2.20";
            vm.ApplyCommand.Execute(null); await vm.ActiveOperation;
            Require(reviews == 1 && writes == 0 && vm.HasDraft && vm.IPv4Address == "192.0.2.20", "Cancel lost draft or wrote state.");
        });
        await Check("非法草稿不进入预览或网络写入", async () =>
        {
            var network = new NetworkService(_ => Task.FromResult<IReadOnlyList<AdapterSnapshot>>([Snapshot()]),
                (_, _, _) => throw new Exception("Unexpected write"), (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            var vm = new MainViewModel(network, new PresetService(Path.Combine(AppContext.BaseDirectory, Guid.NewGuid() + ".json")))
            { ReviewConfiguration = (_, _) => throw new Exception("Unexpected preview") };
            await vm.InitializeAsync(); vm.IPv4Address = "invalid";
            vm.ApplyCommand.Execute(null); await vm.ActiveOperation;
            Require(vm.HasDraft && vm.IPv4Address == "invalid" && vm.DetailMessage.Length > 0, "Invalid draft was lost or not explained.");
        });
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        byte[] Serialize(PresetLibrary l) => JsonSerializer.SerializeToUtf8Bytes(l, options);
        await Check("v1预设往返与导出数据不变", async () =>
        {
            var p = Preset(); var path = Path.Combine(AppContext.BaseDirectory, "export-" + Guid.NewGuid() + ".json");
            try
            {
                await PresetExchange.ExportAsync(path, [p], ["Office"]);
                var read = await PresetExchange.ReadFileAsync(path);
                Require(read.Version == 1 && read.Presets[0].Id == p.Id && read.Presets[0].AdapterInterfaceGuid == "adapter-a", "v1 identity lost.");
                Require(read.Presets[0].Config.IPv4Addresses[0].Gateway == "192.0.2.1", "Gateway lost.");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
        await Check("非法导入不改变旧库与源对象", () =>
        {
            var good = Serialize(new() { Presets = [Preset()] });
            foreach (var bytes in new[] { Encoding.UTF8.GetBytes("{}"), Encoding.UTF8.GetBytes("{\"Version\":2,\"Presets\":[],\"Groups\":[]}"),
                Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(good).Replace("Static", "Unknown")), new byte[PresetExchange.MaximumBytes + 1],
                Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(good).Replace("192.0.2.10", "invalid")) })
            {
                var rejected = false; try { PresetExchange.Read(bytes); } catch (InvalidDataException) { rejected = true; }
                Require(rejected, "Malformed import accepted.");
            }
            return Task.CompletedTask;
        });
        await Check("导入重复策略处理ID同名与文件内重复", () =>
        {
            var existing = Preset(); var incoming = existing.Clone(); incoming.Name = "Incoming";
            var rows = PresetExchange.Preview([existing], [incoming, incoming.Clone()]);
            Require(rows.All(r => r.Conflict), "Duplicate preview incomplete.");
            Require(PresetExchange.Merge([existing], [incoming], ImportConflictPolicy.Skip).Single().Name == "Office", "Skip overwrote existing.");
            Require(PresetExchange.Merge([existing], [incoming, incoming.Clone()], ImportConflictPolicy.Replace).Single().Name == "Incoming", "Replace policy inconsistent.");
            Require(existing.Name == "Office", "Merge mutated live entry.");
            return Task.CompletedTask;
        });
        await Check("跨电脑网卡禁止隐式回退且绑定后可解析", () =>
        {
            var preset = Preset(); var local = new AdapterViewModel(Snapshot("local-guid"));
            preset.AdapterName = local.Name;
            Require(PresetAdapterResolver.Resolve(preset, [local]) is null, "Foreign GUID fell back to name.");
            var copy = preset.Clone(); copy.AdapterInterfaceGuid = local.Snapshot.InterfaceGuid;
            Require(PresetAdapterResolver.Resolve(copy, [local]) == local && preset.AdapterInterfaceGuid == "adapter-a", "Manual binding failed or changed original.");
            return Task.CompletedTask;
        });
    }
}
