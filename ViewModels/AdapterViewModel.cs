using NetAdapterManager.Models;

namespace NetAdapterManager.ViewModels;

public sealed class AdapterViewModel : ObservableObject
{
    public AdapterSnapshot Snapshot { get; }
    public string Name => Snapshot.Name;
    public string TypeText => Snapshot.Type switch
    {
        AdapterType.Physical => L.T("Loc_A454F17872A1"),
        AdapterType.Virtual => L.T("Loc_9D82DD6C9D3D"),
        AdapterType.Vpn => "VPN",
        _ => L.T("Loc_D2909F1647E7")
    };
    public string EnabledText => Snapshot.Enabled ? L.T("Loc_DFB802238B38") : L.T("Loc_BC5A87A757A5");
    public string ConfigurationStatusText => Snapshot.Ipv4InterfacePresent
        ? string.Empty
        : Snapshot.Enabled
            ? L.T("Loc_59D57026AA67")
            : L.T("Loc_5B4C4C1CC0A2");
    public string ConnectionStatus => Snapshot.Type == AdapterType.Virtual && Snapshot.ConnectionStatus == "Not Present"
        ? "—"
        : Snapshot.ConnectionStatus switch
        {
            "Up" => L.T("Loc_5BE0323E8ADC"),
            "Disconnected" => L.T("Loc_1F0AC6953E04"),
            "Not Present" => L.T("Loc_9E665F6CCD4B"),
            "Unknown" => L.T("Loc_4D8C1C5B4283"),
            _ => Snapshot.ConnectionStatus
        };
    public string IPv4Summary => Snapshot.Config.IpMode == IpMode.Dhcp
        ? $"DHCP（{Snapshot.Config.IPv4Addresses.Where(x => x is not null).FirstOrDefault()?.Address ?? L.T("Loc_52496E5AEFF4")}）"
        : string.Join(", ", Snapshot.Config.IPv4Addresses.Where(x => x is not null).Select(x => $"{x.Address}/{x.PrefixLength}"));
    public string MacAddressText => string.IsNullOrWhiteSpace(Snapshot.MacAddress)
        ? "MAC —"
        : $"MAC {Snapshot.MacAddress}";

    public AdapterViewModel(AdapterSnapshot snapshot) => Snapshot = snapshot;
}
