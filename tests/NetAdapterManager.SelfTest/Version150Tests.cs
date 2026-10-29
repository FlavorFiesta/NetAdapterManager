using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using NetAdapterManager.Models;
using NetAdapterManager.Services;
using NetAdapterManager.ViewModels;

internal static class Version150Tests
{
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    internal static async Task RunAsync(Action<string, string> pass, Action<string, string> fail)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "v150-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        async Task Check(string name, Func<Task> test)
        {
            try { await test(); pass(name, "Passed with isolated files and synthetic network state."); }
            catch (Exception ex) { fail(name, ex.ToString()); }
            finally { Localization.SetLanguage("zh-CN"); }
        }
        await Check("恢复前校验备份且不替换正常库", async () =>
        {
            foreach (var invalid in new[] { "{\"Version\":2,\"Presets\":[],\"Groups\":[]}",
                "{\"Version\":1,\"Presets\":null,\"Groups\":[]}",
                "{\"Version\":1,\"Presets\":[null],\"Groups\":[]}" })
            {
                var path = Path.Combine(root, Guid.NewGuid() + ".json");
                var service = new PresetService(path);
                await service.SaveLibraryAsync([], ["kept"]);
                var original = await File.ReadAllBytesAsync(path);
                await File.WriteAllTextAsync(path + ".bak", invalid);
                var rejected = false;
                try { await service.RestoreBackupAsync(); } catch (IOException) { rejected = true; }
                Require(rejected && original.SequenceEqual(await File.ReadAllBytesAsync(path)), "Invalid backup replaced current data.");
                Require((await service.LoadGroupsAsync()).Single() == "kept", "Current library became unreadable.");
            }
        });
        await Check("预设损坏仍可初始化网卡", async () =>
        {
            var path = Path.Combine(root, "damaged.json");
            await File.WriteAllTextAsync(path, "{broken");
            var reads = 0;
            var network = new NetworkService(_ => { reads++; return Task.FromResult<IReadOnlyList<AdapterSnapshot>>([Snapshot(Initial())]); },
                (_, _, _) => throw new Exception("Unexpected write"), (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            var vm = new MainViewModel(network, new PresetService(path));
            await vm.InitializeAsync();
            Require(reads == 1 && vm.Adapters.Count == 1 && vm.DetailMessage.Length > 0, "Adapter load or preset warning missing.");
            Require(await File.ReadAllTextAsync(path) == "{broken", "Damaged preset file was changed.");
        });
        await Check("成功应用预设同步编辑区且再次应用不撤销", async () =>
        {
            var state = Initial(); var writes = 0;
            var network = new NetworkService(_ => Task.FromResult<IReadOnlyList<AdapterSnapshot>>([Snapshot(state)]),
                (_, config, _) => { writes++; state = config.Clone(); },
                (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            var vm = new MainViewModel(network, new PresetService(Path.Combine(root, "preset-success.json")));
            await vm.InitializeAsync();
            var preset = new Preset { Name = "Target", AdapterInterfaceGuid = Snapshot(state).InterfaceGuid,
                Config = NetworkConfig.ForPreset("192.0.2.20", 24, null, DnsMode.Automatic, []) };
            vm.ApplyPresetCommand.Execute(preset); await vm.ActiveOperation;
            Require(state.ManagedIPv4Address == "192.0.2.20" && vm.IPv4Address == "192.0.2.20", "Successful preset left stale editor values.");
            vm.ApplyCommand.Execute(null); await vm.ActiveOperation;
            Require(state.ManagedIPv4Address == "192.0.2.20" && writes == 2, "Second apply reverted the preset.");
        });
        await Check("损坏预设组合状态随语言切换", async () =>
        {
            var path = Path.Combine(root, "localized-damaged.json");
            await File.WriteAllTextAsync(path, "{broken");
            var network = new NetworkService(_ => Task.FromResult<IReadOnlyList<AdapterSnapshot>>([Snapshot(Initial())]),
                (_, _, _) => throw new Exception("Unexpected write"), (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            var vm = new MainViewModel(network, new PresetService(path));
            await vm.InitializeAsync();
            foreach (var language in new[] { "en", "zh-TW", "zh-CN" })
            {
                Localization.SetLanguage(language);
                Require(vm.StatusMessage == Localization.F("Loc_A85CC4FEE060", 1) + Localization.T("Loc_6C97C280234D"), "Composed status did not translate.");
            }
        });
        await Check("嵌套消息保留参数和原始错误", () =>
        {
            var message = Localization.Capture(Localization.Join("\n", Localization.F("Loc_A85CC4FEE060", 3),
                Localization.Join(" ", Localization.T("Loc_EA011B9820F1"), "Synthetic failure")));
            foreach (var language in new[] { "en", "zh-TW", "zh-CN" })
            {
                Localization.SetLanguage(language);
                Require(message.Render() == Localization.F("Loc_A85CC4FEE060", 3) + "\n" + Localization.T("Loc_EA011B9820F1") + " Synthetic failure", "Nested message lost translation or literal detail.");
            }
            return Task.CompletedTask;
        });
        await Check("回滚失败后刷新实际状态并保留草稿", async () =>
        {
            var state = Initial(); var writes = 0;
            var network = new NetworkService(_ => Task.FromResult<IReadOnlyList<AdapterSnapshot>>([Snapshot(state)]),
                (_, config, _) => { if (++writes == 1) state = config.Clone(); throw new IOException("Synthetic failure"); },
                (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            var vm = new MainViewModel(network, new PresetService(Path.Combine(root, "missing.json")));
            await vm.InitializeAsync(); vm.IPv4Address = "192.0.2.20";
            vm.ApplyCommand.Execute(null); await vm.ActiveOperation;
            Require(writes == 2 && vm.SelectedAdapter!.Snapshot.Config.ManagedIPv4Address == "192.0.2.20", "Displayed snapshot is stale.");
            Require(vm.IPv4Address == "192.0.2.20" && vm.StatusMessage.Contains("回滚"), "Draft or failure was lost.");
            Localization.SetLanguage("en");
            Require(vm.StatusMessage == "Configuration failed, and automatic rollback also failed.", "Existing result did not change language.");
        });
        await Check("失败后读取异常清除可操作旧快照", async () =>
        {
            var writes = 0;
            var network = new NetworkService(_ => writes > 0 ? throw new IOException("Reader failed")
                : Task.FromResult<IReadOnlyList<AdapterSnapshot>>([Snapshot(Initial())]),
                (_, _, _) => { writes++; throw new IOException("Writer failed"); },
                (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            var vm = new MainViewModel(network, new PresetService(Path.Combine(root, "missing2.json")));
            await vm.InitializeAsync(); vm.ApplyCommand.Execute(null); await vm.ActiveOperation;
            vm.HideVirtual = true;
            Require(vm.Adapters.Count == 0 && !vm.CanConfigure && vm.DetailMessage.Contains("状态未知"), "Stale state can still be used.");
            Require(vm.StatusMessage.Contains("回滚"), "Original error was overwritten.");
        });
        await Check("启停超时不放开未完成写入", async () =>
        {
            var completion = new TaskCompletionSource<uint>(TaskCreationOptions.RunContinuationsAsynchronously);
            var service = NoNetwork();
            try
            {
                var result = await service.ToggleWithTimeoutAsync(() => completion.Task, TimeSpan.FromMilliseconds(30));
                Require(!result.Succeeded && result.ErrorDetail!.Contains("状态未知"), "Timeout not reported.");
                var invoked = false;
                var blocked = await NoNetwork().ToggleWithTimeoutAsync(() => { invoked = true; return Task.FromResult(0u); }, TimeSpan.FromSeconds(1));
                Require(!blocked.Succeeded && !invoked, "Concurrent writer started after uncertain timeout.");
            }
            finally { completion.TrySetResult(0); }
            var stillBlocked = await service.ToggleWithTimeoutAsync(() => throw new Exception("Unexpected retry"), TimeSpan.FromSeconds(1));
            Require(!stillBlocked.Succeeded, "Original instance must remain blocked.");
            // Let the observer finish before the following independent tests.
            for (var i = 0; i < 100; i++)
            {
                var result = await NoNetwork().ToggleWithTimeoutAsync(() => Task.FromResult(0u), TimeSpan.FromSeconds(1));
                if (result.Succeeded) return;
                await Task.Delay(10);
            }
            throw new Exception("Write gate did not release after confirmed completion.");
        });
        await Check("三语言资源完整与占位符一致", () =>
        {
            foreach (var (key, translations) in Localization.Catalog)
            {
                var expected = Placeholders(translations["zh-CN"]);
                foreach (var language in new[] { "zh-CN", "zh-TW", "en" })
                {
                    Require(translations.TryGetValue(language, out var text) && !string.IsNullOrWhiteSpace(text), $"Missing {key}/{language}");
                    Require(expected == Placeholders(text!), $"Placeholder mismatch {key}/{language}");
                }
            }
            Require(Localization.ResolveLanguage("zh-HK") == "zh-TW" && Localization.ResolveLanguage("zh-Hans-SG") == "zh-CN"
                && Localization.ResolveLanguage("fr-FR") == "en", "Locale mapping failed.");
            return Task.CompletedTask;
        });
        await Check("语言切换不改变预设标识或输入", () =>
        {
            var preset = new Preset { Name = "未分配", GroupName = GroupIds.Ungrouped, Config = Initial() };
            var before = JsonSerializer.Serialize(preset);
            var culture = System.Globalization.CultureInfo.CurrentUICulture;
            var vm = new MainViewModel { IPv4Address = "draft", PresetGroupFilter = GroupIds.Ungrouped };
            foreach (var language in new[] { "zh-TW", "en", "zh-CN" })
            {
                Localization.SetLanguage(language);
                Require(before == JsonSerializer.Serialize(preset) && vm.IPv4Address == "draft" && vm.PresetGroupFilter == GroupIds.Ungrouped, "Language changed user data.");
                Require(ReferenceEquals(culture, System.Globalization.CultureInfo.CurrentUICulture), "UI language changed command/system culture.");
            }
            Localization.SetLanguage("en");
            Require(GroupIds.Display(GroupIds.Ungrouped) == "Ungrouped" && GroupIds.Display("用户分组") == "用户分组", "Group display changed custom data.");
            return Task.CompletedTask;
        });
        await Check("安装语言种子和已有偏好优先级", async () =>
        {
            var path = Path.Combine(root, "settings.json");
            var seed = Path.Combine(root, "install-language.txt");
            await File.WriteAllTextAsync(seed, "chinesetrad");
            var prefs = new PreferencesService(path); prefs.Load();
            Require(prefs.Current.Language == "zh-TW", "Installer seed was ignored.");
            await prefs.SaveAsync(new AppPreferences { Language = "en", Theme = "dark" });
            await File.WriteAllTextAsync(seed, "chinesesimp"); prefs.Load();
            Require(prefs.Current.Language == "en" && prefs.Current.Theme == "dark", "Upgrade seed overwrote settings.");
            await prefs.SaveAsync(new AppPreferences { Language = "system", Theme = "light" }); prefs.Load();
            Require(prefs.Current.Language == "system", "Follow-system choice was lost.");
        });
        await Check("偏好独立回退及损坏文件保留", async () =>
        {
            var path = Path.Combine(root, "bad-settings.json");
            await File.WriteAllTextAsync(path, "{\"Language\":\"bad\",\"Theme\":\"dark\"}");
            var prefs = new PreferencesService(path); prefs.Load();
            Require(prefs.Current.Language == "system" && prefs.Current.Theme == "dark", "Invalid language discarded valid theme.");
            await File.WriteAllTextAsync(path, "{broken"); prefs.Load();
            Require(prefs.LoadError is not null, "Missing settings warning.");
            await prefs.SaveAsync(new AppPreferences { Language = "en" });
            Require(Directory.GetFiles(root, "bad-settings.json.corrupt-*").Any(p => File.ReadAllText(p) == "{broken"), "Original corrupt settings were lost.");
        });
    }
    private static string Placeholders(string text) => string.Join("|", Regex.Matches(text, @"\{\d+(?:[^}]*)\}").Select(m => m.Value).Order());
    private static NetworkConfig Initial() => NetworkConfig.ForPreset("192.0.2.10", 24, null, DnsMode.Automatic, []);
    private static AdapterSnapshot Snapshot(NetworkConfig config) => new() { Name = "Synthetic adapter", InterfaceGuid = "00000000-0000-0000-0000-000000000042", InterfaceIndex = 42, Enabled = true, Ipv4InterfacePresent = true, Config = config.Clone() };
    private static NetworkService NoNetwork() => new(_ => Task.FromResult<IReadOnlyList<AdapterSnapshot>>([]), (_, _, _) => throw new Exception("Unexpected network write"), (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
}
