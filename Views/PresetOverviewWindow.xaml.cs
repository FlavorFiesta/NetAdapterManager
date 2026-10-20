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
        Closed += (_, _) =>
        {
            viewModel.Presets.CollectionChanged -= Presets_CollectionChanged;
            viewModel.ClearBatchSelection();
        };
        UpdateBatchControls();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void Presets_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateBatchControls();
    private void BatchSelection_Click(object sender, RoutedEventArgs e) => UpdateBatchControls();
    private void UpdateBatchControls()
    {
        var count = ViewModel.BatchSelectedPresets.Count;
        BatchCountText.Text = $"已勾选 {count} 项";
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
        if (string.IsNullOrWhiteSpace(group) || group == "全部分组")
        {
            MessageBox.Show(this, "请先选择要移动到的分组。", "批量换分组", MessageBoxButton.OK, MessageBoxImage.Information);
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
        if (ids.Length == 0 || MessageBox.Show(this, $"确定删除勾选的 {ids.Length} 个预设？（包含筛选隐藏的已勾选项）", "批量删除",
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
