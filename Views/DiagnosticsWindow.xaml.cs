using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NetAdapterManager.Services;

namespace NetAdapterManager.Views;
public partial class DiagnosticsWindow : Window
{
    private readonly IReadOnlyList<OperationRecord> _records;
    private bool _ready;
    private bool _exporting;
    public DiagnosticsWindow(IReadOnlyList<OperationRecord> records)
    {
        _records = records;
        InitializeComponent(); WindowCompatibility.Attach(this);
        HistoryList.ItemsSource = records; _ready = true;
        if (records.Count > 0) HistoryList.SelectedIndex = 0;
        UpdatePreview();
        System.ComponentModel.PropertyChangedEventManager.AddHandler(L.Instance, (_, _) => RefreshDetail(), string.Empty);
    }
    private void Selection_Changed(object sender, SelectionChangedEventArgs e)
    { RefreshDetail(); }
    private void RefreshDetail() { if (_ready) DetailBox.Text = (HistoryList.SelectedItem as OperationRecord)?.Details ?? ""; }
    private void Privacy_Changed(object sender, RoutedEventArgs e) => UpdatePreview();
    internal void UpdatePreview()
    {
        if (_ready) PreviewBox.Text = DiagnosticExport.Create(_records,
            new(HideIdentity.IsChecked == true, HideMac.IsChecked == true, HideIp.IsChecked == true));
    }
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText((HistoryList.SelectedItem as OperationRecord)?.ErrorDetail ?? "");
            ActionStatus.Text = L.T("History.Copied");
        }
        catch (Exception) { ActionStatus.Text = L.T("History.CopyFailed"); }
    }
    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var file = new SaveFileDialog { Filter = "JSON (*.json)|*.json", DefaultExt = ".json", FileName = "NetAdapterManager-diagnostics.json" };
        if (file.ShowDialog(this) != true) return;
        if (string.Equals(Path.GetFullPath(Path.GetDirectoryName(file.FileName)!).TrimEnd('\\'), AppContext.BaseDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        { ActionStatus.Text = L.T("Transfer.DataDirectory"); return; }
        var preview = PreviewBox.Text;
        var temp = file.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        IsEnabled = false; _exporting = true;
        try
        {
            await File.WriteAllTextAsync(temp, preview);
            File.Move(temp, file.FileName, overwrite: true);
            ActionStatus.Text = L.T("History.Exported");
        }
        catch (Exception ex) { ActionStatus.Text = L.T("Transfer.Failed") + " " + ex.Message; }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (Exception ex) { AppLogger.Error("Diagnostic export cleanup failed", ex); }
            _exporting = false; IsEnabled = true;
        }
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    { if (_exporting) e.Cancel = true; base.OnClosing(e); }
}
