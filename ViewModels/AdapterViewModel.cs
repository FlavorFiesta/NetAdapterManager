using NetAdapterManager.Models;

namespace NetAdapterManager.ViewModels;

public sealed class AdapterViewModel : ObservableObject
{
    public AdapterSnapshot Snapshot { get; }
    public bool IsAbnormal => !Snapshot.Enabled || Snapshot.ConnectionStatus != "Up";
    public string AbnormalHint => EnabledText + "\n" + ConfigurationStatusText;
    public string Name => Snapshot.Name;
    public string TypeText => Snapshot.Type switch
    {
        AdapterType.Physical => L.T("Loc_A454F17872A1"),
        AdapterType.Virtual => L.T("Loc_9D82DD6C9D3D"),
        AdapterType.Vpn => "VPN",
        _ => L.T("Loc_D2909F1647E7")
    };
    public string EnabledText => !Snapshot.Enabled ? L.T("Loc_BC5A87A757A5")
        : Snapshot.ConnectionStatus == "Up" ? L.T("Adapter.EnabledConnected")
        : Snapshot.ConnectionStatus == "Disconnected" ? L.T("Adapter.EnabledDisconnected")
        : L.T("Loc_DFB802238B38") + " · " + ConnectionStatus;
    public string ConfigurationStatusText => Snapshot.UsesSavedIPv4Configuration
        ? L.T("Network.ShowingSaved")
        : Snapshot.Ipv4InterfacePresent
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
        ? $"DHCP（{(Snapshot.Config.IPv4Addresses.FirstOrDefault() is { } lease ? $"{lease.Address}/{lease.PrefixLength}" : L.T("Loc_52496E5AEFF4"))}）"
        : string.Join(", ", Snapshot.Config.IPv4Addresses.Where(x => x is not null).Select(x => $"{x.Address}/{x.PrefixLength}"));
    public string AddressDetails
    {
        get
        {
            var addresses = Snapshot.Config.IPv4Addresses.Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Address))
                .Concat(Snapshot.Config.DhcpStaticAddresses)
                .DistinctBy(a => (a.Address, a.PrefixLength));
            var details = string.Join(Environment.NewLine, addresses.Select(a =>
                $"IPv4 {a.Address}/{a.PrefixLength}  ·  {L.T("Adapter.SubnetMask")} {Mask(a.PrefixLength)}"));
            return details.Length > 0 ? details : L.T("Adapter.NoIPv4");
        }
    }
    private static string Mask(int prefix)
    {
        if (prefix is < 0 or > 32) return "—";
        var mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        return $"{mask >> 24}.{(mask >> 16) & 255}.{(mask >> 8) & 255}.{mask & 255}";
    }
    public string MacAddressText => string.IsNullOrWhiteSpace(Snapshot.MacAddress)
        ? "MAC —"
        : $"MAC {Snapshot.MacAddress}";

    // Read only the adapter snapshot: editing a draft must not change this line.
    public string ConfigurationSummary
    {
        get
        {
            var config = Snapshot.Config;
            var gateways = string.Join(", ", config.IPv4Addresses.Where(a => a is not null)
                .Select(a => a.Gateway).Where(g => !string.IsNullOrWhiteSpace(g)).Distinct());
            var servers = string.Join(", ", config.DnsServers.Where(d => !string.IsNullOrWhiteSpace(d)));
            var dns = config.DnsMode == DnsMode.Automatic
                ? L.T("Preview.Automatic") + (servers.Length > 0 ? $" ({servers})" : "")
                : servers.Length > 0 ? servers : L.T("Preview.Empty");
            var addresses = string.IsNullOrWhiteSpace(IPv4Summary) ? L.T("Adapter.NoIPv4") : IPv4Summary;
            var text = $"{MacAddressText}  ·  {addresses}  ·  {L.T("Preview.Gateway")}: {(gateways.Length > 0 ? gateways : L.T("Preview.Empty"))}  ·  DNS: {dns}";
            if (config.DhcpStaticAddresses.Count > 0)
                text += $"  ·  {L.T("Ops.Additional")}: {string.Join(", ", config.DhcpStaticAddresses.Select(a => $"{a.Address}/{a.PrefixLength}"))}";
            if (!string.IsNullOrWhiteSpace(ConfigurationStatusText)) text += $"  ·  {ConfigurationStatusText}";
            return text;
        }
    }

    public AdapterViewModel(AdapterSnapshot snapshot) => Snapshot = snapshot;
}
