using System.Windows;
using NetAdapterManager.Models;
using NetAdapterManager.Services;
using NetAdapterManager.Views;

namespace NetAdapterManager.ViewModels;

public sealed partial class MainViewModel
{
    private sealed record EditorDraft(string Ip, string Prefix, string Gateway, string Dns1, string Dns2, bool Dhcp, bool AutoDns, string Additional);
    private readonly Dictionary<string, EditorDraft> _drafts = new(StringComparer.OrdinalIgnoreCase);
    private EditorDraft? _editorBaseline;
    internal Func<AdapterSnapshot, NetworkConfig, bool>? ReviewConfiguration { get; set; }
    private EditorDraft CaptureEditor() => new(IPv4Address, PrefixInput, Gateway, PreferredDns, AlternateDns, IsDhcp, IsDnsAutomatic, AdditionalAddressesText);
    private void RestoreEditor(EditorDraft d) =>
        (IPv4Address, PrefixInput, Gateway, PreferredDns, AlternateDns, IsDhcp, IsDnsAutomatic, AdditionalAddressesText) = (d.Ip, d.Prefix, d.Gateway, d.Dns1, d.Dns2, d.Dhcp, d.AutoDns, d.Additional);
    public bool HasDraft => SelectedAdapter is not null && _editorBaseline is not null && (CaptureEditor() != _editorBaseline || DnsModeAdjusted);
    public bool DnsModeAdjusted => SelectedAdapter?.Snapshot.Config is { IpMode: IpMode.Static, DnsMode: DnsMode.Automatic } && IsStatic && IsDnsStatic;
    public string DraftStatusText => L.T(DnsModeAdjusted ? "Draft.DnsAdjusted" : HasDraft ? "Draft.Modified" : "Draft.Current");
    public string ActualConfigurationText => SelectedAdapter is null ? "" : L.T("Preview.Current") + ":\n" +
        L.T(SelectedAdapter.Snapshot.Config.IpMode == IpMode.Dhcp ? "Preview.Automatic" : "Preview.Static") + "\n" +
        string.Join("\n", SelectedAdapter.Snapshot.Config.IPv4Addresses.Select(a => $"{a.Address}/{a.PrefixLength} | {L.T("Preview.Gateway")}: {(string.IsNullOrWhiteSpace(a.Gateway) ? L.T("Preview.Empty") : a.Gateway)}")) +
        $"\nDNS: {(SelectedAdapter.Snapshot.Config.DnsMode == DnsMode.Automatic ? L.T("Preview.Automatic") : (SelectedAdapter.Snapshot.Config.DnsServers.Count == 0 ? L.T("Preview.Empty") : string.Join(", ", SelectedAdapter.Snapshot.Config.DnsServers)))}";
    private void NotifyDraft()
    {
        OnPropertyChanged(nameof(HasDraft));
        OnPropertyChanged(nameof(DnsModeAdjusted));
        OnPropertyChanged(nameof(DraftStatusText));
        OnPropertyChanged(nameof(ActualConfigurationText));
    }
    private void CacheCurrentDraft()
    {
        if (SelectedAdapter is null) return;
        var guid = SelectedAdapter.Snapshot.InterfaceGuid;
        if (HasDraft) _drafts[guid] = CaptureEditor();
        else _drafts.Remove(guid);
    }
    public RelayCommand RestoreCurrentCommand => new(() =>
    {
        if (SelectedAdapter is null) return;
        _drafts.Remove(SelectedAdapter.Snapshot.InterfaceGuid);
        LoadAdapterEditor(SelectedAdapter.Snapshot);
        _editorBaseline = CaptureEditor();
        NotifyDraft();
    }, () => CanConfigure);

    private Window? DialogOwner => Application.Current?.Windows.OfType<Window>().LastOrDefault(w => w.IsActive)
        ?? Application.Current?.MainWindow;

    private async Task ReviewAndApplyAsync(string interfaceGuid, NetworkConfig target)
    {
        var validation = ValidationService.ValidateManualConfig(target);
        if (!validation.IsValid)
        {
            ShowResult(new ApplyResult { Message = L.T("Loc_E78333A422B7"), ErrorDetail = string.Join("\n", validation.Errors) });
            return;
        }
        var reviewed = (await _networkService.GetAdaptersAsync()).SingleOrDefault(x =>
            string.Equals(x.InterfaceGuid, interfaceGuid, StringComparison.OrdinalIgnoreCase));
        if (reviewed is null) { StatusMessage = L.T("Preview.Stale"); return; }
        var confirmed = ReviewConfiguration is not null
            ? ReviewConfiguration(reviewed, target)
            : new ConfigurationPreviewWindow(reviewed, target) { Owner = DialogOwner }.ShowDialog() == true;
        if (!confirmed) return;
        ApplyResult result;
        try { result = await _networkService.ApplyReviewedAsync(reviewed, target); }
        catch (Exception ex) { result = new ApplyResult { Message = L.T("Loc_BF067030E680"), ErrorDetail = ex.Message }; }
        ShowResult(result);
        // Preset application can target a different NIC; keep the editor's selection.
        var selectedGuid = SelectedAdapter?.Snapshot.InterfaceGuid ?? interfaceGuid;
        await RefreshAfterOperationAsync(selectedGuid, preserveDraft: !result.Succeeded || selectedGuid != interfaceGuid);
        var actual = _snapshots.FirstOrDefault(s => s.InterfaceGuid == interfaceGuid) ?? result.FinalSnapshot;
        await RecordOperationAsync(new OperationRecord
        {
            Kind = "Apply", AdapterName = reviewed.Name, InterfaceGuid = reviewed.InterfaceGuid,
            MacAddress = reviewed.MacAddress, Before = reviewed.Config.Clone(),
            Target = NetworkService.BuildExpectedConfiguration(reviewed, target, false),
            Actual = actual?.Config.Clone(), ActualEnabled = actual?.Enabled,
            Succeeded = result.Succeeded, RolledBack = result.RolledBack, Verified = result.Succeeded || result.RolledBack,
            Message = result.Message, ErrorDetail = result.ErrorDetail ?? "", Warnings = string.Join("\n", result.Warnings)
        });
        if (result.Succeeded)
        {
            _drafts.Remove(interfaceGuid);
            if (SelectedAdapter?.Snapshot.InterfaceGuid == interfaceGuid)
            {
                LoadAdapterEditor(SelectedAdapter.Snapshot);
                _editorBaseline = CaptureEditor();
                NotifyDraft();
            }
        }
    }
}
