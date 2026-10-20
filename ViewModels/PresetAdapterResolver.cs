using NetAdapterManager.Models;

namespace NetAdapterManager.ViewModels;

/// <summary>
/// Resolves the adapter that owns a preset.  A preset is an operation target,
/// not merely a configuration template for whichever adapter is selected in
/// the editor.  New records use the stable interface GUID; MAC/name matching
/// keeps older records created before the GUID field was introduced usable.
/// </summary>
public static class PresetAdapterResolver
{
    public static AdapterViewModel? Resolve(Preset? preset, IEnumerable<AdapterViewModel>? adapters)
    {
        if (preset is null || adapters is null) return null;

        var candidates = adapters.Where(x => x is not null).ToList();
        if (!string.IsNullOrWhiteSpace(preset.AdapterInterfaceGuid))
        {
            // A modern preset has a stable interface identity. If that exact
            // adapter is not present, fail closed instead of falling through
            // to the currently selected adapter or an unrelated name match.
            return candidates.FirstOrDefault(x => string.Equals(
                x.Snapshot.InterfaceGuid,
                preset.AdapterInterfaceGuid,
                StringComparison.OrdinalIgnoreCase));
        }

        var normalizedMac = NormalizeMac(preset.AdapterMacAddress);
        if (!string.IsNullOrWhiteSpace(normalizedMac))
        {
            var byMac = candidates.Where(x => string.Equals(
                NormalizeMac(x.Snapshot.MacAddress),
                normalizedMac,
                StringComparison.OrdinalIgnoreCase)).ToList();
            var byMacAndName = byMac.FirstOrDefault(x => string.Equals(
                x.Name,
                preset.AdapterName,
                StringComparison.OrdinalIgnoreCase));
            if (byMacAndName is not null) return byMacAndName;
            if (byMac.Count == 1) return byMac[0];
        }

        return string.IsNullOrWhiteSpace(preset.AdapterName)
            ? null
            : candidates.FirstOrDefault(x => string.Equals(
                x.Name,
                preset.AdapterName,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeMac(string? value) =>
        string.Concat((value ?? string.Empty).Where(char.IsLetterOrDigit));
}
