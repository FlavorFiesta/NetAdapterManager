using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using NetAdapterManager.Models;

namespace NetAdapterManager.Services;

internal static class WindowsDnsSettings
{
    internal sealed record Configuration(DnsMode Mode, List<string> Servers);

    internal static DnsMode ReadMode(string interfaceGuid) => Read(interfaceGuid).Mode;

    internal static Configuration Read(string interfaceGuid)
    {
        if (!Guid.TryParse(interfaceGuid, out var id))
            throw new InvalidOperationException(L.T("Loc_3D5DC09B19EC"));

        // NameServer contains the configured static override, not the effective
        // DHCP server list. GetInterfaceDnsSettings requires Windows 10 2004+.
        var settings = new DnsInterfaceSettings { Version = 1 };
        try
        {
            var result = GetInterfaceDnsSettings(id, ref settings);
            if (result == 0)
            {
                try
                {
                    return ResolveConfiguration(Marshal.PtrToStringUni(settings.NameServer),
                        Marshal.PtrToStringUni(settings.ProfileNameServer));
                }
                finally { FreeInterfaceDnsSettings(ref settings); }
            }
        }
        catch (EntryPointNotFoundException) { }

        // Compatibility for earlier Windows 10 and interfaces unavailable to
        // the newer API. Only read the per-interface static override; never
        // infer the source from IP DHCPEnabled or from effective DNS addresses.
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + id.ToString("B"));
        if (key is null)
            throw new Win32Exception(L.T("Loc_D77D9D1B3903"));
        return ResolveConfiguration(key.GetValue("NameServer") as string);
    }

    internal static DnsMode ResolveMode(string? nameServer, string? profileNameServer = null) =>
        string.IsNullOrWhiteSpace(nameServer) && string.IsNullOrWhiteSpace(profileNameServer)
            ? DnsMode.Automatic : DnsMode.Static;

    internal static Configuration ResolveConfiguration(string? nameServer, string? profileNameServer = null)
    {
        var mode = ResolveMode(nameServer, profileNameServer);
        var configured = string.IsNullOrWhiteSpace(nameServer) ? profileNameServer : nameServer;
        var servers = (configured ?? "").Split([' ', ',', ';', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(s => ValidationService.TryParseIPv4(s, out _)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new Configuration(mode, servers);
    }

    internal static List<string> ResolveServers(Configuration configured, IEnumerable<string> effective) =>
        configured.Mode == DnsMode.Static ? configured.Servers.ToList() : effective.ToList();

    // https://learn.microsoft.com/windows/win32/api/netioapi/ns-netioapi-dns_interface_settings
    // Default native packing is required (ULONG64 and pointer fields align to 8 on x64).
    [StructLayout(LayoutKind.Sequential)]
    private struct DnsInterfaceSettings
    {
        public uint Version;
        public ulong Flags;
        public IntPtr Domain;
        public IntPtr NameServer;
        public IntPtr SearchList;
        public uint RegistrationEnabled;
        public uint RegisterAdapterName;
        public uint EnableLlmnr;
        public uint QueryAdapterName;
        public IntPtr ProfileNameServer;
    }

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint GetInterfaceDnsSettings(Guid interfaceGuid, ref DnsInterfaceSettings settings);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern void FreeInterfaceDnsSettings(ref DnsInterfaceSettings settings);
}
