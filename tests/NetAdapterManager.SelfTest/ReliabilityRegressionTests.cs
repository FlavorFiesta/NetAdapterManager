using System.Diagnostics;
using System.IO;
using System.Reflection;
using NetAdapterManager.Models;
using NetAdapterManager.Services;
using NetAdapterManager.ViewModels;

internal static class ReliabilityRegressionTests
{
    internal static async Task<int> RunHelperAsync(string[] args)
    {
        switch (args[1])
        {
            case "flood":
                Console.Error.Write(new string('e', 256 * 1024));
                Console.Out.Write("output-complete");
                return 7;
            case "sleep":
                await File.WriteAllTextAsync(args[2] + ".tmp", Environment.ProcessId.ToString());
                File.Move(args[2] + ".tmp", args[2]);
                await Task.Delay(TimeSpan.FromSeconds(30));
                return 0;
            case "mutex":
                using (var lease = new SingleInstanceLease(args[2])) Console.Write(lease.Acquired ? "acquired" : "blocked");
                return 0;
            default: return 2;
        }
    }

    internal static async Task RunAsync(Action<string, string> pass, Action<string, string> fail)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "reliability-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        async Task Check(string name, Func<Task> action)
        {
            try { await action(); pass(name, "隔离测试通过，未访问真实网卡或用户数据。"); }
            catch (Exception ex) { fail(name, ex.ToString()); }
        }

        await Check("损坏 JSON 禁止覆盖并可恢复备份", async () =>
        {
            var path = Path.Combine(root, "corrupt.json");
            var store = new JsonStore<List<string>>(path);
            await store.SaveAsync(["original"]);
            await store.SaveAsync(["latest"]);
            const string broken = "[\"recoverable\", BROKEN]";
            await File.WriteAllTextAsync(path, broken);
            await ThrowsAsync<IOException>(() => store.LoadAsync([]));
            await ThrowsAsync<IOException>(() => store.SaveAsync(["replacement"]));
            await ThrowsAsync<System.Text.Json.JsonException>(() => new JsonStore<List<string>>(path).SaveAsync(["replacement"]));
            Require(await File.ReadAllTextAsync(path) == broken, "读取或保存失败不得改写原文件。");
            await store.RestoreBackupAsync();
            Require((await store.LoadAsync([])).Single() == "original", "应恢复上次保存前的数据。");
            var preserved = Directory.GetFiles(root, "corrupt.json.corrupt-*").Single();
            Require(await File.ReadAllTextAsync(preserved) == broken, "恢复时应另存损坏文件。");
        });
        await Check("读取 IO 失败阻止后续静默覆盖", async () =>
        {
            var path = Path.Combine(root, "locked.json");
            var store = new JsonStore<List<string>>(path);
            await store.SaveAsync(["original"]);
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                await ThrowsAsync<IOException>(() => store.LoadAsync([]));
            await ThrowsAsync<IOException>(() => store.SaveAsync(["replacement"]));
            Require((await store.LoadAsync([])).Single() == "original", "重新读取成功前必须保护旧数据。");
            await store.SaveAsync(["allowed-after-reload"]);
        });
        await Check("取消保存保留旧数据且清理临时文件", async () =>
        {
            var path = Path.Combine(root, "cancel.json");
            var store = new JsonStore<List<string>>(path);
            await store.SaveAsync(["original"]);
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            await ThrowsAsync<OperationCanceledException>(() => store.SaveAsync(["new"], cancel.Token));
            Require((await store.LoadAsync([])).Single() == "original", "取消不得替换旧数据。");
            Require(Directory.GetFiles(root, "*.tmp").Length == 0, "不得遗留临时文件。");
        });
        await Check("旧预设与空分组迁移且保留原文件", async () =>
        {
            var oldPresets = Path.Combine(root, "old-presets.json");
            var oldGroups = Path.Combine(root, "old-groups.json");
            var libraryPath = Path.Combine(root, "library.json");
            var original = new Preset { Name = "legacy", GroupName = "legacy-group", AdapterInterfaceGuid = "legacy-guid" };
            await new JsonStore<List<Preset>>(oldPresets).SaveAsync([original]);
            await new JsonStore<List<string>>(oldGroups).SaveAsync(["legacy-group", "empty-group"]);
            var oldBytes = await File.ReadAllBytesAsync(oldPresets);
            var service = new PresetService(libraryPath, oldPresets, oldGroups);
            var entries = await service.LoadAsync();
            Require(entries.Single().Id == original.Id, "迁移不得改变预设 ID。");
            await service.SaveAsync(entries);
            Require((await service.LoadGroupsAsync()).Contains("empty-group"), "空分组必须迁移。");
            Require(oldBytes.SequenceEqual(await File.ReadAllBytesAsync(oldPresets)), "迁移不得改动旧文件。");
            await File.WriteAllTextAsync(oldPresets, "BROKEN");
            Require((await service.LoadAsync()).Count == 1, "统一库建立后不得重新读取旧数据。");
        });
        await Check("分组与预设单次提交", async () =>
        {
            var path = Path.Combine(root, "group-success.json");
            var service = new PresetService(path);
            var entry = new Preset { Name = "saved", GroupName = "group" };
            await service.SaveLibraryAsync([entry], ["group", "empty"]);
            var vm = Seed(service, [entry], ["group", "empty"]);
            Require(await vm.ChangePresetGroupAsync("group", true), "删除分组失败。");
            Require((await service.LoadAsync()).Single().GroupName == "未分组", "预设应解除分组。");
            Require(!(await service.LoadGroupsAsync()).Contains("group") && (await service.LoadGroupsAsync()).Contains("empty"), "分组集合不正确。");
            Require(entry.GroupName == "group", "提交不得提前污染原对象。");
            await service.RestoreBackupAsync();
            Require((await service.LoadAsync()).Single().GroupName == "group" && (await service.LoadGroupsAsync()).Contains("group"), "备份应同时恢复两个集合。");
        });
        await Check("分组提交失败保留内存与磁盘", async () =>
        {
            var path = Path.Combine(root, "group-failure.json");
            var service = new PresetService(path);
            var entry = new Preset { Name = "saved", GroupName = "group", IsBatchSelected = true };
            await service.SaveLibraryAsync([entry], ["group"]);
            var original = await File.ReadAllBytesAsync(path);
            var vm = Seed(service, [entry], ["group"]);
            // Readers are allowed; replacement is denied. This exercises commit failure after preparation.
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Require(!await vm.ChangePresetGroupAsync("group", true), "替换失败不应报告删除成功。");
                Require(!await vm.ChangePresetGroupAsync("new", false), "替换失败不应报告添加成功。");
            }
            Require(entry.GroupName == "group" && vm.BatchSelectedPresets.Count == 1, "失败必须保留原分组与选择。");
            Require(original.SequenceEqual(await File.ReadAllBytesAsync(path)), "失败必须保留完整旧文件。");
            Require(!(await service.LoadGroupsAsync()).Contains("new"), "失败的新分组不得持久化。");
        });
        await Check("预设身份与校验进入隔离 CI", () =>
        {
            var config = NetworkConfig.ForPreset("192.0.2.5", 24, null, DnsMode.Static, ["192.0.2.53"]);
            Require(ValidationService.ValidateManualConfig(config).IsValid, "有效配置被拒绝。");
            config.IPv4Addresses[0].PrefixLength = 33;
            Require(!ValidationService.ValidateManualConfig(config).IsValid, "非法前缀未被拒绝。");
            var actual = new AdapterViewModel(new() { InterfaceGuid = "actual", Name = "same-name" });
            Require(PresetAdapterResolver.Resolve(new() { AdapterInterfaceGuid = "missing", AdapterName = "same-name" }, [actual]) is null, "不得回退到同名网卡。");
            var preset = new Preset { AdapterInterfaceGuid = "actual", Config = new() };
            Require(PresetService.MatchesConfiguration(preset, new() { DnsServers = ["stale"] }, "", "", "actual"), "自动 DNS 残留值不应造成重复。");
            return Task.CompletedTask;
        });
        await Check("跨进程单实例限制与释放", () =>
        {
            var name = @"Local\NetAdapterManager-Test-" + Guid.NewGuid().ToString("N");
            using (var lease = new SingleInstanceLease(name))
            {
                Require(lease.Acquired, "首个实例应获得所有权。");
                // Mutex ownership is thread-affine; do not await while holding it in a console test.
                var result = ProcessRunner.RunAsync(Helper("mutex", name), TimeSpan.FromSeconds(8), default).GetAwaiter().GetResult();
                Require(result.Output == "blocked", "第二个进程不能进入。");
            }
            using var next = new SingleInstanceLease(name);
            Require(next.Acquired, "退出后应释放实例锁。");
            return Task.CompletedTask;
        });
        await Check("并行排空命令输出和错误流", async () =>
        {
            var result = await ProcessRunner.RunAsync(Helper("flood"), TimeSpan.FromSeconds(8), default);
            Require(result.ExitCode == 7 && result.Output.Contains("output-complete") && result.Output.Length > 256 * 1024, "大量 stderr 不应造成死锁或丢失退出码。");
        });
        await Check("命令超时后先确认进程退出", async () =>
        {
            var marker = Path.Combine(root, "timeout.pid");
            await ThrowsAsync<TimeoutException>(() => ProcessRunner.RunAsync(Helper("sleep", marker), TimeSpan.FromSeconds(2), default));
            Require(!File.Exists(marker) || !IsAlive(int.Parse(await File.ReadAllTextAsync(marker))), "超时返回前子进程必须退出。");
        });
        await Check("命令取消后先确认进程退出", async () =>
        {
            var marker = Path.Combine(root, "cancel.pid");
            using var cancel = new CancellationTokenSource();
            var task = ProcessRunner.RunAsync(Helper("sleep", marker), TimeSpan.FromSeconds(8), cancel.Token);
            for (var i = 0; i < 200 && !File.Exists(marker); i++) await Task.Delay(20);
            cancel.Cancel();
            await ThrowsAsync<OperationCanceledException>(() => task);
            Require(File.Exists(marker) && !IsAlive(int.Parse(await File.ReadAllTextAsync(marker))), "取消返回前已启动的子进程必须退出。");
        });
        await Check("可取消写入结束后独立恢复", async () =>
        {
            var state = new AdapterSnapshot { InterfaceGuid = "test", Config = new() };
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var writes = 0;
            var writerStopped = false;
            var service = new NetworkService(_ => Task.FromResult<IReadOnlyList<AdapterSnapshot>>([new() { InterfaceGuid = "test", Config = state.Config.Clone() }]),
                async (_, target, _, token) =>
                {
                    if (++writes == 1)
                    {
                        state.Config = target.Clone();
                        entered.SetResult();
                        try { await Task.Delay(Timeout.Infinite, token); }
                        finally { writerStopped = true; }
                    }
                    else { Require(writerStopped && !token.IsCancellationRequested, "恢复必须等待旧写入结束且使用独立令牌。"); state.Config = target.Clone(); }
                }, (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            using var cancel = new CancellationTokenSource();
            var task = service.ApplyAsync(state, NetworkConfig.ForPreset("192.0.2.5", 24, null, DnsMode.Automatic, []), OperationSource.Manual, cancel.Token);
            await entered.Task;
            cancel.Cancel();
            var result = await task;
            Require(!result.Succeeded && result.RolledBack && writes == 2 && state.Config.IpMode == IpMode.Dhcp, "取消后必须复核恢复 DHCP。");
        });
        await Check("元数据读取失败不阻断网络恢复", async () =>
        {
            var state = new NetworkConfig();
            var written = false;
            var service = new NetworkService(
                _ => written ? throw new IOException("metadata unavailable") : Task.FromResult<IReadOnlyList<AdapterSnapshot>>([new() { InterfaceGuid = "test", Config = state.Clone() }]),
                (_, target, _, _) => { state = target.Clone(); written = true; return Task.CompletedTask; },
                (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask,
                _ => Task.FromResult<IReadOnlyList<AdapterSnapshot>>([new() { InterfaceGuid = "test", Config = state.Clone() }]));
            var result = await service.ApplyAsync(new() { InterfaceGuid = "test" }, NetworkConfig.ForPreset("192.0.2.5", 24, null, DnsMode.Automatic, []), OperationSource.Manual);
            Require(!result.Succeeded && result.RolledBack && state.IpMode == IpMode.Dhcp, "恢复读取不能依赖本地管理地址文件。");
        });
        await Check("命令退出不确定时禁止后续写入", async () =>
        {
            var writes = 0;
            var service = new NetworkService(
                _ => Task.FromResult<IReadOnlyList<AdapterSnapshot>>([new() { InterfaceGuid = "test", Config = new() }]),
                (_, _, _, _) => { writes++; throw new ProcessTerminationException("uncertain", new IOException()); },
                (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            var adapter = new AdapterSnapshot { InterfaceGuid = "test" };
            var target = NetworkConfig.ForPreset("192.0.2.5", 24, null, DnsMode.Automatic, []);
            var first = await service.ApplyAsync(adapter, target, OperationSource.Manual);
            var second = await service.ApplyAsync(adapter, target, OperationSource.Manual);
            Require(!first.Succeeded && !first.RolledBack && !second.Succeeded && writes == 1, "不能与未知状态的旧命令并行回滚或重复写入。");
        });
        // Only delete this run's unique directory; it contains synthetic files exclusively.
        if (Path.GetDirectoryName(Path.GetFullPath(root)) != Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar)
            || !Path.GetFileName(root).StartsWith("reliability-", StringComparison.Ordinal))
            throw new IOException("Unexpected test cleanup path.");
        Directory.Delete(root, recursive: true);
    }

    private static MainViewModel Seed(PresetService service, List<Preset> presets, List<string> groups)
    {
        var vm = new MainViewModel(presetService: service);
        typeof(MainViewModel).GetField("_allPresets", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, presets);
        typeof(MainViewModel).GetField("_customPresetGroups", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(vm, groups);
        return vm;
    }

    private static ProcessStartInfo Helper(params string[] args)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!);
        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(ReliabilityRegressionTests).Assembly.Location);
        start.ArgumentList.Add("--process-helper");
        foreach (var arg in args) start.ArgumentList.Add(arg);
        return start;
    }
    private static bool IsAlive(int pid)
    {
        try { using var process = Process.GetProcessById(pid); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }
    private static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
