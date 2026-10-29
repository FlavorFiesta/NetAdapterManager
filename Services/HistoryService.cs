using NetAdapterManager.Models;
using System.IO;
using System.Text.Json.Serialization;

namespace NetAdapterManager.Services;

public sealed class PresetService
{
    private readonly JsonStore<PresetLibrary> _store;
    private readonly string? _legacyPresets;
    private readonly string? _legacyGroups;
    private readonly SemaphoreSlim _gate;

    public PresetService(string fileName = "preset-library.json")
        : this(fileName, fileName == "preset-library.json" ? "presets.json" : null,
            fileName == "preset-library.json" ? "preset-groups.json" : null) { }

    internal PresetService(string fileName, string? legacyPresets, string? legacyGroups)
    {
        _store = new(fileName);
        _legacyPresets = legacyPresets;
        _legacyGroups = legacyGroups;
        _gate = StorageLocks.For(_store.FilePath + ".library");
    }

    public async Task<List<Preset>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var library = await ReadLibraryAsync(cancellationToken);
        return Deduplicate(library.Presets);
    }

    public async Task SaveAsync(List<Preset> entries, CancellationToken cancellationToken = default)
    {
        var unique = Deduplicate(entries);
        await UpdateLibraryAsync(unique, null, cancellationToken);
        if (unique.Count != entries.Count)
        {
            entries.Clear();
            entries.AddRange(unique);
        }
    }

    public async Task<List<string>> LoadGroupsAsync(CancellationToken token = default) =>
        (await ReadLibraryAsync(token)).Groups;

    public Task SaveGroupsAsync(List<string> groups, CancellationToken token = default) =>
        UpdateLibraryAsync(null, groups, token);

    public Task SaveLibraryAsync(List<Preset> presets, List<string> groups, CancellationToken token = default) =>
        UpdateLibraryAsync(Deduplicate(presets), groups, token);

    private async Task UpdateLibraryAsync(List<Preset>? presets, List<string>? groups, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            var previous = await ReadLibraryAsync(token);
            await _store.SaveAsync(new PresetLibrary
            {
                Presets = presets ?? previous.Presets,
                Groups = groups ?? previous.Groups
            }, token);
        }
        finally { _gate.Release(); }
    }

    private async Task<PresetLibrary> ReadLibraryAsync(CancellationToken token)
    {
        if (!Path.Exists(_store.FilePath) && _legacyPresets is not null && _legacyGroups is not null)
            return new PresetLibrary
            {
                Presets = await new JsonStore<List<Preset>>(_legacyPresets).LoadAsync([], token),
                Groups = await new JsonStore<List<string>>(_legacyGroups).LoadAsync([], token)
            };
        var library = await _store.LoadAsync(new PresetLibrary(), token);
        ValidateLibrary(library);
        return library;
    }

    private static void ValidateLibrary(PresetLibrary library)
    {
        if (library.Version != 1 || library.Presets is null || library.Groups is null
            || library.Presets.Any(x => x is null) || library.Groups.Any(x => x is null))
            throw new IOException(L.T("Loc_4107785BE450"));
    }

    public async Task RestoreBackupAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try { await _store.RestoreBackupAsync(ValidateLibrary, token); }
        finally { _gate.Release(); }
    }

    private static List<Preset> Deduplicate(IEnumerable<Preset> entries)
    {
        var unique = new List<Preset>();
        foreach (var entry in entries.Where(x => x is not null))
        {
            var duplicateIndex = unique.FindIndex(existing => MatchesConfiguration(existing, entry.Config,
                entry.AdapterName, entry.AdapterMacAddress, entry.AdapterInterfaceGuid));
            if (duplicateIndex < 0)
            {
                unique.Add(entry);
                continue;
            }

            if (entry.UpdatedAt > unique[duplicateIndex].UpdatedAt)
                unique[duplicateIndex] = entry;
        }
        return unique;
    }

    public static bool MatchesConfiguration(Preset preset, NetworkConfig config, string? adapterName, string? macAddress,
        string? interfaceGuid = null) =>
        AdapterMatches(preset, adapterName, macAddress, interfaceGuid)
        && string.Equals(CreateConfigurationFingerprint(preset.Config), CreateConfigurationFingerprint(config),
            StringComparison.OrdinalIgnoreCase);

    private static bool AdapterMatches(Preset preset, string? adapterName, string? macAddress, string? interfaceGuid)
    {
        var leftGuid = preset.AdapterInterfaceGuid?.Trim() ?? string.Empty;
        var rightGuid = interfaceGuid?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(leftGuid) && !string.IsNullOrWhiteSpace(rightGuid))
            return string.Equals(leftGuid, rightGuid, StringComparison.OrdinalIgnoreCase);

        var leftMac = string.Concat((preset.AdapterMacAddress ?? string.Empty).Where(char.IsLetterOrDigit));
        var rightMac = string.Concat((macAddress ?? string.Empty).Where(char.IsLetterOrDigit));
        if (!string.IsNullOrWhiteSpace(leftMac) && !string.IsNullOrWhiteSpace(rightMac)
            && string.Equals(leftMac, rightMac, StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(preset.AdapterName) || string.IsNullOrWhiteSpace(adapterName)
                || string.Equals(preset.AdapterName.Trim(), adapterName.Trim(), StringComparison.OrdinalIgnoreCase);

        return !string.IsNullOrWhiteSpace(preset.AdapterName) && !string.IsNullOrWhiteSpace(adapterName)
            && string.Equals(preset.AdapterName.Trim(), adapterName.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateConfigurationFingerprint(NetworkConfig config)
    {
        config ??= new NetworkConfig();
        var addresses = config.IpMode == IpMode.Dhcp
            ? string.Empty
            : string.Join("|", config.IPv4Addresses.Where(x => x is not null).Select(x =>
                $"{x.Address?.Trim() ?? string.Empty};{x.PrefixLength};{x.Gateway?.Trim()};{x.IsManaged}"));
        var managedAddress = config.IpMode == IpMode.Dhcp ? string.Empty : config.ManagedIPv4Address?.Trim();
        var dns = config.DnsMode == DnsMode.Automatic
            ? string.Empty
            : string.Join("|", config.DnsServers.Where(x => x is not null).Select(x => x?.Trim() ?? string.Empty));
        return string.Join("\u001f", config.IpMode, addresses,
            managedAddress, config.DnsMode, dns);
    }
}

public sealed class PresetGroupService
{
    private readonly PresetService _presets;
    public PresetGroupService(PresetService? presets = null) => _presets = presets ?? new PresetService();

    public async Task<List<string>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _presets.LoadGroupsAsync(cancellationToken);
        return Normalize(groups);
    }

    public Task SaveAsync(IEnumerable<string> groups, CancellationToken cancellationToken = default) =>
        _presets.SaveGroupsAsync(Normalize(groups), cancellationToken);

    public static int DeleteGroup(string groupName, IList<Preset> presets, IList<string> customGroups)
    {
        var normalized = groupName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized)
            || string.Equals(normalized, NetAdapterManager.Models.GroupIds.All, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, NetAdapterManager.Models.GroupIds.Ungrouped, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(L.T("Loc_86F2087DA227"), nameof(groupName));

        var affected = 0;
        foreach (var preset in presets)
        {
            if (!string.Equals(preset.GroupName?.Trim(), normalized, StringComparison.OrdinalIgnoreCase)) continue;
            preset.GroupName = NetAdapterManager.Models.GroupIds.Ungrouped;
            affected++;
        }
        for (var index = customGroups.Count - 1; index >= 0; index--)
        {
            if (string.Equals(customGroups[index]?.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                customGroups.RemoveAt(index);
        }
        return affected;
    }

    private static List<string> Normalize(IEnumerable<string> groups) => groups
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.Trim())
        .Where(x => !string.Equals(x, NetAdapterManager.Models.GroupIds.All, StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}

public sealed class PresetLibrary
{
    [JsonRequired] public int Version { get; set; } = 1;
    [JsonRequired] public List<Preset> Presets { get; set; } = [];
    [JsonRequired] public List<string> Groups { get; set; } = [];
}
