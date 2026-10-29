using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace NetAdapterManager.Models;

public sealed class Ipv4AddressEntry
{
    public string Address { get; set; } = string.Empty;
    public int PrefixLength { get; set; }
    public string? Gateway { get; set; }
    public string PrefixOrigin { get; set; } = string.Empty;
    public bool IsManaged { get; set; }

    public Ipv4AddressEntry Clone() => new()
    {
        Address = Address,
        PrefixLength = PrefixLength,
        Gateway = Gateway,
        PrefixOrigin = PrefixOrigin,
        IsManaged = IsManaged
    };
}

public sealed class NetworkConfig
{
    private List<Ipv4AddressEntry> _ipv4Addresses = [];
    private List<string> _dnsServers = [];

    public IpMode IpMode { get; set; } = IpMode.Dhcp;
    public List<Ipv4AddressEntry> IPv4Addresses
    {
        get => _ipv4Addresses;
        set => _ipv4Addresses = value ?? [];
    }
    public string? ManagedIPv4Address { get; set; }
    public DnsMode DnsMode { get; set; } = DnsMode.Automatic;
    public List<string> DnsServers
    {
        get => _dnsServers;
        set => _dnsServers = value ?? [];
    }

    public NetworkConfig Clone() => new()
    {
        IpMode = IpMode,
        IPv4Addresses = IPv4Addresses.Where(a => a is not null).Select(a => a.Clone()).ToList(),
        ManagedIPv4Address = ManagedIPv4Address,
        DnsMode = DnsMode,
        DnsServers = [.. DnsServers.Where(x => x is not null)]
    };

    public static NetworkConfig ForPreset(string address, int prefixLength, string? gateway,
        DnsMode dnsMode, IEnumerable<string> dnsServers) => new()
    {
        IpMode = IpMode.Static,
        IPv4Addresses = [new Ipv4AddressEntry
        {
            Address = address,
            PrefixLength = prefixLength,
            Gateway = gateway,
            IsManaged = true
        }],
        ManagedIPv4Address = address,
        DnsMode = dnsMode,
        DnsServers = [.. dnsServers]
    };
}

public sealed class AdapterSnapshot
{
    public string Name { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string InterfaceGuid { get; set; } = string.Empty;
    public int InterfaceIndex { get; set; }
    public AdapterType Type { get; set; }
    public bool Enabled { get; set; }
    public bool Ipv4InterfacePresent { get; set; }
    public string ConnectionStatus { get; set; } = string.Empty;
    public NetworkConfig Config { get; set; } = new();
}

public sealed class Preset : INotifyPropertyChanged
{
    public Preset() => System.ComponentModel.PropertyChangedEventManager.AddHandler(L.Instance, LanguageChanged, string.Empty);
    private void LanguageChanged(object? sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    private string _name = string.Empty;
    private string _groupName = NetAdapterManager.Models.GroupIds.Ungrouped;
    private string _adapterName = string.Empty;
    private string _adapterMacAddress = string.Empty;
    private string _adapterInterfaceGuid = string.Empty;
    private NetworkConfig _config = new();
    private DateTimeOffset _updatedAt = DateTimeOffset.Now;

    public Guid Id { get; set; } = Guid.NewGuid();
    private bool _isBatchSelected;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsBatchSelected
    {
        get => _isBatchSelected;
        set
        {
            if (_isBatchSelected == value) return;
            _isBatchSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBatchSelected)));
        }
    }
    public string Name
    {
        get => _name;
        set
        {
            var normalized = value ?? string.Empty;
            if (_name == normalized) return;
            _name = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }
    public string GroupName
    {
        get => string.IsNullOrWhiteSpace(_groupName) ? NetAdapterManager.Models.GroupIds.Ungrouped : _groupName;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? NetAdapterManager.Models.GroupIds.Ungrouped : value.Trim();
            if (_groupName == normalized) return;
            _groupName = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GroupName)));
        }
    }
    public string AdapterName
    {
        get => _adapterName;
        set
        {
            var normalized = value ?? string.Empty;
            if (_adapterName == normalized) return;
            _adapterName = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AdapterName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AdapterSummary)));
        }
    }
    public string AdapterMacAddress
    {
        get => _adapterMacAddress;
        set
        {
            var normalized = value ?? string.Empty;
            if (_adapterMacAddress == normalized) return;
            _adapterMacAddress = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AdapterMacAddress)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MacSummary)));
        }
    }
    public string AdapterInterfaceGuid
    {
        get => _adapterInterfaceGuid;
        set => _adapterInterfaceGuid = value ?? string.Empty;
    }
    public NetworkConfig Config
    {
        get => _config;
        set
        {
            _config = value ?? new NetworkConfig();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Config)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IpSummary)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DnsSummary)));
        }
    }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt
    {
        get => _updatedAt;
        set
        {
            if (_updatedAt == value) return;
            _updatedAt = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdatedAt)));
        }
    }

    public Preset Clone() => new()
    {
        Id = Id,
        Name = Name,
        GroupName = GroupName,
        AdapterName = AdapterName,
        AdapterMacAddress = AdapterMacAddress,
        AdapterInterfaceGuid = AdapterInterfaceGuid,
        Config = Config.Clone(),
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt
    };

    [JsonIgnore]
    public string IpSummary => Config.IpMode == IpMode.Dhcp
        ? L.T("Loc_168F008AB7BD")
        : $"IPv4：{string.Join("，", Config.IPv4Addresses.Where(x => x is not null).Select(x =>
            string.IsNullOrWhiteSpace(x.Gateway)
                ? $"{x.Address}/{x.PrefixLength}"
                : L.F("Loc_12C8640F2F18", x.Address, x.PrefixLength, x.Gateway)))}";

    [JsonIgnore]
    public string DnsSummary => Config.DnsMode == DnsMode.Automatic
        ? L.T("Loc_36A84A014003")
        : $"DNS：{string.Join("，", Config.DnsServers.Where(x => x is not null))}";

    [JsonIgnore]
    public string AdapterSummary => string.IsNullOrWhiteSpace(AdapterName) ? L.T("Loc_877E25224243") : L.F("Loc_E6BD4D087F2A", AdapterName);

    [JsonIgnore]
    public string MacSummary => string.IsNullOrWhiteSpace(AdapterMacAddress)
        ? string.Empty
        : $"MAC：{AdapterMacAddress}";

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class ApplyResult
{
    public bool Succeeded { get; init; }
    public bool RolledBack { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? ErrorDetail { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public AdapterSnapshot? FinalSnapshot { get; init; }
}

public sealed class ValidationResult
{
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public bool IsValid => Errors.Count == 0;
}
