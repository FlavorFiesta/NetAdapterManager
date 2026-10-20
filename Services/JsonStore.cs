using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetAdapterManager.Services;

public sealed class JsonStore<T>
{
    public static string StorageDirectory => AppContext.BaseDirectory;
    public string FilePath => _path;
    private readonly string _path;
    private readonly string _legacyPath;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonStore(string fileName)
    {
        _path = Path.Combine(StorageDirectory, fileName);
        _legacyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NetAdapterManager", fileName);
    }

    public async Task<T> LoadAsync(T fallback, CancellationToken cancellationToken = default)
    {
        try
        {
            var sourcePath = File.Exists(_path)
                ? _path
                : IsMigratableFile() && File.Exists(_legacyPath) ? _legacyPath : string.Empty;
            if (string.IsNullOrWhiteSpace(sourcePath)) return fallback;
            await using var stream = File.OpenRead(sourcePath);
            var value = await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken) ?? fallback;
            if (!string.Equals(sourcePath, _path, StringComparison.OrdinalIgnoreCase))
            {
                try { await SaveAsync(value, cancellationToken); }
                catch (IOException) { /* 便携版放在只读目录时保留旧数据读取能力。 */ }
                catch (UnauthorizedAccessException) { /* 后续保存时继续显示明确的权限错误。 */ }
            }
            return value;
        }
        catch (JsonException ex)
        {
            AppLogger.Error($"读取 JSON 数据失败：{_path}", ex);
            return fallback;
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLogger.Error($"读取 JSON 文件权限不足：{_path}", ex);
            return fallback;
        }
        catch (IOException ex)
        {
            AppLogger.Error($"读取 JSON 文件失败：{_path}", ex);
            return fallback;
        }
    }

    private bool IsMigratableFile() => Path.GetFileName(_path) is "presets.json" or "preset-groups.json" or "managed-addresses.json";

    public async Task SaveAsync(T value, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var tempPath = _path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, _options, cancellationToken);
        }

        if (File.Exists(_path))
            File.Replace(tempPath, _path, null);
        else
            File.Move(tempPath, _path);
    }
}
