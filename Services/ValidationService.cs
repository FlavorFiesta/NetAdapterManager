using System.Net;
using NetAdapterManager.Models;

namespace NetAdapterManager.Services;

public static class ValidationService
{
    public static ValidationResult ValidateManualConfig(NetworkConfig config)
    {
        var result = new ValidationResult();

        if (config.IpMode == IpMode.Static)
        {
            var managed = config.IPv4Addresses.FirstOrDefault(a =>
                string.Equals(a.Address, config.ManagedIPv4Address, StringComparison.OrdinalIgnoreCase))
                ?? config.IPv4Addresses.FirstOrDefault();

            if (managed is null || !TryParseIPv4(managed.Address, out _))
                result.Errors.Add(L.T("Loc_77F44E3CE3F1"));
            else if (managed.PrefixLength is < 0 or > 32)
                result.Errors.Add(L.T("Loc_538E766A8272"));

            if (managed?.Gateway is { Length: > 0 } gateway && !TryParseIPv4(gateway, out _))
                result.Errors.Add(L.T("Loc_15B34F0EA59C"));

            if (managed is not null && managed.Gateway is { Length: > 0 } validGateway &&
                TryParseIPv4(managed.Address, out var address) && TryParseIPv4(validGateway, out var gatewayAddress) &&
                !IsSameSubnet(address, gatewayAddress, managed.PrefixLength))
            {
                result.Warnings.Add(L.T("Loc_2F728E22C64B"));
            }
        }

        if (config.DnsMode == DnsMode.Static)
        {
            if (config.DnsServers.Count > 2)
                result.Errors.Add(L.T("Loc_1C5815A50C5A"));

            foreach (var dns in config.DnsServers.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (!TryParseIPv4(dns, out _))
                    result.Errors.Add(L.F("Loc_87B772C3150A", dns));
            }

            var distinctDns = config.DnsServers.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()).ToList();
            if (distinctDns.Count == 2 && string.Equals(distinctDns[0], distinctDns[1], StringComparison.OrdinalIgnoreCase))
                result.Errors.Add(L.T("Loc_BB70503F5012"));
        }

        return result;
    }

    public static bool TryParsePrefix(string input, out int prefixLength)
    {
        if (int.TryParse(input.Trim().TrimStart('/'), out prefixLength) && prefixLength is >= 0 and <= 32)
            return true;

        if (IPAddress.TryParse(input.Trim(), out var mask) && mask.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var value = BitConverter.ToUInt32(mask.GetAddressBytes().Reverse().ToArray());
            var inverted = ~value;
            var contiguous = inverted == 0 || (inverted & (inverted + 1)) == 0;
            if (contiguous)
            {
                prefixLength = CountBits(value);
                return true;
            }
        }

        prefixLength = 0;
        return false;
    }

    public static bool TryParseIPv4(string value, out IPAddress address)
    {
        return IPAddress.TryParse(value?.Trim(), out address!) &&
               address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
    }

    private static bool IsSameSubnet(IPAddress left, IPAddress right, int prefixLength)
    {
        var l = left.GetAddressBytes();
        var r = right.GetAddressBytes();
        var bits = prefixLength;
        for (var i = 0; i < 4; i++)
        {
            var mask = bits >= 8 ? 255 : bits <= 0 ? 0 : 256 - (1 << (8 - bits));
            if ((l[i] & mask) != (r[i] & mask)) return false;
            bits -= 8;
        }
        return true;
    }

    private static int CountBits(uint value)
    {
        var count = 0;
        while (value != 0)
        {
            count += (int)(value & 1);
            value >>= 1;
        }
        return count;
    }
}
