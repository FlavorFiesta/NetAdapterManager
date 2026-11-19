using NetAdapterManager.Models;
using NetAdapterManager.Services;
using NetAdapterManager.ViewModels;

internal static class Customer153Tests
{
    internal static async Task RunAsync(Action<string, string> pass, Action<string, string> fail)
    {
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        async Task Check(string name, Func<Task> action)
        {
            try { await action(); pass(name, "Synthetic saved/active configuration; no real network writes."); }
            catch (Exception ex) { fail(name, ex.ToString()); }
        }
        await Check("已保存静态配置包含多地址与网关", () =>
        {
            var saved = WindowsIPv4Settings.Parse(false, ["192.0.2.10", "192.0.2.11"],
                ["255.255.255.0", "255.255.255.255"], ["0.0.0.0", "192.0.2.1"]);
            Require(saved.IPv4Addresses.Count == 2 && saved.IPv4Addresses[0].Gateway == "192.0.2.1"
                && saved.IPv4Addresses[1].PrefixLength == 32, "Saved address set, mask or gateway lost.");
            Require(WindowsIPv4Settings.UseSavedConfiguration(false, false, saved)
                && WindowsIPv4Settings.UseSavedConfiguration(true, false, saved), "Inactive interface must retain saved settings.");
            Require(!WindowsIPv4Settings.UseSavedConfiguration(true, true, saved), "Saved settings must not hide active-state mismatches.");
            return Task.CompletedTask;
        });
        await Check("DHCP不把旧静态地址当作租约", () =>
        {
            Require(WindowsIPv4Settings.Parse(true, ["192.0.2.10"], ["255.255.255.0"], []).IPv4Addresses.Count == 0, "Stale static address surfaced as lease.");
            Require(WindowsIPv4Settings.Parse(false, ["0.0.0.0", "fe80::1"], [], []).IPv4Addresses.Count == 0, "Placeholder or IPv6 leaked into IPv4.");
            return Task.CompletedTask;
        });
        await Check("无效已保存掩码拒绝猜测", () =>
        {
            var rejected = false;
            try { WindowsIPv4Settings.Parse(false, ["192.0.2.10"], ["255.0.255.0"], []); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Invalid saved mask silently converted.");
            return Task.CompletedTask;
        });
        await Check("静态DNS取配置值不混入运行时代理", () =>
        {
            var configured = WindowsDnsSettings.ResolveConfiguration("223.5.5.5");
            Require(WindowsDnsSettings.ResolveServers(configured, ["127.0.0.1", "223.5.5.5"]).SequenceEqual(["223.5.5.5"]), "Effective proxy DNS was mixed into static settings.");
            var local = WindowsDnsSettings.ResolveConfiguration("127.0.0.1,223.5.5.5");
            Require(local.Servers.SequenceEqual(["127.0.0.1", "223.5.5.5"]), "An explicitly configured localhost resolver must be preserved.");
            Require(WindowsDnsSettings.ResolveConfiguration("192.0.2.53 192.0.2.54", "192.0.2.99").Servers.SequenceEqual(["192.0.2.53", "192.0.2.54"]), "Static DNS priority/order was lost.");
            return Task.CompletedTask;
        });
        await Check("自动DNS及配置文件DNS来源", () =>
        {
            var auto = WindowsDnsSettings.ResolveConfiguration(null);
            Require(auto.Mode == DnsMode.Automatic && WindowsDnsSettings.ResolveServers(auto, ["192.0.2.53"]).SequenceEqual(["192.0.2.53"]), "DHCP DNS not retained.");
            Require(WindowsDnsSettings.ResolveServers(auto, []).Count == 0, "Empty DNS generated a default resolver.");
            Require(WindowsDnsSettings.ResolveConfiguration(null, "192.0.2.54").Servers.SequenceEqual(["192.0.2.54"]), "Profile override was lost.");
            return Task.CompletedTask;
        });
        await Check("链路断开与管理禁用分离", () =>
        {
            Require(NetworkService.IsAdapterEnabled(0, 7) && NetworkService.IsAdapterEnabled(0, 0), "Disconnected adapter marked disabled.");
            Require(!NetworkService.IsAdapterEnabled(22, 0) && !NetworkService.IsAdapterEnabled(0, 5), "Disabled adapter marked enabled.");
            return Task.CompletedTask;
        });
        await Check("已保存来源界面提示与三语言", () =>
        {
            try
            {
                var vm = new AdapterViewModel(new AdapterSnapshot { UsesSavedIPv4Configuration = true });
                foreach (var language in new[] { "zh-CN", "zh-TW", "en" })
                {
                    Localization.SetLanguage(language);
                    Require(vm.ConfigurationStatusText == Localization.T("Network.ShowingSaved"), "Saved source label missing.");
                }
            }
            finally { Localization.SetLanguage("zh-CN"); }
            return Task.CompletedTask;
        });
        await Check("静态DNS运行时代理不会造成虚假回滚", async () =>
        {
            var state = NetworkConfig.ForPreset("192.0.2.10", 24, null, DnsMode.Automatic, []);
            var writes = 0;
            AdapterSnapshot Read()
            {
                var snapshot = new AdapterSnapshot { InterfaceGuid = "00000000-0000-0000-0000-000000000153", Enabled = true, Ipv4InterfacePresent = true, Config = state.Clone() };
                var configured = WindowsDnsSettings.ResolveConfiguration(state.DnsMode == DnsMode.Static ? string.Join(",", state.DnsServers) : null);
                snapshot.Config.DnsServers = WindowsDnsSettings.ResolveServers(configured, ["127.0.0.1", "223.5.5.5"]);
                snapshot.EffectiveDnsServers = ["127.0.0.1", "223.5.5.5"];
                return snapshot;
            }
            var network = new NetworkService(_ => Task.FromResult<IReadOnlyList<AdapterSnapshot>>([Read()]),
                (_, target, _) => { writes++; state = target.Clone(); }, (_, _) => Task.CompletedTask, (_, _, _) => Task.CompletedTask);
            var target = NetworkConfig.ForPreset("192.0.2.20", 24, null, DnsMode.Static, ["223.5.5.5"]);
            var result = await network.ApplyAsync(Read(), target, OperationSource.Manual);
            Require(result.Succeeded && !result.RolledBack && writes == 1, "Correctly saved settings caused false rollback.");
            Require(result.Warnings.Contains(Localization.T("Network.EffectiveDnsDiffers")), "Runtime resolver difference must remain visible as a warning.");
            var wrong = Read(); wrong.Config.DnsServers = ["192.0.2.99"];
            Require(!NetworkService.MatchesTarget(wrong, target, true), "A genuine DNS mismatch was accepted.");
        });
    }
}
