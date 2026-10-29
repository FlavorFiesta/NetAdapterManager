using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using NetAdapterManager.Models;
using NetAdapterManager.Services;
using NetAdapterManager.Views;

namespace NetAdapterManager.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly NetworkService _networkService;
    private readonly PresetService _presetService;
    private readonly PresetGroupService _presetGroupService;
    private TaskCompletionSource? _activeOperation;
    private TaskCompletionSource? _presetOperation;
    internal Task ActiveOperation => Task.WhenAll(_activeOperation?.Task ?? Task.CompletedTask,
        _presetOperation?.Task ?? Task.CompletedTask);
    internal bool HasPendingOperation => IsBusy || _isPresetDialogOpen;
    private List<Preset> _allPresets = [];
    private List<string> _customPresetGroups = [];
    private bool _hideVirtual;
    private AdapterViewModel? _selectedAdapter;
    private Preset? _selectedPreset;
    private string _ipv4Address = string.Empty;
    private string _prefixInput = "24";
    private string _gateway = string.Empty;
    private string _preferredDns = string.Empty;
    private string _alternateDns = string.Empty;
    private bool _isDhcp = true;
    private bool _isDnsAutomatic = true;
    private bool _isBusy;
    private bool _isTopmost;
    private bool _isPresetDialogOpen;
    private bool _isRefreshingPresetGroups;
    private string _statusMessage = L.T("Loc_9866F869B0BD");
    private string _detailMessage = string.Empty;
    private string _warningMessage = string.Empty;
    private CancellationTokenSource? _toastCancellation;
    private readonly SemaphoreSlim _presetStorageGate = new(1, 1);
    private string _toastTitle = string.Empty;
    private string _toastDetail = string.Empty;
    private bool _isToastVisible;
    private bool _toastSucceeded = true;
    private string _presetSearchText = string.Empty;
    private string _presetGroupFilter = NetAdapterManager.Models.GroupIds.All;
    private DateTimeOffset? _lastRefresh;
    private string _presetLoadError = string.Empty;

    public ObservableCollection<AdapterViewModel> Adapters { get; } = [];
    public ObservableCollection<Preset> Presets { get; } = [];
    public ObservableCollection<string> PresetGroups { get; } = [];
    public IReadOnlyList<Preset> BatchSelectedPresets => _allPresets.Where(x => x.IsBatchSelected).ToList();

    public void ClearBatchSelection()
    {
        foreach (var preset in _allPresets) preset.IsBatchSelected = false;
    }

    public async Task<bool> UpdatePresetBatchAsync(IEnumerable<Guid> ids, string? destinationGroup, bool delete)
    {
        if (_isPresetDialogOpen || IsBusy) return false;
        var selectedIds = ids.ToHashSet();
        var affected = _allPresets.Where(x => selectedIds.Contains(x.Id)).ToList();
        if (affected.Count == 0) return false;
        var group = NormalizeGroupName(destinationGroup);
        if (!delete && (group == NetAdapterManager.Models.GroupIds.All || !GetEditablePresetGroups().Contains(group, StringComparer.OrdinalIgnoreCase)))
        {
            StatusMessage = L.T("Loc_C099B2F83340");
            return false;
        }
        SetPresetDialogOpen(true);
        await _presetStorageGate.WaitAsync();
        try
        {
            // Save a detached batch before replacing the live collection. A failed
            // write leaves both the original records and selection intact.
            var updated = _allPresets.Where(x => !delete || !selectedIds.Contains(x.Id))
                .Select(x =>
                {
                    if (!selectedIds.Contains(x.Id)) return x;
                    var copy = x.Clone();
                    copy.GroupName = group;
                    copy.UpdatedAt = DateTimeOffset.Now;
                    return copy;
                }).ToList();
            await _presetService.SaveAsync(updated);
            var selectedId = SelectedPreset?.Id;
            _allPresets = updated;
            ClearBatchSelection();
            RefreshPresetGroups();
            RefreshPresetFilter();
            SelectedPreset = Presets.FirstOrDefault(x => x.Id == selectedId);
            StatusMessage = delete ? L.F("Loc_9AFFA6F3315D", affected.Count) : L.F("Loc_5C8E61F5D03B", affected.Count, group);
            DetailMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("批量处理预设失败", ex);
            StatusMessage = L.T("Loc_6D341549BDE6");
            DetailMessage = ex.Message;
            ShowToast(L.T("Loc_D92D34B6925D"), FirstLine(ex.Message), false);
            return false;
        }
        finally
        {
            _presetStorageGate.Release();
            SetPresetDialogOpen(false);
        }
    }
    public bool HasAdapters => Adapters.Count > 0;

    public AdapterViewModel? SelectedAdapter
    {
        get => _selectedAdapter;
        set
        {
            if (!SetProperty(ref _selectedAdapter, value)) return;
            if (value is not null) LoadAdapterEditor(value.Snapshot);
            OnPropertyChanged(nameof(CanOperate));
            OnPropertyChanged(nameof(CanConfigure));
            OnPropertyChanged(nameof(CanApplyPreset));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public Preset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (!SetProperty(ref _selectedPreset, value)) return;
            OnPropertyChanged(nameof(CanApplyPreset));
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public bool HideVirtual { get => _hideVirtual; set { if (SetProperty(ref _hideVirtual, value)) ApplyAdapterFilter(); } }
    public string IPv4Address { get => _ipv4Address; set => SetProperty(ref _ipv4Address, value); }
    public string PrefixInput { get => _prefixInput; set => SetProperty(ref _prefixInput, value); }
    public string Gateway { get => _gateway; set => SetProperty(ref _gateway, value); }
    public string PreferredDns { get => _preferredDns; set => SetProperty(ref _preferredDns, value); }
    public string AlternateDns { get => _alternateDns; set => SetProperty(ref _alternateDns, value); }
    public bool IsDhcp
    {
        get => _isDhcp;
        set
        {
            if (!SetProperty(ref _isDhcp, value)) return;
            OnPropertyChanged(nameof(IsStatic));
        }
    }
    public bool IsStatic { get => !IsDhcp; set { if (value) IsDhcp = false; } }
    public bool IsDnsAutomatic
    {
        get => _isDnsAutomatic;
        set
        {
            var normalized = value;
            if (SetProperty(ref _isDnsAutomatic, normalized)) OnPropertyChanged(nameof(IsDnsStatic));
        }
    }
    public bool IsDnsStatic { get => !IsDnsAutomatic; set { if (value) IsDnsAutomatic = false; } }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(CanOperate));
            OnPropertyChanged(nameof(CanConfigure));
            OnPropertyChanged(nameof(CanApplyPreset));
            OnPropertyChanged(nameof(ConfigurationNoticeText));
            CommandManager.InvalidateRequerySuggested();
        }
    }
    public bool CanOperate => !IsBusy && SelectedAdapter is not null;
    public bool CanConfigure => CanOperate;
    private L.Message? _statusMessageTemplate;
    public string StatusMessage { get => _statusMessageTemplate?.Render() ?? _statusMessage; private set { _statusMessageTemplate = L.Capture(value); SetProperty(ref _statusMessage, value); } }
    private L.Message? _detailMessageTemplate;
    public string DetailMessage { get => _detailMessageTemplate?.Render() ?? _detailMessage; private set { _detailMessageTemplate = L.Capture(value); SetProperty(ref _detailMessage, value); } }
    private L.Message? _warningMessageTemplate;
    public string WarningMessage { get => _warningMessageTemplate?.Render() ?? _warningMessage; private set { _warningMessageTemplate = L.Capture(value); SetProperty(ref _warningMessage, value); } }
    public string ConfigurationNoticeText => IsBusy
        ? L.T("Loc_22159535DDE4")
        : L.T("Loc_5088ACBDB947");
    private L.Message? _toastTitleTemplate;
    public string ToastTitle { get => _toastTitleTemplate?.Render() ?? _toastTitle; private set { _toastTitleTemplate = L.Capture(value); SetProperty(ref _toastTitle, value); } }
    private L.Message? _toastDetailTemplate;
    public string ToastDetail { get => _toastDetailTemplate?.Render() ?? _toastDetail; private set { _toastDetailTemplate = L.Capture(value); SetProperty(ref _toastDetail, value); } }
    public bool IsToastVisible { get => _isToastVisible; private set => SetProperty(ref _isToastVisible, value); }
    public bool ToastSucceeded { get => _toastSucceeded; private set => SetProperty(ref _toastSucceeded, value); }
    public string PresetSearchText
    {
        get => _presetSearchText;
        set
        {
            if (SetProperty(ref _presetSearchText, value)) RefreshPresetFilter();
        }
    }
    public bool CanSavePreset => CanOperate && !_isPresetDialogOpen;
    public string PresetCountText => string.IsNullOrWhiteSpace(PresetSearchText) && PresetGroupFilter == NetAdapterManager.Models.GroupIds.All
        ? L.F("Loc_CE0AE0BBF7A9", _allPresets.Count)
        : L.F("Loc_414321860581", Presets.Count, _allPresets.Count);
    public string PresetGroupFilter
    {
        get => _presetGroupFilter;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? NetAdapterManager.Models.GroupIds.All : value;
            if (SetProperty(ref _presetGroupFilter, normalized))
            {
                if (!_isRefreshingPresetGroups) RefreshPresetFilter();
            }
            else if (string.IsNullOrWhiteSpace(value))
            {
                OnPropertyChanged(nameof(PresetGroupFilter));
            }
        }
    }
    public string VersionText => L.F("Loc_CE0744E60649", typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.5.2");
    public string LastRefreshText => _lastRefresh is null ? L.T("Loc_712B0B1B0D2A") : L.F("Loc_6CC762C85611", _lastRefresh);
    public bool IsTopmost
    {
        get => _isTopmost;
        private set
        {
            if (!SetProperty(ref _isTopmost, value)) return;
            OnPropertyChanged(nameof(TopmostToggleText));
            OnPropertyChanged(nameof(TopmostIconText));
        }
    }
    public string TopmostToggleText => IsTopmost ? L.T("Loc_479C565AB721") : L.T("Loc_A63886DD9F49");
    public string TopmostIconText => IsTopmost ? "\uE77A" : "\uE718";
    public string ExistingAddressesText => SelectedAdapter is null
        ? L.T("Loc_2E58832A7E2F")
        : string.Join(Environment.NewLine, SelectedAdapter.Snapshot.Config.IPv4Addresses.Where(x => x is not null).Select(x =>
            $"{x.Address}/{x.PrefixLength}{(x.IsManaged ? L.T("Loc_27A2B6F9D1E2") : L.T("Loc_805220E39FB6"))}"));

    public RelayCommand RefreshCommand => new(() => _ = RefreshAsync());
    public RelayCommand ApplyCommand => new(ApplyAsync, () => CanConfigure);
    public RelayCommand ToggleAdapterCommand => new(ToggleAdapterAsync,
        parameter => !IsBusy && (parameter is AdapterViewModel || SelectedAdapter is not null));
    public RelayCommand ToggleTopmostCommand => new(() => IsTopmost = !IsTopmost);
    public RelayCommand SavePresetCommand => new(SavePresetAsync, () => CanSavePreset);
    public RelayCommand EditPresetCommand => new(EditPresetAsync, () => SelectedPreset is not null);
    public RelayCommand ApplyPresetCommand => new(
        ApplyPresetAsync,
        parameter => !IsBusy
            && (parameter as Preset ?? SelectedPreset) is { } preset
            && ResolvePresetAdapter(preset) is not null);
    public RelayCommand DeletePresetCommand => new(DeletePresetAsync, () => SelectedPreset is not null);
    public RelayCommand AddPresetGroupCommand => new(AddPresetGroupAsync, () => !IsBusy && !_isPresetDialogOpen);
    public RelayCommand DeletePresetGroupCommand => new(DeletePresetGroupAsync, () => !IsBusy && !_isPresetDialogOpen);
    public RelayCommand RestorePresetBackupCommand => new(RestorePresetBackupAsync, () => !IsBusy && !_isPresetDialogOpen);
    public RelayCommand OpenNetworkConnectionsCommand => new(OpenNetworkConnections);
    public RelayCommand ViewAllPresetsCommand => new(ShowAllPresets);
    public RelayCommand ClearPresetFilterCommand => new(ResetPresetFilter);
    public bool CanApplyPreset => !IsBusy && SelectedPreset is not null && ResolvePresetAdapter(SelectedPreset) is not null;

    public MainViewModel(NetworkService? networkService = null, PresetService? presetService = null)
    {
        _statusMessageTemplate = L.Capture(_statusMessage);
        _networkService = networkService ?? new NetworkService();
        _presetService = presetService ?? new PresetService();
        _presetGroupService = new PresetGroupService(_presetService);
    }

    public async Task InitializeAsync()
    {
        try
        {
            _presetLoadError = string.Empty;
            _allPresets = await _presetService.LoadAsync() ?? [];
            _customPresetGroups = await _presetGroupService.LoadAsync() ?? [];
            RefreshPresetGroups();
            RefreshPresetFilter();
        }
        catch (Exception ex)
        {
            // 旧版本或手工修改的 JSON 不应让启动线程未处理异常而闪退。
            AppLogger.Error("启动时读取预设数据失败", ex);
            _allPresets = [];
            _customPresetGroups = [];
            RefreshPresetGroups();
            RefreshPresetFilter();
            StatusMessage = L.T("Loc_B9A97B8476A0");
            DetailMessage = ex.Message;
            _presetLoadError = ex.Message;
        }
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        await RunBusyAsync(async () =>
        {
            var previousGuid = SelectedAdapter?.Snapshot.InterfaceGuid;
            var snapshots = await _networkService.GetAdaptersAsync();
            ApplyAdapterFilter(snapshots);
            SelectedAdapter = Adapters.FirstOrDefault(a => a.Snapshot.InterfaceGuid == previousGuid) ?? Adapters.FirstOrDefault();
            _lastRefresh = DateTimeOffset.Now;
            OnPropertyChanged(nameof(LastRefreshText));
            StatusMessage = L.F("Loc_A85CC4FEE060", snapshots.Count);
            DetailMessage = _presetLoadError;
            if (!string.IsNullOrEmpty(_presetLoadError))
                StatusMessage = L.Join("", StatusMessage, L.T("Loc_6C97C280234D"));
        });
    }

    private async void ApplyAsync()
    {
        if (SelectedAdapter is null) return;
        var adapter = SelectedAdapter.Snapshot;
        await RunBusyAsync(async () =>
        {
            var target = BuildEditorConfig();
            var result = await _networkService.ApplyAsync(adapter, target, OperationSource.Manual);
            ShowResult(result);
            await RefreshAfterOperationAsync(adapter.InterfaceGuid);
        });
    }

    private async void ToggleAdapterAsync(object? parameter)
    {
        var adapter = parameter as AdapterViewModel ?? SelectedAdapter;
        if (adapter is null) return;
        var wasEnabled = adapter.Snapshot.Enabled;
        var action = wasEnabled ? L.T("Loc_7DF5C456C765") : L.T("Loc_F4F0EAD1116B");
        var warning = wasEnabled
            ? L.F("Loc_4C58F4424982", adapter.Name)
            : L.F("Loc_A43DB09AF546", adapter.Name);
        if (MessageBox.Show(warning, L.F("Loc_0EE51E73FEAF", action), MessageBoxButton.YesNo,
                MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        await RunBusyAsync(async () =>
        {
            var result = await _networkService.SetAdapterEnabledAsync(adapter.Snapshot, !wasEnabled);
            StatusMessage = result.Succeeded ? (wasEnabled ? L.T("Loc_2B54E2F5FC7F") : L.T("Loc_BBA6C8072457")) : L.T("Loc_57C305B2D7C1");
            DetailMessage = result.ErrorDetail ?? string.Empty;
            if (!result.Succeeded)
                AppLogger.Error($"网卡启停失败：{adapter.Name}；{result.ErrorDetail}");
            await RefreshAfterOperationAsync(adapter.Snapshot.InterfaceGuid);
        });
    }

    private async void SavePresetAsync()
    {
        if (SelectedAdapter is null || _isPresetDialogOpen) return;
        SetPresetDialogOpen(true);
        try
        {
            var defaultName = $"{SelectedAdapter.Name} - {DateTime.Now:yyyyMMdd-HHmm}";
            var draft = ShowNewPresetEditor(defaultName, NetAdapterManager.Models.GroupIds.Ungrouped, BuildEditorConfig(), SelectedAdapter);
            if (draft is null) return;
            await AddPresetAsync(draft);
        }
        catch (Exception ex)
        {
            AppLogger.Error("保存预设失败", ex);
            StatusMessage = L.T("Loc_C63B5353CC4C");
            DetailMessage = ex.Message;
            ShowToast(L.T("Loc_CC7BF06F0036"), FirstLine(ex.Message), false);
        }
        finally
        {
            SetPresetDialogOpen(false);
        }
    }

    private async void ApplyPresetAsync(object? parameter)
    {
        var preset = parameter as Preset ?? SelectedPreset;
        var target = preset is null ? null : ResolvePresetAdapter(preset);
        if (preset is null || target is null)
        {
            StatusMessage = L.T("Loc_8E3D9B6C8224");
            DetailMessage = L.T("Loc_34DDAB978344");
            AppLogger.Error($"预设应用目标解析失败：预设={preset?.Name ?? "未选择"}，记录网卡={preset?.AdapterName ?? "未记录"}，接口={preset?.AdapterInterfaceGuid ?? "未记录"}");
            ShowToast(L.T("Loc_589CFA1FD15B"), L.T("Loc_CF814777A239"), false);
            return;
        }
        var targetSnapshot = target.Snapshot;
        AppLogger.Info($"开始应用预设“{preset.Name}”，目标网卡={target.Name}，接口={targetSnapshot.InterfaceGuid}");
        await RunBusyAsync(async () =>
        {
            var result = await _networkService.ApplyAsync(targetSnapshot, preset.Config.Clone(), OperationSource.Preset);
            ShowResult(result);
            await RefreshAfterOperationAsync(targetSnapshot.InterfaceGuid, preserveDraft: !result.Succeeded);
        });
    }

    private async void EditPresetAsync()
    {
        var preset = SelectedPreset;
        if (preset is null || _isPresetDialogOpen) return;
        SetPresetDialogOpen(true);
        var originalEntries = _allPresets.ToList();
        var originalIndex = _allPresets.IndexOf(preset);
        if (originalIndex < 0)
        {
            SetPresetDialogOpen(false);
            return;
        }
        try
        {
            var draft = ShowPresetEditor(preset.Name, preset.GroupName, preset.Config,
                preset.AdapterName, preset.AdapterMacAddress, preset.AdapterInterfaceGuid);
            if (draft is null) return;

            // Edit a detached object first. The live list is replaced only
            // after the draft is valid, avoiding half-mutated items during a
            // second edit or a collection refresh.
            var updated = new Preset
            {
                Id = preset.Id,
                CreatedAt = preset.CreatedAt,
                Name = draft.Name.Trim(),
                GroupName = NormalizeGroupName(draft.GroupName),
                AdapterName = string.IsNullOrWhiteSpace(draft.AdapterName) ? preset.AdapterName : draft.AdapterName,
                AdapterMacAddress = string.IsNullOrWhiteSpace(draft.AdapterMacAddress) ? preset.AdapterMacAddress : draft.AdapterMacAddress,
                AdapterInterfaceGuid = string.IsNullOrWhiteSpace(draft.AdapterInterfaceGuid) ? preset.AdapterInterfaceGuid : draft.AdapterInterfaceGuid,
                Config = draft.Config.Clone(),
                UpdatedAt = DateTimeOffset.Now
            };
            _allPresets[originalIndex] = updated;
            RefreshPresetGroups();
            RefreshPresetFilter();
            await SavePresetsAsync();
            SelectedPreset = _allPresets.FirstOrDefault(x => x.Id == updated.Id) ?? updated;
            StatusMessage = L.F("Loc_C543A79350FA", updated.Name);
            DetailMessage = string.Empty;
        }
        catch (Exception ex)
        {
            _allPresets.Clear();
            _allPresets.AddRange(originalEntries);
            RefreshPresetGroups();
            RefreshPresetFilter();
            SelectedPreset = originalEntries.ElementAtOrDefault(originalIndex);
            AppLogger.Error("编辑预设失败", ex);
            StatusMessage = L.T("Loc_4364391EDF8A");
            DetailMessage = ex.Message;
            ShowToast(L.T("Loc_61E68035F1EA"), FirstLine(ex.Message), false);
        }
        finally
        {
            SetPresetDialogOpen(false);
        }
    }

    private async void DeletePresetAsync()
    {
        if (_isPresetDialogOpen || IsBusy) return;
        var preset = SelectedPreset;
        if (preset is null) return;
        var index = _allPresets.IndexOf(preset);
        if (index < 0) return;
        SetPresetDialogOpen(true);
        _allPresets.RemoveAt(index);
        SelectedPreset = null;
        RefreshPresetGroups();
        RefreshPresetFilter();
        try
        {
            await SavePresetsAsync();
            StatusMessage = L.T("Loc_E86E91912EF0");
            DetailMessage = string.Empty;
        }
        catch (Exception ex)
        {
            _allPresets.Insert(Math.Clamp(index, 0, _allPresets.Count), preset);
            RefreshPresetGroups();
            RefreshPresetFilter();
            SelectedPreset = preset;
            AppLogger.Error("删除预设失败", ex);
            StatusMessage = L.T("Loc_84FDB03C6159");
            DetailMessage = ex.Message;
            ShowToast(L.T("Loc_E72038175629"), FirstLine(ex.Message), false);
        }
        finally { SetPresetDialogOpen(false); }
    }

    private void OpenNetworkConnections()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "control.exe",
                Arguments = "ncpa.cpl",
                UseShellExecute = true
            });
            StatusMessage = L.T("Loc_626C45722D06");
            DetailMessage = string.Empty;
        }
        catch (Exception ex)
        {
            AppLogger.Error("打开系统网络连接失败", ex);
            StatusMessage = L.T("Loc_25D900D90FDE");
            DetailMessage = ex.Message;
        }
    }

    private void ShowAllPresets()
    {
        try
        {
            ResetPresetFilter();
            var dialog = new PresetOverviewWindow(this)
            {
                Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => ReferenceEquals(w.DataContext, this))
                        ?? Application.Current?.MainWindow
            };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            AppLogger.Error("打开预设管理失败", ex);
            StatusMessage = L.T("Loc_EC709D151400");
            DetailMessage = ex.Message;
            ShowToast(L.T("Loc_DE487746F02D"), FirstLine(ex.Message), false);
        }
    }

    private NetworkConfig BuildEditorConfig()
    {
        var result = new NetworkConfig
        {
            IpMode = IsDhcp ? IpMode.Dhcp : IpMode.Static,
            DnsMode = IsDnsAutomatic ? DnsMode.Automatic : DnsMode.Static,
            DnsServers = IsDnsAutomatic
                ? []
                : [.. new[] { PreferredDns, AlternateDns }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())]
        };
        if (!IsDhcp)
        {
            var prefix = ValidationService.TryParsePrefix(PrefixInput, out var parsed) ? parsed : -1;
            result.ManagedIPv4Address = IPv4Address.Trim();
            result.IPv4Addresses = [new Ipv4AddressEntry { Address = IPv4Address.Trim(), PrefixLength = prefix, Gateway = Gateway.Trim(), IsManaged = true }];
        }
        return result;
    }

    private void LoadAdapterEditor(AdapterSnapshot snapshot)
    {
        LoadConfigEditor(snapshot.Config, snapshot.Config.IPv4Addresses.Where(a => a is not null).FirstOrDefault(a => a.IsManaged)
            ?? snapshot.Config.IPv4Addresses.Where(a => a is not null).FirstOrDefault());
        OnPropertyChanged(nameof(ExistingAddressesText));
    }

    private void LoadConfigEditor(NetworkConfig config, Ipv4AddressEntry? preferredAddress = null)
    {
        IsDhcp = config.IpMode == IpMode.Dhcp;
        IsDnsAutomatic = config.DnsMode == DnsMode.Automatic;
        var managed = preferredAddress ?? config.IPv4Addresses.Where(a => a is not null).FirstOrDefault(a => a.IsManaged)
            ?? config.IPv4Addresses.Where(a => a is not null).FirstOrDefault();
        IPv4Address = managed?.Address ?? string.Empty;
        PrefixInput = managed?.PrefixLength.ToString() ?? "24";
        Gateway = managed?.Gateway ?? string.Empty;
        PreferredDns = config.DnsServers.ElementAtOrDefault(0) ?? string.Empty;
        AlternateDns = config.DnsServers.ElementAtOrDefault(1) ?? string.Empty;
    }

    private void ShowResult(ApplyResult result)
    {
        StatusMessage = result.Message;
        DetailMessage = result.ErrorDetail ?? string.Empty;
        WarningMessage = L.Join(Environment.NewLine, result.Warnings.ToArray());
        var detail = result.Succeeded ? result.Message : result.ErrorDetail ?? result.Message;
        if (result.Succeeded)
            AppLogger.Info($"网络配置成功：{result.Message}");
        else
            AppLogger.Error($"网络配置失败：{result.Message}；{result.ErrorDetail}");
        ShowToast(result.Succeeded ? L.T("Loc_C1BDEB148F36") : L.T("Loc_39C0C3D5A3C3"), FirstLine(detail), result.Succeeded);
    }

    private async Task RefreshSilentlyAsync(string interfaceGuid)
    {
        var snapshots = await _networkService.GetAdaptersAsync();
        ApplyAdapterFilter(snapshots);
        SelectedAdapter = Adapters.FirstOrDefault(x => x.Snapshot.InterfaceGuid == interfaceGuid) ?? Adapters.FirstOrDefault();
        _lastRefresh = DateTimeOffset.Now;
        OnPropertyChanged(nameof(LastRefreshText));
    }

    private async Task RefreshAfterOperationAsync(string interfaceGuid, bool preserveDraft = true)
    {
        var draftGuid = SelectedAdapter?.Snapshot.InterfaceGuid;
        var draft = (IPv4Address, PrefixInput, Gateway, PreferredDns, AlternateDns, IsDhcp, IsDnsAutomatic);
        try { await RefreshSilentlyAsync(interfaceGuid); }
        catch (Exception ex)
        {
            // Never leave a stale snapshot actionable or replace the original operation error.
            Adapters.Clear();
            _snapshots.Clear();
            SelectedAdapter = null;
            OnPropertyChanged(nameof(HasAdapters));
            DetailMessage = L.Join(Environment.NewLine, DetailMessage, L.Join(" ", L.T("Loc_EA011B9820F1"), ex.Message));
        }
        finally
        {
            if (preserveDraft && (SelectedAdapter is null || SelectedAdapter.Snapshot.InterfaceGuid == draftGuid))
                (IPv4Address, PrefixInput, Gateway, PreferredDns, AlternateDns, IsDhcp, IsDnsAutomatic) = draft;
        }
    }

    internal async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _activeOperation = completion;
        try { IsBusy = true; await action(); }
        catch (Exception ex)
        {
            AppLogger.Error("操作执行失败", ex);
            StatusMessage = L.T("Loc_B3B75BD055C0");
            DetailMessage = ex.Message;
            ShowToast(L.T("Loc_0C3B4CF7AA25"), FirstLine(ex.Message), false);
        }
        finally { IsBusy = false; completion.TrySetResult(); }
    }

    /// <summary>
    /// 显示短暂的操作结果提示。提示会先淡入，停留数秒后再淡出，不改变网络状态。
    /// </summary>
    public void ShowToast(string title, string detail, bool succeeded)
    {
        var cancellation = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _toastCancellation, cancellation);
        if (previous is not null)
        {
            try { previous.Cancel(); }
            catch (ObjectDisposedException) { }
        }
        ToastTitle = title;
        ToastDetail = detail;
        ToastSucceeded = succeeded;
        IsToastVisible = true;
        _ = HideToastAsync(cancellation);
    }

    public void ReportUnexpectedError(Exception exception)
    {
        AppLogger.Unhandled("界面线程出现未处理异常", exception);
        StatusMessage = L.T("Loc_093BCCBB18F1");
        DetailMessage = L.F("Loc_F10825042898", FirstLine(exception.Message), AppLogger.LogFilePath);
        ShowToast(L.T("Loc_5A4205871547"), L.T("Loc_190FFBA24E11"), false);
    }

    private async Task HideToastAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4), cancellation.Token);

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
                await dispatcher.InvokeAsync(() => IsToastVisible = false);
            else
                IsToastVisible = false;
        }
        catch (OperationCanceledException)
        {
            // 新提示替换旧提示时，旧的淡出计时会被取消。
        }
        catch (ObjectDisposedException)
        {
            // 进程退出或外部取消时，提示不应影响主窗口。
        }
        finally
        {
            Interlocked.CompareExchange(ref _toastCancellation, null, cancellation);
            cancellation.Dispose();
        }
    }

    private static string FirstLine(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0) return L.T("Loc_1E57D739389D");
        var firstLine = text.Split([Environment.NewLine, "\n", "\r"], StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        return firstLine.Length <= 180 ? firstLine : $"{firstLine[..177]}...";
    }

    private void ApplyAdapterFilter(IReadOnlyList<AdapterSnapshot>? snapshots = null)
    {
        if (snapshots is not null) _snapshots = snapshots.ToList();
        var selectedGuid = SelectedAdapter?.Snapshot.InterfaceGuid;
        var filtered = _snapshots.Where(x => !HideVirtual || x.Type != AdapterType.Virtual)
            .OrderByDescending(x => x.Type == AdapterType.Physical && x.Enabled)
            .ThenBy(x => x.Name).Select(x => new AdapterViewModel(x)).ToList();
        ReplaceCollection(Adapters, filtered);
        OnPropertyChanged(nameof(HasAdapters));
        if (selectedGuid is not null) SelectedAdapter = Adapters.FirstOrDefault(x => x.Snapshot.InterfaceGuid == selectedGuid);
        OnPropertyChanged(nameof(CanApplyPreset));
        CommandManager.InvalidateRequerySuggested();
    }

    private AdapterViewModel? ResolvePresetAdapter(Preset preset) =>
        PresetAdapterResolver.Resolve(preset, Adapters);

    private List<AdapterSnapshot> _snapshots = [];

    private async Task SavePresetsAsync()
    {
        await _presetStorageGate.WaitAsync();
        try
        {
            await _presetService.SaveAsync(_allPresets);
        }
        finally
        {
            _presetStorageGate.Release();
        }
    }

    private async Task AddPresetAsync(PresetEditorResult draft, string? statusPrefix = null,
        string? adapterName = null, string? macAddress = null)
    {
        var resolvedAdapterName = !string.IsNullOrWhiteSpace(draft.AdapterName)
            ? draft.AdapterName
            : adapterName ?? SelectedAdapter?.Name ?? string.Empty;
        var resolvedMacAddress = !string.IsNullOrWhiteSpace(draft.AdapterMacAddress)
            ? draft.AdapterMacAddress
            : macAddress ?? SelectedAdapter?.Snapshot.MacAddress ?? string.Empty;
        var resolvedInterfaceGuid = !string.IsNullOrWhiteSpace(draft.AdapterInterfaceGuid)
            ? draft.AdapterInterfaceGuid
            : SelectedAdapter?.Snapshot.InterfaceGuid ?? string.Empty;
        var groupName = NormalizeGroupName(draft.GroupName);
        var duplicate = _allPresets.FirstOrDefault(existing => PresetService.MatchesConfiguration(existing,
            draft.Config, resolvedAdapterName, resolvedMacAddress, resolvedInterfaceGuid));
        if (duplicate is not null)
        {
            var oldName = duplicate.Name;
            var oldGroup = duplicate.GroupName;
            var oldAdapterName = duplicate.AdapterName;
            var oldMacAddress = duplicate.AdapterMacAddress;
            var oldInterfaceGuid = duplicate.AdapterInterfaceGuid;
            var oldConfig = duplicate.Config.Clone();
            var oldUpdatedAt = duplicate.UpdatedAt;
            try
            {
                duplicate.Name = draft.Name.Trim();
                duplicate.GroupName = groupName;
                duplicate.AdapterName = resolvedAdapterName;
                duplicate.AdapterMacAddress = resolvedMacAddress;
                duplicate.AdapterInterfaceGuid = resolvedInterfaceGuid;
                duplicate.Config = draft.Config.Clone();
                duplicate.UpdatedAt = DateTimeOffset.Now;
                RefreshPresetGroups();
                ResetPresetFilter();
                await SavePresetsAsync();
                SelectedPreset = duplicate;
                StatusMessage = L.F("Loc_F991765E4315", duplicate.Name);
                DetailMessage = string.Empty;
            }
            catch
            {
                duplicate.Name = oldName;
                duplicate.GroupName = oldGroup;
                duplicate.AdapterName = oldAdapterName;
                duplicate.AdapterMacAddress = oldMacAddress;
                duplicate.AdapterInterfaceGuid = oldInterfaceGuid;
                duplicate.Config = oldConfig;
                duplicate.UpdatedAt = oldUpdatedAt;
                RefreshPresetGroups();
                RefreshPresetFilter();
                throw;
            }
            return;
        }

        var preset = new Preset
        {
            Name = draft.Name,
            GroupName = groupName,
            AdapterName = resolvedAdapterName,
            AdapterMacAddress = resolvedMacAddress,
            AdapterInterfaceGuid = resolvedInterfaceGuid,
            Config = draft.Config.Clone()
        };
        _allPresets.Insert(0, preset);
        try
        {
            RefreshPresetGroups();
            ResetPresetFilter();
            await SavePresetsAsync();
            SelectedPreset = preset;
            StatusMessage = statusPrefix is null ? L.F("Loc_933A04F8314F", preset.Name) : $"{statusPrefix}“{preset.Name}”。";
            DetailMessage = string.Empty;
        }
        catch
        {
            _allPresets.Remove(preset);
            RefreshPresetGroups();
            RefreshPresetFilter();
            throw;
        }
    }

    private PresetEditorResult? ShowPresetEditor(string defaultName, string defaultGroup, NetworkConfig config,
        string? adapterName, string? macAddress, string? interfaceGuid)
    {
        var dialog = new PresetEditorWindow(defaultName, defaultGroup, config, adapterName, macAddress,
            GetEditablePresetGroups(), interfaceGuid)
        {
            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => ReferenceEquals(w.DataContext, this))
                    ?? Application.Current?.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private PresetEditorResult? ShowNewPresetEditor(string defaultName, string defaultGroup, NetworkConfig config,
        AdapterViewModel selectedAdapter)
    {
        var dialog = new PresetEditorWindow(defaultName, defaultGroup, config, Adapters.ToList(), selectedAdapter,
            GetEditablePresetGroups())
        {
            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => ReferenceEquals(w.DataContext, this))
                    ?? Application.Current?.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private void RefreshPresetGroups()
    {
        var currentFilter = string.IsNullOrWhiteSpace(_presetGroupFilter) ? NetAdapterManager.Models.GroupIds.All : _presetGroupFilter;
        var groups = new[] { NetAdapterManager.Models.GroupIds.All, NetAdapterManager.Models.GroupIds.Ungrouped }
            .Concat(_customPresetGroups)
            .Concat(_allPresets.Select(x => NormalizeGroupName(x.GroupName)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var nextFilter = groups.Contains(currentFilter, StringComparer.OrdinalIgnoreCase)
            ? currentFilter
            : NetAdapterManager.Models.GroupIds.All;
        _isRefreshingPresetGroups = true;
        try
        {
            for (var index = PresetGroups.Count - 1; index >= 0; index--)
            {
                if (!groups.Contains(PresetGroups[index], StringComparer.OrdinalIgnoreCase))
                    PresetGroups.RemoveAt(index);
            }
            for (var targetIndex = 0; targetIndex < groups.Count; targetIndex++)
            {
                var existingIndex = -1;
                for (var index = 0; index < PresetGroups.Count; index++)
                {
                    if (!string.Equals(PresetGroups[index], groups[targetIndex], StringComparison.OrdinalIgnoreCase)) continue;
                    existingIndex = index;
                    break;
                }
                if (existingIndex < 0)
                    PresetGroups.Insert(targetIndex, groups[targetIndex]);
                else if (existingIndex != targetIndex)
                    PresetGroups.Move(existingIndex, targetIndex);
            }
            _presetGroupFilter = nextFilter;
            OnPropertyChanged(nameof(PresetGroupFilter));
        }
        finally
        {
            _isRefreshingPresetGroups = false;
        }
    }

    private void RefreshPresetFilter()
    {
        var search = PresetSearchText.Trim();
        var group = PresetGroupFilter;
        var filtered = _allPresets.Where(x =>
                (string.Equals(group, NetAdapterManager.Models.GroupIds.All, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(NormalizeGroupName(x.GroupName), group, StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(search)
                    || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || NormalizeGroupName(x.GroupName).Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.AdapterName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.AdapterMacAddress.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.IpSummary.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.DnsSummary.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        ReplaceCollection(Presets, filtered);
        OnPropertyChanged(nameof(PresetCountText));
    }

    private void ResetPresetFilter()
    {
        _presetSearchText = string.Empty;
        _presetGroupFilter = NetAdapterManager.Models.GroupIds.All;
        OnPropertyChanged(nameof(PresetSearchText));
        OnPropertyChanged(nameof(PresetGroupFilter));
        RefreshPresetFilter();
    }

    private async void AddPresetGroupAsync()
    {
        if (IsBusy || _isPresetDialogOpen) return;
        try
        {
            var dialog = new PresetGroupWindow
            {
                Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => ReferenceEquals(w.DataContext, this))
                        ?? Application.Current?.MainWindow
            };
            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.GroupName)) return;

            var groupName = NormalizeGroupName(dialog.GroupName);
            if (string.Equals(groupName, NetAdapterManager.Models.GroupIds.All, StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = L.T("Loc_BB3CA812D51F");
                return;
            }
            if (_customPresetGroups.Contains(groupName, StringComparer.OrdinalIgnoreCase)
                || _allPresets.Any(x => string.Equals(NormalizeGroupName(x.GroupName), groupName, StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = L.F("Loc_3EEBA399E8B8", groupName);
                return;
            }

            await ChangePresetGroupAsync(groupName, delete: false);
        }
        catch (Exception ex)
        {
            AppLogger.Error("添加预设分组失败", ex);
            StatusMessage = L.T("Loc_BC3DC99F0325");
            DetailMessage = ex.Message;
        }
    }

    private async void DeletePresetGroupAsync()
    {
        if (IsBusy || _isPresetDialogOpen) return;
        try
        {
            var groupName = NormalizeGroupName(PresetGroupFilter);
            if (string.Equals(groupName, NetAdapterManager.Models.GroupIds.All, StringComparison.OrdinalIgnoreCase)
                || string.Equals(groupName, NetAdapterManager.Models.GroupIds.Ungrouped, StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = L.T("Loc_A0B1060D02EA");
                DetailMessage = string.Empty;
                return;
            }

            var affected = _allPresets
                .Where(x => string.Equals(NormalizeGroupName(x.GroupName), groupName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var prompt = affected.Count == 0
                ? L.F("Loc_112D386844F2", groupName)
                : L.F("Loc_92C21E3C7ECC", groupName, affected.Count);
            var owner = Application.Current?.Windows.OfType<Window>()
                .FirstOrDefault(w => ReferenceEquals(w.DataContext, this))
                ?? Application.Current?.MainWindow;
            if (MessageBox.Show(owner, prompt, L.T("Loc_A2C0F9EFEB25"), MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes)
                return;

            await ChangePresetGroupAsync(groupName, delete: true);
        }
        catch (Exception ex)
        {
            AppLogger.Error("删除预设分组失败", ex);
            StatusMessage = L.T("Loc_7A3494FB90C5");
            DetailMessage = ex.Message;
        }
    }

    internal async Task<bool> ChangePresetGroupAsync(string groupName, bool delete)
    {
        if (IsBusy || _isPresetDialogOpen) return false;
        SetPresetDialogOpen(true);
        await _presetStorageGate.WaitAsync();
        try
        {
            var groups = _customPresetGroups.ToList();
            var presets = _allPresets.Select(x => x.Clone()).ToList();
            if (delete) PresetGroupService.DeleteGroup(groupName, presets, groups);
            else if (!groups.Contains(groupName, StringComparer.OrdinalIgnoreCase)) groups.Add(groupName);
            await _presetService.SaveLibraryAsync(presets, groups);
            var selected = SelectedPreset?.Id;
            _allPresets = presets;
            _customPresetGroups = groups;
            RefreshPresetGroups();
            ResetPresetFilter();
            SelectedPreset = Presets.FirstOrDefault(x => x.Id == selected);
            StatusMessage = delete ? L.F("Loc_0194448410D5", groupName) : L.F("Loc_D9E0462A7B76", groupName);
            DetailMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("保存预设分组失败", ex);
            StatusMessage = L.T("Loc_19656E29A889");
            DetailMessage = ex.Message;
            ShowToast(L.T("Loc_20570F913040"), FirstLine(ex.Message), false);
            return false;
        }
        finally { _presetStorageGate.Release(); SetPresetDialogOpen(false); }
    }

    private async void RestorePresetBackupAsync()
    {
        if (IsBusy || _isPresetDialogOpen) return;
        if (MessageBox.Show(L.T("Loc_419FDE36875A"), L.T("Loc_C8C662C28870"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        await RunBusyAsync(async () =>
        {
            await _presetService.RestoreBackupAsync();
            _allPresets = await _presetService.LoadAsync();
            _customPresetGroups = await _presetGroupService.LoadAsync();
            _presetLoadError = string.Empty;
            RefreshPresetGroups();
            ResetPresetFilter();
            StatusMessage = L.T("Loc_25E1656CA975");
            DetailMessage = string.Empty;
        });
    }

    private IReadOnlyList<string> GetEditablePresetGroups() => PresetGroups
        .Where(x => !string.Equals(x, NetAdapterManager.Models.GroupIds.All, StringComparison.OrdinalIgnoreCase))
        .ToList();

    private void SetPresetDialogOpen(bool value)
    {
        if (_isPresetDialogOpen == value) return;
        _isPresetDialogOpen = value;
        if (value) _presetOperation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        else _presetOperation?.TrySetResult();
        OnPropertyChanged(nameof(CanSavePreset));
        CommandManager.InvalidateRequerySuggested();
    }

    private static string NormalizeGroupName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NetAdapterManager.Models.GroupIds.Ungrouped : value.Trim();

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}
