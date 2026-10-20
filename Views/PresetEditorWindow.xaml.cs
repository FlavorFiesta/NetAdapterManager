using System.Windows;
using NetAdapterManager.Models;
using NetAdapterManager.Services;
using NetAdapterManager.ViewModels;

namespace NetAdapterManager.Views;

public sealed class PresetEditorResult
{
    public string Name { get; init; } = string.Empty;
    public string GroupName { get; init; } = "未分组";
    public string AdapterName { get; init; } = string.Empty;
    public string AdapterMacAddress { get; init; } = string.Empty;
    public string AdapterInterfaceGuid { get; init; } = string.Empty;
    public NetworkConfig Config { get; init; } = new();
}

public partial class PresetEditorWindow : Window
{
    private AdapterViewModel? _selectedAdapter;
    private bool _initializingAdapter;
    private string _fallbackAdapterName = string.Empty;
    private string _fallbackMacAddress = string.Empty;
    private string _fallbackInterfaceGuid = string.Empty;
    private string _autoName = string.Empty;
    public PresetEditorResult? Result { get; private set; }

    public PresetEditorWindow(string defaultName, string defaultGroup, NetworkConfig source,
        string? adapterName = null, string? macAddress = null, IReadOnlyList<string>? groups = null,
        string? interfaceGuid = null)
    {
        InitializeComponent();
        LoadGroups(groups, defaultGroup);
        SetFallbackAdapter(adapterName, macAddress, interfaceGuid);
        NameBox.Text = defaultName;
        LoadConfig(source);
    }

    public PresetEditorWindow(string defaultName, string defaultGroup, NetworkConfig source,
        IReadOnlyList<AdapterViewModel> adapters, AdapterViewModel selectedAdapter, IReadOnlyList<string>? groups = null)
    {
        InitializeComponent();
        LoadGroups(groups, defaultGroup);
        _autoName = defaultName;
        NameBox.Text = defaultName;
        LoadConfig(source);
        AdapterNameText.Visibility = Visibility.Collapsed;
        PresetAdapterSelector.Visibility = Visibility.Visible;
        _initializingAdapter = true;
        PresetAdapterSelector.ItemsSource = adapters;
        PresetAdapterSelector.SelectedItem = selectedAdapter;
        _selectedAdapter = selectedAdapter;
        UpdateAdapterDisplay(selectedAdapter);
        _initializingAdapter = false;
    }

    private void LoadGroups(IReadOnlyList<string>? groups, string defaultGroup)
    {
        var items = new[] { "未分组" }
            .Concat(groups ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x)
                        && !string.Equals(x, "全部分组", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        GroupBox.ItemsSource = items;
        var selectedGroup = string.IsNullOrWhiteSpace(defaultGroup) ? "未分组" : defaultGroup.Trim();
        GroupBox.SelectedItem = items.FirstOrDefault(x =>
            string.Equals(x, selectedGroup, StringComparison.OrdinalIgnoreCase));
        if (GroupBox.SelectedItem is null) GroupBox.Text = selectedGroup;
    }

    private void SetFallbackAdapter(string? adapterName, string? macAddress, string? interfaceGuid)
    {
        _fallbackAdapterName = string.IsNullOrWhiteSpace(adapterName) ? "未指定" : adapterName;
        _fallbackMacAddress = macAddress ?? string.Empty;
        _fallbackInterfaceGuid = interfaceGuid ?? string.Empty;
        AdapterNameText.Text = _fallbackAdapterName;
        AdapterMacText.Text = string.IsNullOrWhiteSpace(_fallbackMacAddress) ? string.Empty : $"MAC：{_fallbackMacAddress}";
    }

    private void PresetAdapterSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (PresetAdapterSelector.SelectedItem is not AdapterViewModel adapter) return;
        var previousAutoName = _autoName;
        _selectedAdapter = adapter;
        UpdateAdapterDisplay(adapter);
        if (_initializingAdapter) return;
        LoadConfig(adapter.Snapshot.Config);
        if (!string.IsNullOrWhiteSpace(previousAutoName)
            && string.Equals(NameBox.Text.Trim(), previousAutoName, StringComparison.Ordinal))
        {
            _autoName = $"{adapter.Name} - {DateTime.Now:yyyyMMdd-HHmm}";
            NameBox.Text = _autoName;
        }
    }

    private void UpdateAdapterDisplay(AdapterViewModel adapter)
    {
        AdapterMacText.Text = string.IsNullOrWhiteSpace(adapter.Snapshot.MacAddress)
            ? string.Empty
            : $"MAC：{adapter.Snapshot.MacAddress}";
    }

    private void LoadConfig(NetworkConfig config)
    {
        DhcpRadio.IsChecked = config.IpMode == IpMode.Dhcp;
        StaticRadio.IsChecked = config.IpMode == IpMode.Static;
        var managed = config.IPv4Addresses.Where(x => x is not null).FirstOrDefault(x => x.IsManaged)
            ?? config.IPv4Addresses.Where(x => x is not null).FirstOrDefault();
        IpBox.Text = managed?.Address ?? string.Empty;
        PrefixBox.Text = managed?.PrefixLength.ToString() ?? "24";
        GatewayBox.Text = managed?.Gateway ?? string.Empty;

        DnsAutomaticRadio.IsChecked = config.DnsMode == DnsMode.Automatic;
        DnsStaticRadio.IsChecked = config.DnsMode == DnsMode.Static;
        PreferredDnsBox.Text = config.DnsServers.ElementAtOrDefault(0) ?? string.Empty;
        AlternateDnsBox.Text = config.DnsServers.ElementAtOrDefault(1) ?? string.Empty;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ClearValidationMessage();
        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetValidationMessage("请输入预设名称。");
            NameBox.Focus();
            return;
        }

        var config = new NetworkConfig
        {
            IpMode = StaticRadio.IsChecked == true ? IpMode.Static : IpMode.Dhcp,
            DnsMode = DnsStaticRadio.IsChecked == true ? DnsMode.Static : DnsMode.Automatic,
            DnsServers = DnsStaticRadio.IsChecked == true
                ? [.. new[] { PreferredDnsBox.Text, AlternateDnsBox.Text }
                    .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())]
                : []
        };
        if (config.IpMode == IpMode.Static)
        {
            config.ManagedIPv4Address = IpBox.Text.Trim();
            config.IPv4Addresses = [new Ipv4AddressEntry
            {
                Address = IpBox.Text.Trim(),
                PrefixLength = ValidationService.TryParsePrefix(PrefixBox.Text, out var prefix) ? prefix : -1,
                Gateway = GatewayBox.Text.Trim(),
                IsManaged = true
            }];
        }

        var validation = ValidationService.ValidateManualConfig(config);
        if (!validation.IsValid)
        {
            SetValidationMessage(string.Join(Environment.NewLine, validation.Errors));
            if (validation.Errors.Any(x => x.Contains("DNS", StringComparison.OrdinalIgnoreCase)))
                PreferredDnsBox.Focus();
            return;
        }

        Result = new PresetEditorResult
        {
            Name = name,
            GroupName = string.IsNullOrWhiteSpace(GroupBox.Text) ? "未分组" : GroupBox.Text.Trim(),
            AdapterName = _selectedAdapter?.Name ?? _fallbackAdapterName,
            AdapterMacAddress = _selectedAdapter?.Snapshot.MacAddress ?? _fallbackMacAddress,
            AdapterInterfaceGuid = _selectedAdapter?.Snapshot.InterfaceGuid ?? _fallbackInterfaceGuid,
            Config = config
        };
        DialogResult = true;
    }

    private void SetValidationMessage(string message)
    {
        ValidationMessage.Text = message;
        ValidationMessage.Visibility = Visibility.Visible;
    }

    private void ClearValidationMessage()
    {
        ValidationMessage.Text = string.Empty;
        ValidationMessage.Visibility = Visibility.Collapsed;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }
}
