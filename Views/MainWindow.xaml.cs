using System.Windows;
using NetAdapterManager.Services;
using NetAdapterManager.ViewModels;

namespace NetAdapterManager.Views;

public partial class MainWindow : Window
{
    private void PresetList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left || sender is not System.Windows.Controls.ListBox list) return;
        if (System.Windows.Controls.ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) is not System.Windows.Controls.ListBoxItem item
            || item.DataContext is not NetAdapterManager.Models.Preset preset) return;
        _viewModel.SelectedPreset = preset;
        if (_viewModel.EditPresetCommand.CanExecute(null)) _viewModel.EditPresetCommand.Execute(null);
        e.Handled = true;
    }
    private readonly MainViewModel _viewModel;
    private readonly bool _initializeOnLoad;

    public MainWindow() : this(new MainViewModel(), true) { }

    internal MainWindow(MainViewModel viewModel, bool initializeOnLoad)
    {
        _viewModel = viewModel;
        _initializeOnLoad = initializeOnLoad;
        InitializeComponent();
        DataContext = _viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_initializeOnLoad) return;
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            // 启动阶段的异常也必须留在界面状态栏，不能变成 WPF async void 闪退。
            AppLogger.Error("主窗口初始化失败", ex);
            _viewModel.ShowToast("启动失败", ex.Message, false);
        }
    }
}
