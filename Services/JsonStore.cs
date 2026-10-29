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
            throw new IOException(L.F("Loc_8F89A32A7569", _path), ex);
        }
        finally { gate.Release(); }
    }

    private async Task<T> ReadAsync(string path, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, _options, token)
            ?? throw new JsonException(L.T("Loc_AD4C5E18121D"));
    }

    private bool IsMigratableFile() => Path.GetFileName(_path) is "presets.json" or "preset-groups.json" or "managed-addresses.json";

    public async Task SaveAsync(T value, CancellationToken cancellationToken = default)
    {
        var gate = StorageLocks.For(_path);
        await gate.WaitAsync(cancellationToken);
        var tempPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            if (_loadFailed) throw new IOException(L.F("Loc_75C56947B478", _path));
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

    public Task RestoreBackupAsync(CancellationToken cancellationToken = default) =>
        RestoreBackupAsync(null, cancellationToken);

    public async Task RestoreBackupAsync(Action<T>? validate, CancellationToken cancellationToken = default)
    {
        var gate = StorageLocks.For(_path);
        await gate.WaitAsync(cancellationToken);
        var tempPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.Copy(_path + ".bak", tempPath);
            // Validate the exact bytes that will be committed, not a second read of the backup.
            var candidate = await ReadAsync(tempPath, cancellationToken);
            validate?.Invoke(candidate);
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
