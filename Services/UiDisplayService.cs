using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace NetAdapterManager.Services;
internal static class UiDisplayService
{
    private sealed class FontSizeState(double size) { public double Size = size; }
    private static readonly ConditionalWeakTable<DependencyObject, FontSizeState> Fonts = new();
    private static readonly ConditionalWeakTable<ComboBox, object> Dropdowns = new();
    private static AppPreferences _current = new();
    static UiDisplayService()
    {
        EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent, new RoutedEventHandler((sender, _) =>
        {
            if (sender is not ToolTip tip) return;
            var scale = LayoutScale(_current, VisualTreeHelper.GetDpi(tip).DpiScaleX);
            tip.LayoutTransform = new ScaleTransform(scale, scale);
            ScaleFonts(tip);
        }));
    }
    internal static void Attach(Window window)
    {
        window.Loaded += (_, _) => ApplyWindow(window);
        window.DpiChanged += (_, _) => window.Dispatcher.BeginInvoke(() => ApplyWindow(window));
        window.AddHandler(ToolTipService.ToolTipOpeningEvent, new ToolTipEventHandler((sender, _) =>
        {
            if (sender is FrameworkElement e && e.ToolTip is ToolTip t) ApplyPopup(t);
        }), true);
    }
    internal static double LayoutScale(AppPreferences prefs, double dpi) => prefs.FollowSystemDpi ? 1 : prefs.UiScale / Math.Max(.5, dpi);
    internal static void Apply(AppPreferences preferences)
    {
        _current = PreferencesService.Normalize(preferences);
        if (Application.Current is not { } app) return;
        foreach (Window window in app.Windows) ApplyWindow(window);
        app.Resources["UiTooltipScale"] = new ScaleTransform(_current.FollowSystemDpi ? 1 : _current.UiScale, _current.FollowSystemDpi ? 1 : _current.UiScale);
    }
    private static void ApplyWindow(Window window)
    {
        if (window.Content is not FrameworkElement content) return;
        var scale = LayoutScale(_current, VisualTreeHelper.GetDpi(window).DpiScaleX);
        content.LayoutTransform = new ScaleTransform(scale, scale);
        ScaleFonts(window);
    }
    private static void ApplyPopup(ToolTip t) { ScaleFonts(t); }
    private static void ScaleFonts(DependencyObject root)
    {
        // Capture effective sizes before changing any parent to avoid compounded inheritance.
        var edits = new List<(DependencyObject Element, DependencyProperty Property, double Size)>();
        void Visit(DependencyObject node)
        {
            if (node is ComboBox combo && !Dropdowns.TryGetValue(combo, out _))
            {
                Dropdowns.Add(combo, new());
                combo.DropDownOpened += (_, _) =>
                {
                    if (combo.Template.FindName("PART_Popup", combo) is not System.Windows.Controls.Primitives.Popup { Child: FrameworkElement child }) return;
                    var scale = LayoutScale(_current, VisualTreeHelper.GetDpi(combo).DpiScaleX);
                    child.LayoutTransform = new ScaleTransform(scale, scale);
                    ScaleFonts(child);
                };
            }
            var property = node is TextBlock ? TextBlock.FontSizeProperty : node is Control ? Control.FontSizeProperty : null;
            if (property is not null)
            {
                var source = DependencyPropertyHelper.GetValueSource(node, property).BaseValueSource;
                if (source is BaseValueSource.Local or BaseValueSource.Style or BaseValueSource.ImplicitStyleReference || node is Window or ToolTip)
                {
                    var baseline = Fonts.GetValue(node, _ => new FontSizeState((double)node.GetValue(property)));
                    edits.Add((node, property, baseline.Size));
                }
            }
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Visit(VisualTreeHelper.GetChild(node, i));
        }
        Visit(root);
        foreach (var (element, property, size) in edits) element.SetCurrentValue(property, size * _current.FontScale);
    }
}
