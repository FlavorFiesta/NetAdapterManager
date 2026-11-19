using Microsoft.Win32;
using NetAdapterManager.Models;

namespace NetAdapterManager.Services;

// Read-only persisted TCP/IP settings. WMI's active address arrays may be empty
// for disabled/disconnected adapters even though IPv4 Properties has saved values.
internal static class WindowsIPv4Settings
{
    internal static NetworkConfig? Read(string interfaceGuid)
    {
        if (!Guid.TryParse(interfaceGuid, out var id)) return null;
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + id.ToString("B"));
        if (key?.GetValue("EnableDHCP") is not int dhcp) return null;
        return Parse(dhcp != 0, Values(key.GetValue("IPAddress")),
            Values(key.GetValue("SubnetMask")), Values(key.GetValue("DefaultGateway")));
    }

    private static string[] Values(object? value) => value switch
    {
        string[] array => array,
        string text => [text],
        _ => []
    };

    internal static NetworkConfig Parse(bool dhcp, string[] addresses, string[] masks, string[] gateways)
    {
        var result = new NetworkConfig { IpMode = dhcp ? IpMode.Dhcp : IpMode.Static };
        if (dhcp) return result; // Never display a stale saved static address as a DHCP lease.
        for (var i = 0; i < addresses.Length; i++)
        {
            var address = addresses[i].Trim();
            if (address == "0.0.0.0" || !ValidationService.TryParseIPv4(address, out _)) continue;
            if (i >= masks.Length || !ValidationService.TryParsePrefix(masks[i], out var prefix))
                throw new InvalidOperationException(L.T("Network.InvalidSavedMask"));
            result.IPv4Addresses.Add(new Ipv4AddressEntry
            {
                Address = address, PrefixLength = prefix, PrefixOrigin = "Saved",
                Gateway = result.IPv4Addresses.Count == 0
                    ? gateways.FirstOrDefault(g => g != "0.0.0.0" && ValidationService.TryParseIPv4(g, out _)) : null
            });
        }
        return result;
    }

    internal static bool UseSavedConfiguration(bool enabled, bool ipEnabled, NetworkConfig? saved) =>
        saved is not null && (!enabled || !ipEnabled);
}
