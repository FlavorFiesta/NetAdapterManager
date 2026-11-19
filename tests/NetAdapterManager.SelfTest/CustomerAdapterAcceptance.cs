using System.IO;
using System.Management;
using System.Security.Principal;
using System.Text.Json;
using NetAdapterManager.Models;
using NetAdapterManager.Services;

internal static class CustomerAdapterAcceptance
{
    internal static async Task<int> RunAsync(string interfaceGuid, string evidenceRoot)
    {
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            throw new InvalidOperationException("Administrator rights required.");
        var guid = Guid.Parse(interfaceGuid);
        var service = new NetworkService();
        var allBefore = await service.GetAdaptersAsync();
        var original = allBefore.Single(s => Guid.TryParse(s.InterfaceGuid, out var id) && id == guid);
        // This opt-in test is intentionally restricted to Microsoft's test loopback
        // driver. A disconnected production NIC is not sufficient proof of idleness.
        using (var query = new ManagementObjectSearcher("root\\CIMV2", "SELECT GUID, Description FROM Win32_NetworkAdapter"))
        {
            var loopback = false;
            foreach (ManagementObject row in query.Get())
                using (row)
                    if (Guid.TryParse(row["GUID"] as string, out var id) && id == guid)
                        loopback = (row["Description"] as string ?? "").Contains("KM-TEST", StringComparison.OrdinalIgnoreCase);
            if (!loopback) throw new InvalidOperationException("Only an explicitly selected KM-TEST loopback adapter is eligible.");
        }
        if (!original.Enabled || original.Config.IpMode != IpMode.Static || original.Config.IPv4Addresses.Count == 0
            || original.Config.IPv4Addresses.Any(a => !string.IsNullOrWhiteSpace(a.Gateway)))
            throw new InvalidOperationException("Test adapter must be enabled, static and have no gateway.");
        var routesBefore = DefaultRoutes();
        if (routesBefore.Any(r => r.StartsWith(original.InterfaceIndex + ":", StringComparison.Ordinal)))
            throw new InvalidOperationException("Test adapter has a default route.");
        Directory.CreateDirectory(evidenceRoot);
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "customer-adapter-before.json"), JsonSerializer.Serialize(allBefore));
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "customer-default-routes-before.json"), JsonSerializer.Serialize(routesBefore));
        var originalConfig = original.Config.Clone();
        var failure = false;
        async Task<AdapterSnapshot> Current() => (await service.GetAdaptersAsync()).Single(s => Guid.TryParse(s.InterfaceGuid, out var id) && id == guid);
        async Task<AdapterSnapshot> WaitEnabled(bool enabled)
        {
            for (var i = 0; i < 20; i++)
            {
                var s = await Current();
                if (s.Enabled == enabled) return s;
                await Task.Delay(300);
            }
            throw new InvalidOperationException("Adapter enable/disable did not settle.");
        }
        async Task Apply(NetworkConfig config, string name)
        {
            var result = await service.ApplyExactAsync(await Current(), config);
            if (!result.Succeeded) throw new InvalidOperationException(name + ": " + result.Message + " " + result.ErrorDetail);
            Console.WriteLine("[GREEN] " + name);
        }
        try
        {
            var disabled = await service.SetAdapterEnabledAsync(original, false);
            if (!disabled.Succeeded) throw new InvalidOperationException(disabled.ErrorDetail);
            var saved = await WaitEnabled(false);
            if (!saved.UsesSavedIPv4Configuration || !NetworkService.MatchesTarget(saved, originalConfig, true))
                throw new InvalidOperationException("Disabled adapter did not retain saved IP/mask/gateway/DNS.");
            Console.WriteLine("[GREEN] Disabled adapter displays saved configuration.");
            var enabled = await service.SetAdapterEnabledAsync(saved, true);
            if (!enabled.Succeeded) throw new InvalidOperationException(enabled.ErrorDetail);
            await WaitEnabled(true);
            await Apply(NetworkConfig.ForPreset("198.18.0.254", 32, null, DnsMode.Static, ["223.5.5.5"]), "Static IPv4 and single DNS apply/verify");
            await Apply(NetworkConfig.ForPreset("198.18.0.254", 32, null, DnsMode.Static, ["192.0.2.53", "192.0.2.54"]), "Dual DNS order apply/verify");
            await Apply(NetworkConfig.ForPreset("198.18.0.254", 32, null, DnsMode.Static, ["127.0.0.1", "223.5.5.5"]), "Explicit localhost DNS preserved");
            await Apply(NetworkConfig.ForPreset("198.18.0.254", 32, null, DnsMode.Static, []), "Empty static DNS apply/verify");
            await Apply(NetworkConfig.ForPreset("198.18.0.254", 32, null, DnsMode.Automatic, []), "Automatic DNS apply/verify");
            var multi = NetworkConfig.ForPreset("198.18.0.254", 32, null, DnsMode.Automatic, []);
            multi.IPv4Addresses.Add(new Ipv4AddressEntry { Address = "198.18.0.253", PrefixLength = 32 });
            await Apply(multi, "Multiple static IPv4 addresses apply/verify");
        }
        catch (Exception ex) { failure = true; Console.WriteLine("[RED] " + ex); }
        finally
        {
            try
            {
                var current = await Current();
                if (!current.Enabled)
                {
                    var enabled = await service.SetAdapterEnabledAsync(current, true);
                    if (!enabled.Succeeded) throw new InvalidOperationException(enabled.ErrorDetail);
                    await WaitEnabled(true);
                }
                await Apply(originalConfig, "Original adapter configuration restored and verified");
                var allAfter = await service.GetAdaptersAsync();
                await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "customer-adapter-after.json"), JsonSerializer.Serialize(allAfter));
                if (!routesBefore.SequenceEqual(DefaultRoutes())) throw new InvalidOperationException("Default routes changed.");
                foreach (var before in allBefore.Where(s => s.InterfaceGuid != original.InterfaceGuid))
                {
                    var after = allAfter.Single(s => s.InterfaceGuid == before.InterfaceGuid);
                    if (before.Enabled != after.Enabled || !NetworkService.MatchesTarget(after, before.Config, true))
                        throw new InvalidOperationException("Unrelated adapter changed: " + before.Name);
                }
                Console.WriteLine("[GREEN] Other adapters and default routes unchanged.");
            }
            catch (Exception ex) { failure = true; Console.WriteLine("[RED] RESTORATION/ISOLATION: " + ex); }
        }
        return failure ? 1 : 0;
    }

    private static string[] DefaultRoutes()
    {
        using var query = new ManagementObjectSearcher("root\\StandardCimv2", "SELECT InterfaceIndex, NextHop, RouteMetric FROM MSFT_NetRoute WHERE DestinationPrefix = '0.0.0.0/0'");
        var rows = new List<string>();
        foreach (ManagementObject row in query.Get())
            using (row) rows.Add($"{row["InterfaceIndex"]}:{row["NextHop"]}:{row["RouteMetric"]}");
        return rows.Order(StringComparer.Ordinal).ToArray();
    }
}
