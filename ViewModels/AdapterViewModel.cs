using NetAdapterManager.Models;

namespace NetAdapterManager.ViewModels;

public sealed class AdapterViewModel : ObservableObject
{
    public AdapterSnapshot Snapshot { get; }
    public string Name => Snapshot.Name;
    public string TypeText => Snapshot.Type switch
    {
        AdapterType.Physical => "物理",
        AdapterType.Virtual => "虚拟",
        AdapterType.Vpn => "VPN",
        _ => "其他"
    };
    public string EnabledText => Snapshot.Enabled ? "已启用" : "已禁用";
    public string ConfigurationStatusText => Snapshot.Ipv4InterfacePresent
        ? string.Empty
        : Snapshot.Enabled
            ? "当前尚无 IPv4 地址，仍可直接写入系统网络连接配置"
            : "网卡当前已禁用，仍可写入系统网络连接配置；启用后即可使用";
    public string ConnectionStatus => Snapshot.Type == AdapterType.Virtual && Snapshot.ConnectionStatus == "Not Present"
        ? "—"
        : Snapshot.ConnectionStatus switch
        {
            "Up" => "已连接",
            "Disconnected" => "已断开",
            "Not Present" => "未插入",
            "Unknown" => "未知",
            _ => Snapshot.ConnectionStatus
        };
    public string IPv4Summary => Snapshot.Config.IpMode == IpMode.Dhcp
        ? $"DHCP（{Snapshot.Config.IPv4Addresses.Where(x => x is not null).FirstOrDefault()?.Address ?? "未分配"}）"
        : string.Join(", ", Snapshot.Config.IPv4Addresses.Where(x => x is not null).Select(x => $"{x.Address}/{x.PrefixLength}"));
    public string MacAddressText => string.IsNullOrWhiteSpace(Snapshot.MacAddress)
        ? "MAC —"
        : $"MAC {Snapshot.MacAddress}";

    public AdapterViewModel(AdapterSnapshot snapshot) => Snapshot = snapshot;
}
