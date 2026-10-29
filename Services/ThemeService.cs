using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace NetAdapterManager.Services;

public static class ThemeService
{
    private static Dictionary<string, Color>? _dark;
    private static string _preference = "system";
    public static string EffectiveTheme { get; private set; } = "dark";
    private static bool _subscribed;
    private static readonly Dictionary<string, string> Light = new()
    {
        ["WindowBackground"]="#F3F5F9", ["CardBackground"]="#FFFFFF", ["CardBorder"]="#CDD4DF",
        ["TextColor"]="#172033", ["MutedText"]="#506078", ["PrimaryColor"]="#215EC7",
        ["PrimaryForeground"]="#FFFFFF", ["PrimaryDark"]="#1D4E9E", ["SoftBlue"]="#EAF1FF",
        ["SuccessColor"]="#087B50", ["DangerColor"]="#B42332", ["ControlBackground"]="#FFFFFF",
        ["ControlBorder"]="#8593A8", ["QuietBackground"]="#EDF1F7", ["QuietBorder"]="#ABB6C8",
        ["HeaderBackground"]="#FFFFFF", ["HeaderBorder"]="#CDD4DF", ["HeaderBadgeBackground"]="#EAF1FF",
        ["HeaderBadgeBorder"]="#92B1E4", ["PanelHeaderBackground"]="#F4F7FB", ["PanelHeaderBorder"]="#CDD4DF",
        ["ListItemBackground"]="#FFFFFF", ["ListItemBorder"]="#CDD4DF", ["ListItemSelectedBackground"]="#E1EBFF",
        ["ListItemSelectedBorder"]="#215EC7", ["ListItemHoverBackground"]="#EEF3FA",
        ["ExistingBackground"]="#F4F7FB", ["ExistingBorder"]="#B6C5DC",
        ["WarningBackground"]="#FFF5D9", ["WarningBorder"]="#9A751E", ["WarningText"]="#765500"
    };

    public static void Start()
    {
        if (_subscribed) return;
        SystemEvents.UserPreferenceChanged += OnSystemChanged;
        SystemParameters.StaticPropertyChanged += OnParametersChanged;
        _subscribed = true;
    }
    public static void Stop()
    {
        if (!_subscribed) return;
        SystemEvents.UserPreferenceChanged -= OnSystemChanged;
        SystemParameters.StaticPropertyChanged -= OnParametersChanged;
        _subscribed = false;
    }
    private static void OnSystemChanged(object sender, UserPreferenceChangedEventArgs e) => Refresh();
    private static void OnParametersChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(SystemParameters.HighContrast)) Refresh(); }
    private static void Refresh()
    {
        var app = Application.Current;
        if (app is null || app.Dispatcher.HasShutdownStarted) return;
        app.Dispatcher.BeginInvoke(() =>
        {
            Apply(_preference);
            if (PreferencesService.Default.Current.Language == "system") PreferencesService.Default.Apply();
        });
    }

    public static void Apply(string preference)
    {
        _preference = preference;
        var app = Application.Current;
        if (app is null) return;
        _dark ??= Light.Keys.ToDictionary(key => key, key => ((SolidColorBrush)app.Resources[key]).Color);
        var dark = preference == "dark" || (preference == "system" && SystemUsesDark());
        EffectiveTheme = SystemParameters.HighContrast ? "high-contrast" : dark ? "dark" : "light";
        foreach (var key in Light.Keys)
        {
            var color = SystemParameters.HighContrast ? ContrastColor(key)
                : dark ? _dark[key] : (Color)ColorConverter.ConvertFromString(Light[key]);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            app.Resources[key] = brush;
        }
    }

    private static Color ContrastColor(string key) => key switch
    {
        "PrimaryForeground" => SystemColors.HighlightTextColor,
        "PrimaryColor" or "PrimaryDark" or "ListItemSelectedBorder" => SystemColors.HighlightColor,
        _ when key.EndsWith("Background") => SystemColors.WindowColor,
        _ => SystemColors.WindowTextColor
    };
    private static bool SystemUsesDark()
    {
        try { return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0; }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        { return false; }
    }
}
