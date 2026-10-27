using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetAdapterManager.Services;

internal static class StorageLocks
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    internal static SemaphoreSlim For(string path) => Gates.GetOrAdd(Path.GetFullPath(path), _ => new(1, 1));
}

public sealed class JsonStore<T>
{
    public static string StorageDirectory => AppContext.BaseDirectory;
    public string FilePath => _path;
    private readonly string _path;
    private readonly string _legacyPath;
    private bool _loadFailed;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonStore(string fileName)
    {
        _path = Path.Combine(StorageDirectory, fileName);
        _legacyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NetAdapterManager", fileName);
    }

    public async Task<T> LoadAsync(T fallback, CancellationToken cancellationToken = default)
    {
        var gate = StorageLocks.For(_path);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var source = Path.Exists(_path) ? _path
                : IsMigratableFile() && Path.Exists(_legacyPath) ? _legacyPath : null;
            if (source is null) { _loadFailed = false; return fallback; }
            var value = await ReadAsync(source, cancellationToken);
            _loadFailed = false;
            return value;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _loadFailed = true;
            AppLogger.Error($"读取数据失败，原文件保留：{_path}", ex);
            throw new IOException($"无法读取数据：{_path}。原文件已保留，禁止覆盖；请修复文件或恢复备份。", ex);
        }
        finally { gate.Release(); }
    }

    private async Task<T> ReadAsync(string path, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, _options, token)
            ?? throw new JsonException("数据文件不能为 null。");
    }

    private bool IsMigratableFile() => Path.GetFileName(_path) is "presets.json" or "preset-groups.json" or "managed-addresses.json";

    public async Task SaveAsync(T value, CancellationToken cancellationToken = default)
    {
        var gate = StorageLocks.For(_path);
        await gate.WaitAsync(cancellationToken);
        var tempPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            if (_loadFailed) throw new IOException($"数据读取失败后禁止覆盖：{_path}。请重新读取或恢复备份。");
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            // Validate even for a fresh store instance: corruption must never be replaced by a fallback.
            if (Path.Exists(_path)) await ReadAsync(_path, cancellationToken);
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, value, _options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(_path)) File.Replace(tempPath, _path, _path + ".bak");
            else File.Move(tempPath, _path);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            finally { gate.Release(); }
        }
    }

    public async Task RestoreBackupAsync(CancellationToken cancellationToken = default)
    {
        var gate = StorageLocks.For(_path);
        await gate.WaitAsync(cancellationToken);
        var tempPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await ReadAsync(_path + ".bak", cancellationToken);
            File.Copy(_path + ".bak", tempPath);
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(_path))
                File.Replace(tempPath, _path, _path + ".corrupt-" + Guid.NewGuid().ToString("N"));
            else File.Move(tempPath, _path);
            _loadFailed = false;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            finally { gate.Release(); }
        }
    }
}
