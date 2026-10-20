using NetAdapterManager.Models;

namespace NetAdapterManager.Services;

public sealed class PresetService
{
    private readonly JsonStore<List<Preset>> _store;

    public PresetService(string fileName = "presets.json") => _store = new(fileName);

    public async Task<List<Preset>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var loaded = await _store.LoadAsync([], cancellationToken);
        var unique = Deduplicate(loaded);
        if (unique.Count != loaded.Count)
            await _store.SaveAsync(unique, cancellationToken);
        return unique;
    }

    public async Task SaveAsync(List<Preset> entries, CancellationToken cancellationToken = default)
    {
        var unique = Deduplicate(entries);
        if (unique.Count != entries.Count)
        {
            entries.Clear();
            entries.AddRange(unique);
        }
        await _store.SaveAsync(entries, cancellationToken);
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
    private readonly JsonStore<List<string>> _store = new("preset-groups.json");

    public async Task<List<string>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _store.LoadAsync([], cancellationToken);
        return Normalize(groups);
    }

    public Task SaveAsync(IEnumerable<string> groups, CancellationToken cancellationToken = default) =>
        _store.SaveAsync(Normalize(groups), cancellationToken);

    public static int DeleteGroup(string groupName, IList<Preset> presets, IList<string> customGroups)
    {
        var normalized = groupName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized)
            || string.Equals(normalized, "全部分组", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "未分组", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("只能删除自定义预设分组。", nameof(groupName));

        var affected = 0;
        foreach (var preset in presets)
        {
            if (!string.Equals(preset.GroupName?.Trim(), normalized, StringComparison.OrdinalIgnoreCase)) continue;
            preset.GroupName = "未分组";
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
        .Where(x => !string.Equals(x, "全部分组", StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}
