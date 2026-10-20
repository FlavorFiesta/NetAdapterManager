using NetAdapterManager.Models;
using NetAdapterManager.Services;
using System.IO;

internal static class NetworkRegressionTests
{
    internal static async Task<int> RunStandaloneAsync()
    {
        var failed = 0;
        var passed = 0;
        await RunAsync((name, detail) => { passed++; Console.WriteLine($"[GREEN] {name}: {detail}"); },
            (name, detail) => { failed++; Console.WriteLine($"[RED] {name}: {detail}"); });
        Console.WriteLine($"Network regression: {passed} passed, {failed} failed; no real network writes.");
        return failed == 0 ? 0 : 1;
    }

    internal static async Task RunAsync(Action<string, string> pass, Action<string, string> fail)
    {
        async Task Check(string name, Func<Task> test)
        {
            try { await test(); pass(name, "隔离回归通过，未访问真实网卡。"); }
            catch (Exception ex) { fail(name, ex.Message); }
        }

        await Check("DHCP + 手动 DNS", async () =>
        {
            var h = new Harness(Dhcp());
            var target = Dhcp(DnsMode.Static, "192.0.2.53");
            var result = await h.ApplyAsync(target);
            Require(result.Succeeded && h.State.Config.IpMode == IpMode.Dhcp, "手动 DNS 应能与 DHCP 同时应用并通过复核。");
            Require(h.Commands.Count == 1 && h.Commands[0].Contains("address=192.0.2.53"), "只应写入手动 DNS。");
        });
        await Check("手动 DNS 转自动", async () =>
        {
            var original = Dhcp(WindowsDnsSettings.ResolveMode("192.0.2.53"), "192.0.2.53");
            var h = new Harness(original);
            var result = await h.ApplyAsync(Dhcp());
            Require(result.Succeeded && h.Commands.Any(IsAutomaticDns), "必须生成恢复自动 DNS 的命令。");
        });
        await Check("静态 IP + 自动 DNS", async () =>
        {
            var h = new Harness(Dhcp());
            Require((await h.ApplyAsync(Static(DnsMode.Automatic))).Succeeded, "静态 IP 不能强制改为手动 DNS。");
            Require(h.Commands.Any(IsAutomaticDns), "切换 IP 模式后应明确应用自动 DNS。");
        });
        await Check("DNS 来源独立于 IP 和有效地址", () =>
        {
            Require(WindowsDnsSettings.ResolveMode("192.0.2.53") == DnsMode.Static, "静态覆盖不能识别为自动。");
            Require(WindowsDnsSettings.ResolveMode(null, "192.0.2.54") == DnsMode.Static, "应识别 profile 静态 DNS。");
            Require(WindowsDnsSettings.ResolveMode("  ") == DnsMode.Automatic, "空覆盖应为自动。");
            var actual = Snapshot(Dhcp(DnsMode.Static, "192.0.2.53"));
            Require(NetworkService.MatchesTarget(actual, Dhcp(DnsMode.Static, "192.0.2.53"), false), "正确模式与地址应通过复核。");
            Require(!NetworkService.MatchesTarget(actual, Dhcp(), false), "手动 DNS 不能满足自动目标。");
            return Task.CompletedTask;
        });
        await Check("写入中途失败恢复 DHCP/DNS", async () =>
        {
            var h = new Harness(Dhcp()) { FailForwardDns = true };
            var result = await h.ApplyAsync(Static());
            Require(!result.Succeeded && result.RolledBack, "部分写入失败应恢复并报告回滚。");
            Require(h.RecoveryCommands.Any(IsDhcp) && h.RecoveryCommands.Any(IsAutomaticDns), "回滚必须真正恢复 DHCP 和自动 DNS。");
            Require(h.RecoveryReads > 0 && NetworkService.MatchesTarget(h.State, Dhcp(), true), "必须读回复核恢复结果。");
        });
        await Check("复核失败触发恢复", async () =>
        {
            var h = new Harness(Dhcp()) { IgnoreForwardDns = true };
            var result = await h.ApplyAsync(Static());
            Require(!result.Succeeded && result.RolledBack && h.RecoveryReads > 0, "目标复核失败也必须恢复并验证。");
        });
        await Check("管理地址保存失败触发恢复", async () =>
        {
            var h = new Harness(Dhcp()) { FailSave = true };
            var result = await h.ApplyAsync(Static());
            Require(h.SaveCalls == 1 && !result.Succeeded && result.RolledBack, "持久化失败不能遗留已修改的网络。");
        });
        await Check("仅 DNS 失败不重复开启 DHCP", async () =>
        {
            var h = new Harness(Dhcp()) { FailSave = true };
            var result = await h.ApplyAsync(Dhcp(DnsMode.Static, "192.0.2.53"));
            Require(result.RolledBack && h.RecoveryCommands.Count == 1 && h.RecoveryCommands.Any(IsAutomaticDns),
                "回滚应恢复自动 DNS，不能重复开启已经生效的 DHCP。");
            Require(!h.RecoveryCommands.Any(IsDhcp), "重复开启 DHCP 可能触发 netsh 退出码 1。");
        });
        await Check("回滚未生效不得误报成功", async () =>
        {
            var h = new Harness(Dhcp()) { FailForwardDns = true, IgnoreRecovery = true };
            var result = await h.ApplyAsync(Static());
            Require(!result.Succeeded && !result.RolledBack && h.RecoveryReads == 8, "命令成功但状态未恢复时必须报告回滚失败。");
            Require(result.ErrorDetail?.Contains("未恢复") == true, "应保留回滚复核失败详情。");
        });
        await Check("回滚命令报错不得误报成功", async () =>
        {
            var h = new Harness(Dhcp()) { FailForwardDns = true, FailRecovery = true };
            var result = await h.ApplyAsync(Static());
            Require(!result.RolledBack && result.ErrorDetail?.Contains("synthetic recovery failure") == true,
                "恢复命令异常必须报告回滚失败。");
        });
        await Check("回滚目标丢失不得误报成功", async () =>
        {
            var h = new Harness(Dhcp()) { FailForwardDns = true, MissingOnRecovery = true };
            Require(!(await h.ApplyAsync(Static())).RolledBack, "恢复后找不到目标不能视为成功。");
        });
        await Check("取消操作后仍独立恢复", async () =>
        {
            using var cancellation = new CancellationTokenSource();
            var h = new Harness(Dhcp()) { AfterForwardWrite = cancellation.Cancel };
            var result = await h.ApplyAsync(Static(), cancellation.Token);
            Require(!result.Succeeded && result.RolledBack && h.RecoveryReads > 0, "调用方取消不能取消必要的恢复。");
        });
        await Check("恢复静态多地址与手动 DNS", async () =>
        {
            var original = Static();
            original.IPv4Addresses.Add(new Ipv4AddressEntry { Address = "192.0.2.11", PrefixLength = 24 });
            var h = new Harness(original) { FailForwardDns = true };
            var result = await h.ApplyAsync(Dhcp());
            Require(result.RolledBack && NetworkService.MatchesTarget(h.State, original, true), "应恢复完整原地址集与 DNS。");
            Require(h.RecoveryCommands.Any(x => x.Contains("address=192.0.2.11")), "必须恢复额外静态地址。");
        });
        await Check("静态复核拒绝 DHCP 租约", () =>
        {
            var target = Static();
            var actual = Snapshot(target.Clone());
            actual.Config.IpMode = IpMode.Dhcp;
            Require(!NetworkService.MatchesTarget(actual, target, true), "相同地址的 DHCP 租约不能冒充静态配置。");
            return Task.CompletedTask;
        });
        await Check("恢复静态空地址", async () =>
        {
            var original = new NetworkConfig { IpMode = IpMode.Static, DnsMode = DnsMode.Automatic };
            var h = new Harness(original) { FailForwardDns = true };
            Require((await h.ApplyAsync(Static())).RolledBack, "应能复核恢复静态但无地址的原始状态。");
        });
    }

    private static NetworkConfig Dhcp(DnsMode dns = DnsMode.Automatic, params string[] servers) =>
        new() { IpMode = IpMode.Dhcp, DnsMode = dns, DnsServers = servers.ToList() };
    private static NetworkConfig Static(DnsMode dns = DnsMode.Static) =>
        NetworkConfig.ForPreset("192.0.2.10", 24, null, dns, dns == DnsMode.Static ? ["192.0.2.53"] : []);
    private static AdapterSnapshot Snapshot(NetworkConfig config) => new()
    {
        InterfaceGuid = "{00000000-0000-0000-0000-000000000042}", InterfaceIndex = 42, Config = config
    };
    private static bool IsDhcp(string[] command) => command.Contains("address") && command.Contains("source=dhcp");
    private static bool IsAutomaticDns(string[] command) => command.Contains("dnsservers") && command.Contains("source=dhcp");
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Harness
    {
        public AdapterSnapshot State { get; }
        public List<string[]> Commands { get; } = [];
        public List<string[]> RecoveryCommands { get; } = [];
        public bool FailForwardDns { get; init; }
        public bool IgnoreForwardDns { get; init; }
        public bool FailRecovery { get; init; }
        public bool IgnoreRecovery { get; init; }
        public bool MissingOnRecovery { get; init; }
        public bool FailSave { get; init; }
        public Action? AfterForwardWrite { get; init; }
        public int RecoveryReads { get; private set; }
        public int SaveCalls { get; private set; }
        private bool _recovering;
        private int _writeCalls;
        private readonly NetworkService _service;

        public Harness(NetworkConfig initial)
        {
            State = Snapshot(initial.Clone());
            _service = new NetworkService(ReadAsync, Write,
                (_, token) => { token.ThrowIfCancellationRequested(); return Task.CompletedTask; },
                (_, _, _) =>
                {
                    SaveCalls++;
                    if (FailSave) throw new IOException("synthetic save failure");
                    return Task.CompletedTask;
                });
        }
        public Task<ApplyResult> ApplyAsync(NetworkConfig target, CancellationToken token = default) =>
            _service.ApplyAsync(State, target, OperationSource.Manual, token);

        private Task<IReadOnlyList<AdapterSnapshot>> ReadAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_recovering) RecoveryReads++;
            IReadOnlyList<AdapterSnapshot> snapshots = _recovering && MissingOnRecovery
                ? [] : [Snapshot(State.Config.Clone())];
            return Task.FromResult(snapshots);
        }
        private void Write(AdapterSnapshot before, NetworkConfig target, bool exact)
        {
            var restoring = _writeCalls++ > 0;
            _recovering = restoring;
            if (restoring && FailRecovery) throw new IOException("synthetic recovery failure");
            NetworkService.ApplyConfiguration(before, target, exact, command =>
            {
                Commands.Add(command);
                if (restoring) RecoveryCommands.Add(command);
                if (command.Contains("dnsservers"))
                {
                    if (!restoring && FailForwardDns) throw new IOException("synthetic DNS failure");
                    if ((!restoring && IgnoreForwardDns) || (restoring && IgnoreRecovery)) return;
                    State.Config.DnsMode = target.DnsMode;
                    State.Config.DnsServers = target.DnsServers.ToList();
                }
                else if (!(restoring && IgnoreRecovery))
                {
                    State.Config.IpMode = target.IpMode;
                    State.Config.IPv4Addresses = target.IPv4Addresses.Select(x => x.Clone()).ToList();
                }
            });
            if (!restoring) AfterForwardWrite?.Invoke();
        }
    }
}
