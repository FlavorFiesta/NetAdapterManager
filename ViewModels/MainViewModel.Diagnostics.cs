using NetAdapterManager.Services;
using NetAdapterManager.Views;

namespace NetAdapterManager.ViewModels;
public sealed partial class MainViewModel
{
    internal OperationJournal Journal { get; }
    public RelayCommand DiagnosticsCommand => new(ShowDiagnosticsAsync, () => !HasPendingOperation);
    private async Task RecordOperationAsync(OperationRecord record)
    {
        try { await Journal.AppendAsync(record); }
        catch (Exception ex)
        {
            AppLogger.Error("Operation journal unavailable", ex);
            WarningMessage = L.Join("\n", WarningMessage, L.T("History.SaveFailed"));
        }
    }
    private async void ShowDiagnosticsAsync()
    {
        if (HasPendingOperation) return;
        SetPresetDialogOpen(true);
        try { new DiagnosticsWindow(await Journal.ReadAsync()) { Owner = DialogOwner }.ShowDialog(); }
        catch (Exception ex) { StatusMessage = L.T("History.LoadFailed"); DetailMessage = ex.Message; }
        finally { SetPresetDialogOpen(false); }
    }
}
