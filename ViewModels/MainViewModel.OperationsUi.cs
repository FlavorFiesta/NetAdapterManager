using System.Windows;
using NetAdapterManager.Models;
using NetAdapterManager.Services;
namespace NetAdapterManager.ViewModels;
public sealed partial class MainViewModel
{
    private string _additionalAddressesText = "";
    public string AdditionalAddressesText { get => _additionalAddressesText; set { if (SetProperty(ref _additionalAddressesText, value)) NotifyDraft(); } }
    public bool IsAdministrator => _networkService.CanWrite;
    public string PrivilegeText => L.T(IsAdministrator ? "Ops.Admin" : "Ops.ReadOnly");
    public string ActualConfigurationTitle => L.T(SelectedAdapter?.Snapshot.UsesSavedIPv4Configuration == true ? "Ops.SavedConfiguration" : "Ops.LiveConfiguration");
    public bool IsCrossAdapter => SelectedAdapter is not null && SelectedPreset is not null && HasBinding(SelectedPreset) && !BindingMatches(SelectedPreset, SelectedAdapter.Snapshot);
    public string CrossAdapterText => !IsCrossAdapter ? "" : L.F("Ops.CrossWarning", SelectedAdapter!.Name, SelectedPreset!.AdapterName);
    internal static bool HasBinding(Preset p) => !string.IsNullOrWhiteSpace(p.AdapterInterfaceGuid) || !string.IsNullOrWhiteSpace(p.AdapterMacAddress) || !string.IsNullOrWhiteSpace(p.AdapterName);
    internal static bool BindingMatches(Preset p, AdapterSnapshot a) => !string.IsNullOrWhiteSpace(p.AdapterInterfaceGuid)
        ? string.Equals(p.AdapterInterfaceGuid.Trim('{','}'), a.InterfaceGuid.Trim('{','}'), StringComparison.OrdinalIgnoreCase)
        : !string.IsNullOrWhiteSpace(p.AdapterMacAddress)
        ? string.Equals(p.AdapterMacAddress.Replace(":", "").Replace("-", ""), a.MacAddress.Replace(":", "").Replace("-", ""), StringComparison.OrdinalIgnoreCase)
        : !string.IsNullOrWhiteSpace(p.AdapterName) && p.AdapterName == a.Name;
    private void NotifyTemplateTarget()
    {
        foreach (var p in Presets) p.MatchesSelectedAdapter = SelectedAdapter is not null && BindingMatches(p, SelectedAdapter.Snapshot);
        OnPropertyChanged(nameof(IsCrossAdapter)); OnPropertyChanged(nameof(CrossAdapterText)); OnPropertyChanged(nameof(ActualConfigurationTitle));
    }
    internal static List<Ipv4AddressEntry> ParseAdditionalAddresses(string text) => text.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line => { var pieces = line.Split('/'); return new Ipv4AddressEntry { Address = pieces[0].Trim(), PrefixLength = pieces.Length == 2 && int.TryParse(pieces[1], out var n) ? n : -1, PrefixOrigin = "Manual" }; }).ToList();
}
