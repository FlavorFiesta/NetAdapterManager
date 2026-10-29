using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.Specialized;
using NetAdapterManager.Models;
using NetAdapterManager.ViewModels;

namespace NetAdapterManager.Views;

public partial class PresetOverviewWindow : Window
{
    public PresetOverviewWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.ClearBatchSelection();
        viewModel.Presets.CollectionChanged += Presets_CollectionChanged;
        System.ComponentModel.PropertyChangedEventManager.AddHandler(L.Instance, LanguageChanged, string.Empty);
        Closed += (_, _) =>
        {
            viewModel.Presets.CollectionChanged -= Presets_CollectionChanged;
            viewModel.ClearBatchSelection();
        };
        UpdateBatchControls();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;
    private void LanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => UpdateBatchControls();

    private void Presets_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateBatchControls();
    private void BatchSelection_Click(object sender, RoutedEventArgs e) => UpdateBatchControls();
    private void UpdateBatchControls()
    {
        var count = ViewModel.BatchSelectedPresets.Count;
        BatchCountText.Text = L.F("Loc_6EAADD746DC7", count);
        BatchMoveButton.IsEnabled = count > 0;
        BatchDeleteButton.IsEnabled = count > 0;
    }
    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var preset in ViewModel.Presets) preset.IsBatchSelected = true;
        UpdateBatchControls();
    }
    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearBatchSelection();
        UpdateBatchControls();
    }
    private async void BatchMove_Click(object sender, RoutedEventArgs e)
    {
        var group = BatchGroupBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(group) || group == NetAdapterManager.Models.GroupIds.All)
        {
            MessageBox.Show(this, L.T("Loc_564914985A95"), L.T("Loc_29C37EA0105B"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var ids = ViewModel.BatchSelectedPresets.Select(x => x.Id).ToArray();
        BatchMoveButton.IsEnabled = BatchDeleteButton.IsEnabled = false;
        await ViewModel.UpdatePresetBatchAsync(ids, group, false);
        UpdateBatchControls();
    }
    private async void BatchDelete_Click(object sender, RoutedEventArgs e)
    {
        var ids = ViewModel.BatchSelectedPresets.Select(x => x.Id).ToArray();
        if (ids.Length == 0 || MessageBox.Show(this, L.F("Loc_572B62D5A86B", ids.Length), L.T("Loc_DDAE7A0FC554"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        BatchMoveButton.IsEnabled = BatchDeleteButton.IsEnabled = false;
        await ViewModel.UpdatePresetBatchAsync(ids, null, true);
        UpdateBatchControls();
    }
    private void PresetList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || sender is not ListBox list) return;
        // Checkbox gestures are reserved for bulk selection, including double-clicks.
        var source = e.OriginalSource as DependencyObject;
        for (var node = source; node is not null && node != list; node = System.Windows.Media.VisualTreeHelper.GetParent(node))
            if (node is CheckBox) return;
        if (ItemsControl.ContainerFromElement(list, source) is not ListBoxItem item || item.DataContext is not Preset preset) return;
        ViewModel.SelectedPreset = preset;
        if (ViewModel.EditPresetCommand.CanExecute(null)) ViewModel.EditPresetCommand.Execute(null);
        e.Handled = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }
}
