using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace NetAdapterManager.Services;

public sealed class Localization : INotifyPropertyChanged
{
    public static Localization Instance { get; } = new();
    private static readonly Dictionary<string, Dictionary<string, string>> Resources = LoadResources();
    // Keep UI language separate from CurrentCulture and Windows command output encoding.
    public static string Language { get; private set; } = "zh-CN";
    public string this[string key] => T(key);
    public string CurrentLanguage => Language;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Message> Messages = new();
    public sealed record Message(string Key, object?[] Arguments)
    {
        public Message[]? Parts { get; init; }
        public string Separator { get; init; } = "";
        public string Render() => Parts is not null ? string.Join(Separator, Parts.Select(part => part.Render()))
            : Key.Length == 0 ? (string)Arguments[0]!
            : Arguments.Length == 0 ? Lookup(Key, Language) : string.Format(CultureInfo.CurrentCulture, Lookup(Key, Language), Arguments);
    }
    public static Message Capture(string text) => Messages.TryGetValue(text, out var message) ? message : new("", [text]);
    public static string Join(string separator, params string[] parts)
    {
        var message = new Message("", []) { Parts = parts.Select(Capture).ToArray(), Separator = separator };
        var text = message.Render();
        if (Messages.Count > 1024) Messages.Clear();
        Messages[text] = message;
        return text;
    }
    private static string Remember(string key, object?[] args)
    {
        var message = new Message(key, args);
        var text = message.Render();
        if (Messages.Count > 1024) Messages.Clear();
        Messages[text] = message;
        return text;
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    internal static IReadOnlyDictionary<string, Dictionary<string, string>> Catalog => Resources;

    private static Dictionary<string, Dictionary<string, string>> LoadResources()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NetAdapterManager.Localization.Strings.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)!;
    }

    public static string ResolveLanguage(string? language)
    {
        var value = (language ?? "").ToLowerInvariant();
        if (value is "chinesesimp") return "zh-CN";
        if (value is "chinesetrad") return "zh-TW";
        if (value.StartsWith("zh-hant") || value is "zh-tw" or "zh-hk" or "zh-mo") return "zh-TW";
        if (value.StartsWith("zh-hans") || value is "zh" or "zh-cn" or "zh-sg") return "zh-CN";
        return "en";
    }

    public static string T(string key) => Remember(key, []);
    internal static string Lookup(string key, string language) => Resources.TryGetValue(key, out var values)
        ? values.GetValueOrDefault(language) ?? values.GetValueOrDefault("en") ?? key : key;
    public static string F(string key, params object?[] values) => Remember(key, values);

    public static void SetLanguage(string language)
    {
        Language = ResolveLanguage(language);
        if (Application.Current is { } app)
        {
            foreach (var key in Resources.Keys) app.Resources[key] = T(key);
            // Resource invalidation can leave a template measured for the previous language.
            // Remeasure after bindings/resources settle, without recreating windows or editors.
            app.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>
            {
                foreach (Window window in app.Windows)
                {
                    InvalidateLayout(window);
                    window.UpdateLayout();
                }
            }));
        }
        Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(string.Empty));
    }

    private static void InvalidateLayout(DependencyObject element)
    {
        if (element is UIElement ui) { ui.InvalidateMeasure(); ui.InvalidateVisual(); }
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); index++)
            InvalidateLayout(System.Windows.Media.VisualTreeHelper.GetChild(element, index));
    }
}
