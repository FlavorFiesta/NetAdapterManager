using System.Windows;
using NetAdapterManager.Services;
using NetAdapterManager.ViewModels;

namespace NetAdapterManager.Views;

public partial class MainWindow : Window
{
    private double _expandedWidth;
    private double _expandedMinWidth;
    private double _collapsedWidth;
    private WindowState _expandedWindowState;

    private void TogglePresetLibrary_Click(object sender, RoutedEventArgs e)
    {
        var collapse = PresetLibraryPanel.Visibility == Visibility.Visible;
        var editorWidth = ConfigurationScroll.ActualWidth;
        if (collapse)
        {
            _expandedWidth = Width;
            _expandedMinWidth = MinWidth;
            _expandedWindowState = WindowState;
            // Measure in window coordinates so manual UI scaling is included.
            var transform = MainLayout.TransformToAncestor(this);
            var removedWidth = transform.Transform(new Point(PresetLibraryColumn.ActualWidth - 42, 0)).X
                - transform.Transform(new Point()).X;
            var scale = transform.Transform(new Point(1, 0)).X - transform.Transform(new Point()).X;
            var compactClientWidth = MainViewport.ActualWidth * scale - removedWidth;
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                UpdateLayout();
            }
            var frameWidth = ActualWidth - MainViewport.ActualWidth * scale;
            var compactWidth = Math.Max(560, compactClientWidth + frameWidth);
            MinWidth = Math.Min(_expandedMinWidth, compactWidth);
            Width = compactWidth;
            _collapsedWidth = compactWidth;
        }
        else
        {
            var restoredWidth = _expandedWidth + (ActualWidth - _collapsedWidth);
            MinWidth = Math.Min(_expandedMinWidth, MaxWidth);
            restoredWidth = Math.Clamp(restoredWidth, MinWidth, MaxWidth);
            WindowState = _expandedWindowState;
            if (WindowState == WindowState.Normal)
                WindowCompatibility.ResizeToFitWorkArea(this, restoredWidth);
            else
                Width = restoredWidth;
        }
        PresetLibraryPanel.Visibility = collapse ? Visibility.Collapsed : Visibility.Visible;
        PresetLibraryRail.Visibility = collapse ? Visibility.Visible : Visibility.Collapsed;
        PresetLibraryColumn.MinWidth = collapse ? 42 : 440;
        PresetLibraryColumn.Width = collapse ? new GridLength(42) : new GridLength(1, GridUnitType.Star);
        MainLayout.MinWidth = collapse ? 528 : 960;
        ExpandedFooterActions.Visibility = collapse ? Visibility.Collapsed : Visibility.Visible;
        MoreActionsButton.Visibility = collapse ? Visibility.Visible : Visibility.Collapsed;
        System.Windows.Controls.Grid.SetRow(FooterActions, collapse ? 1 : 0);
        System.Windows.Controls.Grid.SetColumn(FooterActions, collapse ? 0 : 1);
        System.Windows.Controls.Grid.SetColumnSpan(FooterActions, collapse ? 2 : 1);
        if (collapse && _expandedWindowState == WindowState.Maximized)
        {
            // The native frame finishes changing after the maximized state is cleared.
            // Correct against the rendered editor once that layout has settled.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                if (PresetLibraryPanel.Visibility != Visibility.Collapsed || WindowState != WindowState.Normal) return;
                UpdateLayout();
                var transform = MainLayout.TransformToAncestor(this);
                var difference = editorWidth - ConfigurationScroll.ActualWidth;
                var adjustment = transform.Transform(new Point(difference, 0)).X - transform.Transform(new Point()).X;
                Width = Math.Clamp(Width + adjustment, MinWidth, MaxWidth);
                _collapsedWidth = Width;
            }));
        }
        // Keep keyboard focus on a visible control after either toggle.
        (collapse ? ExpandPresetLibraryButton : CollapsePresetLibraryButton).Focus();
    }

    private void MoreActions_Click(object sender, RoutedEventArgs e)
    {
        var menu = MoreActionsButton.ContextMenu;
        menu.PlacementTarget = MoreActionsButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        menu.IsOpen = true;
    }

    private void ConfigurationHelp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { ToolTip: System.Windows.Controls.ToolTip tip } button)
        {
            tip.PlacementTarget = button;
            tip.IsOpen = true;
        }
    }
    private void Settings_Click(object sender, RoutedEventArgs e) => new SettingsWindow(_viewModel, _preferences) { Owner = this }.ShowDialog();
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
    private readonly PreferencesService _preferences;
    private bool _initialized;

    public MainWindow() : this(new MainViewModel(), true) { }

    internal MainWindow(MainViewModel viewModel, bool initializeOnLoad, PreferencesService? preferences = null)
    {
        _viewModel = viewModel;
        _initializeOnLoad = initializeOnLoad;
        _preferences = preferences ?? PreferencesService.Default;
        InitializeComponent();
        NetAdapterManager.Services.WindowCompatibility.Attach(this);
        DataContext = _viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_initializeOnLoad || _initialized) return;
        _initialized = true;
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            // 启动阶段的异常也必须留在界面状态栏，不能变成 WPF async void 闪退。
            AppLogger.Error("主窗口初始化失败", ex);
            _viewModel.ShowToast(L.T("Loc_0CF642CF998C"), ex.Message, false);
        }
    }
}
