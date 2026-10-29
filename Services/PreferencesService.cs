using System.Globalization;
using System.IO;
using System.Text.Json;

namespace NetAdapterManager.Services;

public sealed record AppPreferences
{
    public string Language { get; init; } = "system";
    public string Theme { get; init; } = "system";
}

public sealed class PreferencesService(string? filePath = null)
{
    private readonly string _path = filePath ?? Path.Combine(AppContext.BaseDirectory, "settings.json");
    public AppPreferences Current { get; private set; } = new();
    public string? LoadError { get; private set; }
    private bool _unreadable;
    public static PreferencesService Default { get; } = new();
    internal static AppPreferences Normalize(AppPreferences value) => new()
    {
        Language = value.Language is "system" or "zh-CN" or "zh-TW" or "en" ? value.Language : "system",
        Theme = value.Theme is "system" or "light" or "dark" ? value.Theme : "system"
    };

    public void Load()
    {
        LoadError = null;
        _unreadable = false;
        try
        {
            if (File.Exists(_path))
                Current = Normalize(JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(_path))
                    ?? throw new JsonException("Empty settings"));
            else
            {
                var seed = Path.Combine(Path.GetDirectoryName(_path)!, "install-language.txt");
                Current = new AppPreferences { Language = File.Exists(seed)
                    ? Localization.ResolveLanguage(File.ReadAllText(seed).Trim()) : "system" };
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Current = new();
            _unreadable = true;
            LoadError = ex.Message;
            AppLogger.Error("Preferences could not be loaded; using defaults", ex);
        }
    }

    public async Task SaveAsync(AppPreferences preferences)
    {
        var value = Normalize(preferences);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(_path))
                File.Replace(temporary, _path, _path + (_unreadable ? ".corrupt-" + Guid.NewGuid().ToString("N") : ".bak"));
            else File.Move(temporary, _path);
            Current = value;
            LoadError = null;
            _unreadable = false;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void Apply()
    {
        Localization.SetLanguage(Current.Language == "system" ? SystemLanguage() : Current.Language);
        ThemeService.Apply(Current.Theme);
    }

    private static string SystemLanguage()
    {
        try { return CultureInfo.GetCultureInfo(GetUserDefaultUILanguage()).Name; }
        catch (CultureNotFoundException) { return "en"; }
    }
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();
}
