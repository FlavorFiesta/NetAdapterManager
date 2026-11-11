using Microsoft.Win32;
using NetAdapterManager.Services;
using NetAdapterManager.Views;

namespace NetAdapterManager.ViewModels;
public sealed partial class MainViewModel
{
    public RelayCommand ImportPresetsCommand => new(ImportPresetsAsync, () => !HasPendingOperation);
    public RelayCommand ExportPresetsCommand => new(ExportPresetsAsync, _ => !HasPendingOperation);
    public RelayCommand BindPresetCommand => new(BindPresetAsync, () => !HasPendingOperation && SelectedPreset is not null);

    private async void ImportPresetsAsync()
    {
        if (HasPendingOperation) return;
        var file = new OpenFileDialog { Filter = "JSON (*.json)|*.json", Title = L.T("Transfer.Import") };
        if (file.ShowDialog(DialogOwner) != true) return;
        SetPresetDialogOpen(true);
        await _presetStorageGate.WaitAsync();
        try
        {
            var incoming = await PresetExchange.ReadFileAsync(file.FileName);
            var preview = new PresetImportWindow(PresetExchange.Preview(_allPresets, incoming.Presets)) { Owner = DialogOwner };
            if (preview.ShowDialog() != true) return;
            var updated = PresetExchange.Merge(_allPresets, incoming.Presets, preview.Policy);
            var groups = _customPresetGroups.Concat(incoming.Groups)
                .Where(g => g != Models.GroupIds.All && g != Models.GroupIds.Ungrouped)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            // One atomic v1 library commit. Only publish in-memory state after success.
            await _presetService.SaveLibraryAsync(updated, groups);
            _allPresets = updated; _customPresetGroups = groups;
            SelectedPreset = null;
            RefreshPresetGroups(); RefreshPresetFilter();
            StatusMessage = L.T("Transfer.Imported");
        }
        catch (Exception ex) { StatusMessage = L.T("Transfer.Failed"); DetailMessage = ex.Message; }
        finally { _presetStorageGate.Release(); SetPresetDialogOpen(false); }
    }
    private async void ExportPresetsAsync(object? scope)
    {
        if (HasPendingOperation) return;
        var entries = (scope as string) switch
        {
            "all" => _allPresets.ToList(),
            "batch" => BatchSelectedPresets.ToList(),
            _ => SelectedPreset is null ? [] : new List<Models.Preset> { SelectedPreset }
        };
        if (entries.Count == 0) { StatusMessage = L.T("Transfer.Select"); return; }
        var file = new SaveFileDialog { Filter = "JSON (*.json)|*.json", DefaultExt = ".json", FileName = "NetAdapterManager-presets.json", Title = L.T("Transfer.Export") };
        if (file.ShowDialog(DialogOwner) != true) return;
        // Do not let an export overwrite a live application data store.
        var directory = System.IO.Path.GetFullPath(System.IO.Path.GetDirectoryName(file.FileName)!);
        if (string.Equals(directory.TrimEnd('\\'), System.IO.Path.GetFullPath(JsonStore<object>.StorageDirectory).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        { StatusMessage = L.T("Transfer.DataDirectory"); return; }
        SetPresetDialogOpen(true);
        try
        {
            await PresetExchange.ExportAsync(file.FileName, entries, entries.Select(p => p.GroupName));
            StatusMessage = L.T("Transfer.Exported");
        }
        catch (Exception ex) { StatusMessage = L.T("Transfer.Failed"); DetailMessage = ex.Message; }
        finally { SetPresetDialogOpen(false); }
    }
    private async void BindPresetAsync()
    {
        var preset = SelectedPreset;
        if (preset is null || HasPendingOperation) return;
        SetPresetDialogOpen(true);
        await _presetStorageGate.WaitAsync();
        try
        {
            var adapters = (await _networkService.GetAdaptersAsync()).Select(s => new AdapterViewModel(s)).ToList();
            var dialog = new AdapterBindingWindow(preset, adapters) { Owner = DialogOwner };
            if (dialog.ShowDialog() != true || dialog.Selected is null) return;
            var copy = preset.Clone(); var adapter = dialog.Selected.Snapshot;
            copy.AdapterName = adapter.Name; copy.AdapterMacAddress = adapter.MacAddress;
            copy.AdapterInterfaceGuid = adapter.InterfaceGuid; copy.UpdatedAt = DateTimeOffset.Now;
            if (_allPresets.Any(p => p.Id != preset.Id && PresetService.MatchesConfiguration(p, copy.Config, copy.AdapterName, copy.AdapterMacAddress, copy.AdapterInterfaceGuid)))
                throw new System.IO.InvalidDataException(L.T("Transfer.BindingConflict"));
            var updated = _allPresets.Select(p => p.Id == preset.Id ? copy : p.Clone()).ToList();
            await _presetService.SaveAsync(updated);
            _allPresets = updated; RefreshPresetGroups(); RefreshPresetFilter();
            SelectedPreset = Presets.FirstOrDefault(p => p.Id == copy.Id);
            StatusMessage = L.T("Transfer.Bound");
        }
        catch (Exception ex) { StatusMessage = L.T("Transfer.Failed"); DetailMessage = ex.Message; }
        finally { _presetStorageGate.Release(); SetPresetDialogOpen(false); }
    }
}
