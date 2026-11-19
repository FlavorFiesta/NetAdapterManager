using NetAdapterManager.Models;
using NetAdapterManager.Services;
using NetAdapterManager.ViewModels;

internal static class Version163Tests
{
    internal static void Run(Action<string, string> pass, Action<string, string> fail)
    {
        try
        {
            var config = NetworkConfig.ForPreset("192.0.2.10", 24, null, DnsMode.Automatic, []);
            var vm = new MainViewModel();
            vm.SelectedAdapter = new AdapterViewModel(new AdapterSnapshot { Name = "Demo", InterfaceGuid = "ui-test", Config = config });
            if (!vm.DnsModeAdjusted || !vm.HasDraft || !vm.IsDnsStatic || config.DnsMode != DnsMode.Automatic)
                throw new Exception("Editor normalization must be shown as pending without changing the real snapshot.");
            vm.IPv4Address = "192.0.2.99";
            if (!vm.SelectedAdapter.ConfigurationSummary.Contains("192.0.2.10/24")
                || vm.SelectedAdapter.ConfigurationSummary.Contains("192.0.2.99"))
                throw new Exception("Actual adapter summary must remain independent of the draft.");
            foreach (var language in new[] { "zh-CN", "zh-TW", "en" })
            {
                Localization.SetLanguage(language);
                if (vm.DraftStatusText != Localization.T("Draft.DnsAdjusted")) throw new Exception("Missing adjusted-DNS status.");
                var preset = new Preset { Config = NetworkConfig.ForPreset("192.0.2.10", 24, null, DnsMode.Static, []) };
                if (!preset.DnsSummary.Contains(Localization.T("Ops.NoDns"))) throw new Exception("Empty static DNS lacks a label.");
                var adapter = new AdapterViewModel(new AdapterSnapshot { MacAddress = "00:11:22:33:44:55",
                    UsesSavedIPv4Configuration = true, Config = NetworkConfig.ForPreset("192.0.2.10", 24, "192.0.2.1", DnsMode.Static, ["192.0.2.53"]) });
                if (!adapter.ConfigurationSummary.Contains("192.0.2.1") || !adapter.ConfigurationSummary.Contains("192.0.2.53")
                    || !adapter.ConfigurationSummary.Contains(Localization.T("Network.ShowingSaved")))
                    throw new Exception("Adapter summary must include gateway, DNS and saved-state provenance.");
            }
            if (vm.ExportPresetsCommand.CanExecute(null) || vm.ExportPresetsCommand.CanExecute("batch"))
                throw new Exception("Empty selections must disable export.");
            vm.SelectedPreset = new Preset();
            if (!vm.ExportPresetsCommand.CanExecute(null) || vm.ExportPresetsCommand.CanExecute("batch"))
                throw new Exception("Single selection must be distinct from checked selection.");
            pass("1.6.3 DNS状态与选择语义", "Adjusted DNS remains pending; empty DNS and single/bulk export states are explicit.");
        }
        catch (Exception ex) { fail("1.6.3 DNS状态与选择语义", ex.ToString()); }
        finally { Localization.SetLanguage("zh-CN"); }
    }
}
