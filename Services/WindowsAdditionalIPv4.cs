using System.Management;
using NetAdapterManager.Models;
namespace NetAdapterManager.Services;
internal static class WindowsAdditionalIPv4
{
    internal static Dictionary<int, List<Ipv4AddressEntry>> Read()
    {
        var result = new Dictionary<int, List<Ipv4AddressEntry>>();
        using var search = new ManagementObjectSearcher("root\\StandardCimv2", "SELECT InterfaceIndex,IPAddress,PrefixLength,PrefixOrigin FROM MSFT_NetIPAddress WHERE AddressFamily=2");
        foreach (ManagementObject row in search.Get())
        {
            using (row)
            {
                if (Convert.ToInt32(row["PrefixOrigin"]) != 1) continue; // Manual, never a DHCP lease.
                var index = Convert.ToInt32(row["InterfaceIndex"]);
                if (!result.TryGetValue(index, out var entries)) result[index] = entries = [];
                entries.Add(new() { Address = (string)row["IPAddress"], PrefixLength = Convert.ToInt32(row["PrefixLength"]), PrefixOrigin = "Manual" });
            }
        }
        return result;
    }
}
