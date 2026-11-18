using System.Text.Json;
using NetAdapterManager.Models;

namespace NetAdapterManager.Services;

public sealed record ConfigurationChange(string Field, string Current, string Target)
{
    public bool Changed => Current != Target;
}

public static class ConfigurationPreview
{
    // Includes identity, administrative state and managed-address metadata because
    // all affect which interface/address set the eventual write will target.
    internal static string StateKey(AdapterSnapshot adapter) => JsonSerializer.Serialize(new
    {
        Guid = adapter.InterfaceGuid.ToUpperInvariant(), adapter.InterfaceIndex,
        adapter.Name, adapter.MacAddress, adapter.Enabled, adapter.Ipv4InterfacePresent,
        adapter.UsesSavedIPv4Configuration, adapter.Config
    });

    public static IReadOnlyList<ConfigurationChange> Create(AdapterSnapshot before, NetworkConfig target)
    {
        var expected = NetworkService.BuildExpectedConfiguration(before, target, false);
        static string Value(IEnumerable<string?> values) { var text = string.Join("\n", values); return text.Length == 0 ? L.T("Preview.Empty") : text; }
        static string Addresses(NetworkConfig c) => Value(c.IPv4Addresses.Select(a => $"{a.Address}/{a.PrefixLength}"));
        static string Gateways(NetworkConfig c) => Value(c.IPv4Addresses.Select(a => a.Gateway).Where(g => !string.IsNullOrWhiteSpace(g)).Distinct());
        static string Dns(NetworkConfig c) => c.DnsMode == DnsMode.Automatic ? L.T("Preview.Automatic") : Value(c.DnsServers);
        static string Mode(NetworkConfig c) => L.T(c.IpMode == IpMode.Dhcp ? "Preview.Automatic" : "Preview.Static");
        return [new(L.T("Preview.Mode"), Mode(before.Config), Mode(expected)),
            new("IPv4", Addresses(before.Config), expected.IpMode == IpMode.Dhcp ? L.T("Preview.Automatic") : Addresses(expected)),
            new(L.T("Preview.Gateway"), Gateways(before.Config), expected.IpMode == IpMode.Dhcp ? L.T("Preview.Automatic") : Gateways(expected)),
            new("DNS", Dns(before.Config), Dns(expected)),
            new(L.T("Ops.Additional"), Value(before.Config.DhcpStaticAddresses.Select(a => $"{a.Address}/{a.PrefixLength}")), Value(expected.DhcpStaticAddresses.Select(a => $"{a.Address}/{a.PrefixLength}")))];
    }
}
