using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetAdapterManager.Models;

namespace NetAdapterManager.Services;

public enum ImportConflictPolicy { Skip, Replace }
public sealed record PresetImportRow(string Name, bool Conflict);

public static class PresetExchange
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    public const int MaximumBytes = 4 * 1024 * 1024;
    public static PresetLibrary Read(byte[] bytes)
    {
        try
        {
            if (bytes.Length > MaximumBytes) throw new InvalidDataException();
            // Validate raw required properties before model defaults can hide missing values.
            using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 32 });
            var root = document.RootElement;
            void ValidateProperties(JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var property in element.EnumerateObject())
                    {
                        if (!names.Add(property.Name)) throw new InvalidDataException();
                        ValidateProperties(property.Value);
                    }
                }
                else if (element.ValueKind == JsonValueKind.Array)
                    foreach (var item in element.EnumerateArray()) ValidateProperties(item);
            }
            ValidateProperties(root);
            var presets = root.GetProperty("Presets");
            if (root.GetProperty("Version").GetInt32() != 1 || presets.GetArrayLength() > 2000
                || root.GetProperty("Groups").GetArrayLength() > 500) throw new InvalidDataException();
            foreach (var entry in presets.EnumerateArray())
            {
                if (!Guid.TryParse(entry.GetProperty("Id").GetString(), out var id) || id == Guid.Empty) throw new InvalidDataException();
                var c = entry.GetProperty("Config");
                c.GetProperty("IPv4Addresses").GetArrayLength();
                c.GetProperty("DnsServers").GetArrayLength();
                c.GetProperty("IpMode").GetString(); c.GetProperty("DnsMode").GetString();
                entry.GetProperty("Name").GetString();
            }
            var library = JsonSerializer.Deserialize<PresetLibrary>(bytes, Options) ?? throw new InvalidDataException();
            if (library.Presets.Any(p => p is null || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 256
                    || p.GroupName.Length > 256 || p.Config.IPv4Addresses.Count > 64
                    || p.Config.IPv4Addresses.Any(a => a is null || !ValidationService.TryParseIPv4(a.Address, out _)
                        || a.PrefixLength is < 0 or > 32 || (!string.IsNullOrWhiteSpace(a.Gateway) && !ValidationService.TryParseIPv4(a.Gateway, out _)))
                    || p.Config.DnsServers.Any(d => !ValidationService.TryParseIPv4(d, out _))
                    || !ValidationService.ValidateManualConfig(p.Config).IsValid)
                || library.Groups.Any(g => string.IsNullOrWhiteSpace(g) || g.Length > 256)) throw new InvalidDataException();
            return library;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or InvalidDataException or FormatException or NullReferenceException)
        { throw new InvalidDataException(L.T("Transfer.Invalid"), ex); }
    }

    public static async Task<PresetLibrary> ReadFileAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        if (stream.Length > MaximumBytes) throw new InvalidDataException(L.T("Transfer.Invalid"));
        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes);
        return Read(bytes);
    }
    public static bool Conflicts(Preset a, Preset b) => a.Id == b.Id
        || (string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.GroupName, b.GroupName, StringComparison.OrdinalIgnoreCase))
        || PresetService.MatchesConfiguration(a, b.Config, b.AdapterName, b.AdapterMacAddress, b.AdapterInterfaceGuid);

    public static List<PresetImportRow> Preview(IEnumerable<Preset> existing, IEnumerable<Preset> incoming)
    {
        var seen = existing.ToList();
        var rows = new List<PresetImportRow>();
        foreach (var p in incoming) { rows.Add(new(p.Name, seen.Any(x => Conflicts(x, p)))); seen.Add(p); }
        return rows;
    }
    public static List<Preset> Merge(IEnumerable<Preset> existing, IEnumerable<Preset> incoming, ImportConflictPolicy policy)
    {
        var result = existing.Select(p => p.Clone()).ToList();
        foreach (var source in incoming)
        {
            var conflicts = result.Where(p => Conflicts(p, source)).ToList();
            if (conflicts.Count > 0 && policy == ImportConflictPolicy.Skip) continue;
            foreach (var conflict in conflicts) result.Remove(conflict);
            result.Add(source.Clone());
        }
        return result;
    }
    public static async Task ExportAsync(string path, IEnumerable<Preset> presets, IEnumerable<string> groups)
    {
        var library = new PresetLibrary { Presets = presets.Select(p => p.Clone()).ToList(), Groups = groups.Distinct(StringComparer.OrdinalIgnoreCase).ToList() };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(library, Options);
        Read(bytes); // An exported file must be importable by this version.
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllBytesAsync(temp, bytes); File.Move(temp, path, overwrite: true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
