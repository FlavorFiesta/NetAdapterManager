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
                result.Errors.Add("静态 IPv4 模式必须填写合法的 IPv4 地址。");
            else if (managed.PrefixLength is < 0 or > 32)
                result.Errors.Add("子网前缀长度必须在 0 到 32 之间。");

            if (managed?.Gateway is { Length: > 0 } gateway && !TryParseIPv4(gateway, out _))
                result.Errors.Add("默认网关不是合法的 IPv4 地址。");

            if (managed is not null && managed.Gateway is { Length: > 0 } validGateway &&
                TryParseIPv4(managed.Address, out var address) && TryParseIPv4(validGateway, out var gatewayAddress) &&
                !IsSameSubnet(address, gatewayAddress, managed.PrefixLength))
            {
                result.Warnings.Add("默认网关与 IPv4 地址不在同一子网，仍可继续应用。");
            }
        }

        if (config.DnsMode == DnsMode.Static)
        {
            if (config.DnsServers.Count > 2)
                result.Errors.Add("最多只能配置两个 DNS 地址。");

            foreach (var dns in config.DnsServers.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (!TryParseIPv4(dns, out _))
                    result.Errors.Add($"DNS 地址不是合法的 IPv4 地址：{dns}");
            }

            var distinctDns = config.DnsServers.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()).ToList();
            if (distinctDns.Count == 2 && string.Equals(distinctDns[0], distinctDns[1], StringComparison.OrdinalIgnoreCase))
                result.Errors.Add("备用 DNS 不能与首选 DNS 相同。");
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
