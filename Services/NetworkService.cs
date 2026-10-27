using System.Globalization;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text;
using NetAdapterManager.Models;

namespace NetAdapterManager.Services;

/// <summary>
/// Windows network configuration service.
///
/// The application deliberately does not shell out to PowerShell. WMI is used
/// for discovery and adapter enable/disable. IPv4/DNS writes use the Windows
/// netsh IPv4 command because the legacy WMI provider rejects a valid /32 mask
/// with error 66. Every write is followed by a fresh read and can be rolled
/// back from the captured snapshot.
/// </summary>
public sealed class NetworkService
{
    private readonly JsonStore<Dictionary<string, string?>> _managedStore = new("managed-addresses.json");
    private static readonly Encoding NetshEncoding = CreateNetshEncoding();
    private readonly Func<CancellationToken, Task<IReadOnlyList<AdapterSnapshot>>> _snapshotReader;
    private readonly Func<CancellationToken, Task<IReadOnlyList<AdapterSnapshot>>> _recoverySnapshotReader;
    private bool _writesUnsafe;
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    private readonly Func<AdapterSnapshot, NetworkConfig, bool, CancellationToken, Task> _configurationWriter;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<AdapterSnapshot, NetworkConfig, CancellationToken, Task> _saveManagedAddress;

    public NetworkService()
    {
        _snapshotReader = ReadAdaptersCoreAsync;
        // Recovery must still read OS state if local managed-address metadata becomes unreadable.
        _recoverySnapshotReader = token => Task.Run<IReadOnlyList<AdapterSnapshot>>(() => ReadAdapters([]), token).WaitAsync(token);
        _configurationWriter = ApplyConfigurationAsync;
        _delay = Task.Delay;
        _saveManagedAddress = SaveManagedAddressAsync;
    }

    // Test the complete apply/rollback flow without WMI, netsh, timers or user files.
    internal NetworkService(Func<CancellationToken, Task<IReadOnlyList<AdapterSnapshot>>> snapshotReader,
        Action<AdapterSnapshot, NetworkConfig, bool> configurationWriter,
        Func<TimeSpan, CancellationToken, Task> delay,
        Func<AdapterSnapshot, NetworkConfig, CancellationToken, Task> saveManagedAddress)
        : this(snapshotReader, (before, target, exact, token) =>
        {
            token.ThrowIfCancellationRequested();
            configurationWriter(before, target, exact);
            return Task.CompletedTask;
        }, delay, saveManagedAddress) { }

    internal NetworkService(Func<CancellationToken, Task<IReadOnlyList<AdapterSnapshot>>> snapshotReader,
        Func<AdapterSnapshot, NetworkConfig, bool, CancellationToken, Task> configurationWriter,
        Func<TimeSpan, CancellationToken, Task> delay,
        Func<AdapterSnapshot, NetworkConfig, CancellationToken, Task> saveManagedAddress,
        Func<CancellationToken, Task<IReadOnlyList<AdapterSnapshot>>>? recoverySnapshotReader = null)
    {
        _snapshotReader = snapshotReader;
        _recoverySnapshotReader = recoverySnapshotReader ?? snapshotReader;
        _configurationWriter = configurationWriter;
        _delay = delay;
        _saveManagedAddress = saveManagedAddress;
    }

    public Task<IReadOnlyList<AdapterSnapshot>> GetAdaptersAsync(CancellationToken cancellationToken = default)
        => _snapshotReader(cancellationToken);

    private async Task<IReadOnlyList<AdapterSnapshot>> ReadAdaptersCoreAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var managed = await _managedStore.LoadAsync([], timeout.Token);
            // A timed-out WMI reader may finish later, but it never writes network state.
            return await Task.Run(() => ReadAdapters(managed), timeout.Token).WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("读取网卡状态超时，请稍后刷新。");
        }
    }

    public async Task<ApplyResult> ApplyAsync(AdapterSnapshot adapter, NetworkConfig target,
        OperationSource source, CancellationToken cancellationToken = default)
        => await ApplyInternalAsync(adapter, target, exactAddressSet: false, cancellationToken);

    public async Task<ApplyResult> ApplyExactAsync(AdapterSnapshot adapter, NetworkConfig target,
        CancellationToken cancellationToken = default)
        => await ApplyInternalAsync(adapter, target, exactAddressSet: true, cancellationToken);

    public async Task<(bool Succeeded, string? ErrorDetail)> SetAdapterEnabledAsync(AdapterSnapshot adapter,
        bool enabled, CancellationToken cancellationToken = default)
    {
        await WriteGate.WaitAsync(cancellationToken);
        try
        {
            if (_writesUnsafe) throw new InvalidOperationException("上一个命令的退出状态不确定，已禁止继续写入。请检查系统状态后重启程序。");
            var code = await Task.Run(() =>
            {
                using var networkAdapter = FindNetworkAdapter(adapter);
                if (networkAdapter is null)
                    throw new InvalidOperationException("找不到目标网卡的 Windows 管理对象。");

                return InvokeReturnCode(networkAdapter, enabled ? "Enable" : "Disable", [],
                    enabled ? "启用网卡" : "禁用网卡", allowRebootRequired: false);
            }, cancellationToken);
            return (code == 0, code == 0 ? null : FormatWmiError(code, "网卡启停"));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
        finally { WriteGate.Release(); }
    }

    private async Task<ApplyResult> ApplyInternalAsync(AdapterSnapshot adapter, NetworkConfig target,
        bool exactAddressSet, CancellationToken cancellationToken)
    {
        await WriteGate.WaitAsync(cancellationToken);
        try
        {
            if (_writesUnsafe) return new ApplyResult { Message = "已禁止继续写入，请检查系统状态后重启程序。" };
            return await ApplyCoreAsync(adapter, target.Clone(), exactAddressSet, cancellationToken);
        }
        finally { WriteGate.Release(); }
    }

    private async Task<ApplyResult> ApplyCoreAsync(AdapterSnapshot adapter, NetworkConfig target,
        bool exactAddressSet, CancellationToken cancellationToken)
    {
        var validation = ValidationService.ValidateManualConfig(target);
        if (!validation.IsValid)
            return new ApplyResult
            {
                Message = "输入校验失败。",
                ErrorDetail = string.Join(Environment.NewLine, validation.Errors),
                Warnings = validation.Warnings
            };

        AdapterSnapshot before;
        try
        {
            var current = await GetAdaptersAsync(cancellationToken);
            before = current.FirstOrDefault(x => SameId(x.InterfaceGuid, adapter.InterfaceGuid))
                ?? throw new InvalidOperationException("目标网卡已不存在，请刷新网卡列表。");
        }
        catch (Exception ex)
        {
            return new ApplyResult
            {
                Message = "读取修改前网卡状态失败。",
                ErrorDetail = ex.Message,
                Warnings = validation.Warnings
            };
        }

        try
        {
            var expected = BuildExpectedConfiguration(before, target, exactAddressSet);
            await _configurationWriter(before, expected, true, cancellationToken);
            var after = await WaitForSnapshotAsync(before.InterfaceGuid, expected, true, cancellationToken);
            if (after is null)
                throw new InvalidOperationException("配置已写入，但复核时找不到目标网卡。");
            if (!MatchesTarget(after, expected, true))
                throw new InvalidOperationException("最终复核不一致，实际网卡状态未达到目标配置。");

            await _saveManagedAddress(before, target, cancellationToken);

            after.Config.ManagedIPv4Address = target.IpMode == IpMode.Static ? target.ManagedIPv4Address : null;
            foreach (var address in after.Config.IPv4Addresses)
                address.IsManaged = string.Equals(address.Address, after.Config.ManagedIPv4Address, StringComparison.OrdinalIgnoreCase);

            return new ApplyResult
            {
                Succeeded = true,
                Message = "配置已应用并复核一致。",
                Warnings = validation.Warnings,
                FinalSnapshot = after
            };
        }
        catch (Exception ex)
        {
            if (ex is ProcessTerminationException)
            {
                _writesUnsafe = true;
                return new ApplyResult { Message = "无法确认网络命令已停止，请勿重复操作。", ErrorDetail = ex.Message };
            }
            var rollback = await TryRollbackAsync(before);
            return new ApplyResult
            {
                Message = rollback.Succeeded ? "配置失败，已恢复修改前状态。" : "配置失败，自动回滚也失败。",
                ErrorDetail = rollback.Succeeded ? ex.Message : ex.Message + Environment.NewLine + rollback.ErrorDetail,
                RolledBack = rollback.Succeeded,
                Warnings = validation.Warnings
            };
        }
    }

    private async Task SaveManagedAddressAsync(AdapterSnapshot before, NetworkConfig target,
        CancellationToken cancellationToken)
    {
        var managedMap = await _managedStore.LoadAsync([], cancellationToken);
        if (target.IpMode == IpMode.Static && !string.IsNullOrWhiteSpace(target.ManagedIPv4Address))
            managedMap[before.InterfaceGuid] = target.ManagedIPv4Address;
        else if (target.IpMode == IpMode.Dhcp)
            managedMap.Remove(before.InterfaceGuid);
        await _managedStore.SaveAsync(managedMap, cancellationToken);
    }

    private static IReadOnlyList<AdapterSnapshot> ReadAdapters(Dictionary<string, string?> managed)
    {
        var configurations = new Dictionary<string, WmiConfigurationData>(StringComparer.OrdinalIgnoreCase);
        using (var configurationSearcher = new ManagementObjectSearcher(
                   "root\\CIMV2",
                   "SELECT SettingID, IPEnabled, DHCPEnabled, IPAddress, IPSubnet, DefaultIPGateway, DNSServerSearchOrder FROM Win32_NetworkAdapterConfiguration"))
        {
            foreach (ManagementObject configurationObject in configurationSearcher.Get())
            {
                using (configurationObject)
                {
                    var settingId = ReadString(configurationObject, "SettingID");
                    if (string.IsNullOrWhiteSpace(settingId)) continue;
                    configurations[NormalizeId(settingId)] = ReadConfigurationData(configurationObject);
                }
            }
        }

        var dnsClientServerAddressesAvailable = TryReadDnsClientServerAddresses(out var dnsClientServers);
        var rows = new List<AdapterSnapshot>();
        using var adapterSearcher = new ManagementObjectSearcher(
            "root\\CIMV2",
                   "SELECT GUID, Name, Description, NetConnectionID, MACAddress, InterfaceIndex, Index, NetEnabled, PhysicalAdapter, NetConnectionStatus, ConfigManagerErrorCode FROM Win32_NetworkAdapter");
        foreach (ManagementObject adapterObject in adapterSearcher.Get())
        {
            using (adapterObject)
            {
                var guid = ReadString(adapterObject, "GUID");
                var interfaceIndex = ReadInt(adapterObject, "InterfaceIndex", ReadInt(adapterObject, "Index", 0));
                var key = string.IsNullOrWhiteSpace(guid) ? $"WMI:{interfaceIndex}" : guid;
                var connectionName = ReadString(adapterObject, "NetConnectionID");
                if (string.IsNullOrWhiteSpace(connectionName))
                    continue;
                configurations.TryGetValue(NormalizeId(guid), out var configuration);

                var resolvedDnsServers = dnsClientServerAddressesAvailable
                    ? dnsClientServers.TryGetValue(interfaceIndex, out var authoritativeDns)
                        ? authoritativeDns.ToList()
                        : []
                    : configuration?.DnsServers.ToList() ?? [];
                var config = new NetworkConfig
                {
                    IpMode = configuration?.DhcpEnabled == true ? IpMode.Dhcp : IpMode.Static,
                    DnsMode = WindowsDnsSettings.ReadMode(guid),
                    DnsServers = resolvedDnsServers
                };
                if (configuration is not null)
                    config.IPv4Addresses = configuration.Addresses.Select(x => x.Clone()).ToList();

                var managedAddress = managed.FirstOrDefault(x => SameId(x.Key, key)).Value;
                config.ManagedIPv4Address = managedAddress;
                foreach (var address in config.IPv4Addresses)
                    address.IsManaged = string.Equals(address.Address, managedAddress, StringComparison.OrdinalIgnoreCase);

                var enabled = ReadBool(adapterObject, "NetEnabled", ReadInt(adapterObject, "ConfigManagerErrorCode", 0) != 22);
                var connectionStatus = GetConnectionStatus(adapterObject, enabled);
                rows.Add(new AdapterSnapshot
                {
                    Name = connectionName,
                    MacAddress = ReadString(adapterObject, "MACAddress"),
                    InterfaceGuid = key,
                    InterfaceIndex = interfaceIndex,
                    Enabled = enabled,
                    Ipv4InterfacePresent = configuration?.IpEnabled == true,
                    ConnectionStatus = connectionStatus,
                    Type = Classify(ReadString(adapterObject, "Name"), ReadString(adapterObject, "Description"),
                        ReadBool(adapterObject, "PhysicalAdapter", false)),
                    Config = config
                });
            }
        }

        return rows
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static WmiConfigurationData ReadConfigurationData(ManagementObject configurationObject)
    {
        var addresses = ReadStrings(configurationObject, "IPAddress");
        var subnets = ReadStrings(configurationObject, "IPSubnet");
        var gateways = ReadStrings(configurationObject, "DefaultIPGateway")
            .Where(IsIPv4)
            .ToList();
        var dns = ReadStrings(configurationObject, "DNSServerSearchOrder")
            .Where(IsIPv4)
            .ToList();

        var ipv4 = new List<Ipv4AddressEntry>();
        for (var i = 0; i < addresses.Length; i++)
        {
            if (!IPAddress.TryParse(addresses[i], out var parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
                continue;
            var prefix = i < subnets.Length ? ParsePrefix(subnets[i]) : 32;
            ipv4.Add(new Ipv4AddressEntry
            {
                Address = addresses[i],
                PrefixLength = prefix,
                Gateway = ipv4.Count == 0 ? gateways.FirstOrDefault() : null,
                PrefixOrigin = "WMI"
            });
        }

        return new WmiConfigurationData(
            ReadBool(configurationObject, "IPEnabled", false),
            ReadBool(configurationObject, "DHCPEnabled", false),
            ipv4,
            dns);
    }

    private static bool TryReadDnsClientServerAddresses(out Dictionary<int, List<string>> addressesByInterface)
    {
        addressesByInterface = new Dictionary<int, List<string>>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\StandardCimv2",
                "SELECT InterfaceIndex, AddressFamily, ServerAddresses FROM MSFT_DNSClientServerAddress WHERE AddressFamily = 2");
            foreach (ManagementObject dnsObject in searcher.Get())
            {
                using (dnsObject)
                {
                    var interfaceIndex = ReadInt(dnsObject, "InterfaceIndex", 0);
                    if (interfaceIndex <= 0) continue;
                    addressesByInterface[interfaceIndex] = ReadStrings(dnsObject, "ServerAddresses")
                        .Where(IsIPv4)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }

            // The provider returns an empty ServerAddresses collection for an interface
            // with no configured DNS. Keeping that empty value is important: falling
            // back to the legacy Win32 provider can surface stale 127.0.0.1 values.
            return addressesByInterface.Count > 0;
        }
        catch
        {
            // Keep the legacy WMI path as a compatibility fallback for older Windows
            // images where StandardCimv2 is unavailable.
            addressesByInterface.Clear();
            return false;
        }
    }

    internal static void ApplyConfiguration(AdapterSnapshot before, NetworkConfig target, bool exactAddressSet,
        Action<string[]> commandRunner)
    {
        void Execute(params string[] arguments) => commandRunner(arguments);
        var interfaceSelector = GetNetshInterfaceSelector(before);
        if (target.IpMode == IpMode.Dhcp)
        {
            if (before.Config.IpMode != IpMode.Dhcp)
            {
                Execute("interface", "ipv4", "set", "address",
                    interfaceSelector, "source=dhcp");
            }
        }
        else
        {
            var desired = BuildDesiredAddresses(before, target, exactAddressSet);
            var expected = target.Clone();
            expected.IPv4Addresses = desired;
            if (!MatchesIpTarget(before.Config, expected))
            {
                if (desired.Count == 0)
                {
                    // 允许回滚到“静态但当前没有地址”的原始状态。
                    Execute("interface", "ipv4", "set", "address",
                        interfaceSelector, "source=static");
                }
                else
                {
                    var managed = target.IPv4Addresses.FirstOrDefault(x =>
                        string.Equals(x.Address, target.ManagedIPv4Address, StringComparison.OrdinalIgnoreCase));
                    var gateway = managed?.Gateway ?? target.IPv4Addresses.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Gateway))?.Gateway;
                    var first = desired[0];
                    Execute("interface", "ipv4", "set", "address",
                        interfaceSelector, "source=static", $"address={first.Address}",
                        $"mask={PrefixToMask(first.PrefixLength)}",
                        $"gateway={(string.IsNullOrWhiteSpace(gateway) ? "none" : gateway)}");

                    foreach (var address in desired.Skip(1))
                    {
                        Execute("interface", "ipv4", "add", "address",
                            interfaceSelector, $"address={address.Address}",
                            $"mask={PrefixToMask(address.PrefixLength)}");
                    }
                }
            }
        }

        if (MatchesDnsTarget(before.Config, target) && before.Config.IpMode == target.IpMode)
            return;
        if (target.DnsMode == DnsMode.Automatic)
        {
            if (before.Config.DnsMode != DnsMode.Automatic || before.Config.IpMode != target.IpMode)
            {
                Execute("interface", "ipv4", "set", "dnsservers",
                    interfaceSelector, "source=dhcp");
            }
        }
        else if (target.DnsServers.Count > 0)
        {
            Execute("interface", "ipv4", "set", "dnsservers",
                interfaceSelector, "source=static", $"address={target.DnsServers[0]}", "validate=no");
            for (var i = 1; i < target.DnsServers.Count; i++)
            {
                Execute("interface", "ipv4", "add", "dnsservers",
                    interfaceSelector, $"address={target.DnsServers[i]}", $"index={i + 1}", "validate=no");
            }
        }
        else
        {
            // 静态 IPv4 可以明确选择“不配置 DNS”。使用 static/none，避免回退成自动 DNS。
            Execute("interface", "ipv4", "set", "dnsservers",
                interfaceSelector, "source=static", "address=none", "validate=no");
        }
    }

    private static async Task ApplyConfigurationAsync(AdapterSnapshot before, NetworkConfig target, bool exact,
        CancellationToken cancellationToken)
    {
        var commands = new List<string[]>();
        ApplyConfiguration(before, target, exact, commands.Add);
        foreach (var arguments in commands)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = System.IO.Path.Combine(Environment.SystemDirectory, "netsh.exe"),
                StandardOutputEncoding = NetshEncoding, StandardErrorEncoding = NetshEncoding
            };
            foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
            var result = await ProcessRunner.RunAsync(startInfo, TimeSpan.FromSeconds(10), cancellationToken);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"Windows netsh 执行失败（退出码 {result.ExitCode}）：{result.Output}");
        }
    }

    internal static NetworkConfig BuildExpectedConfiguration(AdapterSnapshot before, NetworkConfig target, bool exact)
    {
        var expected = target.Clone();
        if (target.IpMode == IpMode.Static)
            expected.IPv4Addresses = BuildDesiredAddresses(before, target, exact);
        return expected;
    }

    private static bool MatchesIpTarget(NetworkConfig actual, NetworkConfig expected)
    {
        if (actual.IpMode != expected.IpMode) return false;
        if (expected.IpMode == IpMode.Dhcp) return true;
        var left = actual.IPv4Addresses.Select(x => $"{x.Address}/{x.PrefixLength}").OrderBy(x => x);
        var right = expected.IPv4Addresses.Select(x => $"{x.Address}/{x.PrefixLength}").OrderBy(x => x);
        var leftGateway = actual.IPv4Addresses.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Gateway))?.Gateway ?? "";
        var rightGateway = expected.IPv4Addresses.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Gateway))?.Gateway ?? "";
        return left.SequenceEqual(right, StringComparer.OrdinalIgnoreCase) && leftGateway.Trim() == rightGateway.Trim();
    }

    private static string GetNetshInterfaceSelector(AdapterSnapshot adapter) =>
        adapter.InterfaceIndex > 0 ? $"name={adapter.InterfaceIndex}" : $"name={adapter.Name}";

    private static Encoding CreateNetshEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var codePage = CultureInfo.CurrentUICulture.TextInfo.ANSICodePage;
            return codePage > 0 ? Encoding.GetEncoding(codePage) : Encoding.GetEncoding(936);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    private static List<Ipv4AddressEntry> BuildDesiredAddresses(AdapterSnapshot before, NetworkConfig target, bool exactAddressSet)
    {
        if (exactAddressSet)
            return target.IPv4Addresses.Select(x => x.Clone()).ToList();

        var managed = target.IPv4Addresses.FirstOrDefault(x =>
            string.Equals(x.Address, target.ManagedIPv4Address, StringComparison.OrdinalIgnoreCase))
            ?? target.IPv4Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("缺少要管理的静态 IPv4 地址。");
        if (before.Config.IpMode == IpMode.Dhcp)
            return [managed.Clone()];

        var oldManaged = before.Config.ManagedIPv4Address
            ?? before.Config.IPv4Addresses.FirstOrDefault()?.Address;
        var preserved = before.Config.IPv4Addresses
            .Where(x => !string.Equals(x.Address, oldManaged, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(x.Address, managed.Address, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Clone())
            .ToList();
        var desired = new List<Ipv4AddressEntry> { managed.Clone() };
        desired.AddRange(preserved);
        return desired;
    }

    private async Task<AdapterSnapshot?> WaitForSnapshotAsync(string interfaceGuid, NetworkConfig target,
        bool exactAddressSet, CancellationToken cancellationToken, bool recovering = false)
    {
        AdapterSnapshot? last = null;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            await _delay(TimeSpan.FromMilliseconds(350), cancellationToken);
            var snapshots = await (recovering ? _recoverySnapshotReader(cancellationToken) : GetAdaptersAsync(cancellationToken));
            last = snapshots.FirstOrDefault(x => SameId(x.InterfaceGuid, interfaceGuid));
            if (last is not null && MatchesTarget(last, target, exactAddressSet))
                return last;
        }
        return last;
    }

    private async Task<(bool Succeeded, string? ErrorDetail)> TryRollbackAsync(AdapterSnapshot before)
    {
        // A caller cancelling the failed operation must not cancel its recovery.
        using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            // Use fresh state for idempotency: the original snapshot is only the
            // restore target. Re-enabling already active DHCP can fail in netsh.
            var snapshots = await _recoverySnapshotReader(recovery.Token);
            var current = snapshots.FirstOrDefault(x => SameId(x.InterfaceGuid, before.InterfaceGuid))
                ?? throw new InvalidOperationException("回滚前找不到目标网卡，请检查网卡是否仍连接。");
            await _configurationWriter(current, before.Config, true, recovery.Token);
            var restored = await WaitForSnapshotAsync(before.InterfaceGuid, before.Config, true, recovery.Token, recovering: true);
            if (restored is null || !MatchesTarget(restored, before.Config, true))
                return (false, "回滚命令已执行，但实际 IPv4/DNS 状态未恢复到修改前配置。");
            return (true, null);
        }
        catch (Exception ex)
        {
            if (ex is ProcessTerminationException) _writesUnsafe = true;
            return (false, ex.Message);
        }
    }

    internal static bool MatchesTarget(AdapterSnapshot actual, NetworkConfig target, bool exactAddressSet)
    {
        if (actual.Config.IpMode != target.IpMode) return false;
        if (target.IpMode == IpMode.Static)
        {
            if (exactAddressSet && target.IPv4Addresses.Count == 0)
                return actual.Config.IPv4Addresses.Count == 0 && MatchesDnsTarget(actual.Config, target);
            var desired = target.IPv4Addresses.FirstOrDefault(x =>
                              string.Equals(x.Address, target.ManagedIPv4Address, StringComparison.OrdinalIgnoreCase))
                          ?? target.IPv4Addresses.FirstOrDefault();
            var current = actual.Config.IPv4Addresses.FirstOrDefault(x =>
                string.Equals(x.Address, desired?.Address, StringComparison.OrdinalIgnoreCase));
            if (desired is null || current is null || current.PrefixLength != desired.PrefixLength) return false;
            var actualGateway = actual.Config.IPv4Addresses.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Gateway))?.Gateway;
            var desiredGateway = target.IPv4Addresses.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Gateway))?.Gateway;
            if (!string.Equals(actualGateway?.Trim() ?? string.Empty, desiredGateway?.Trim() ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase)) return false;
            if (exactAddressSet)
            {
                var expected = target.IPv4Addresses.Select(x => $"{x.Address}/{x.PrefixLength}").OrderBy(x => x);
                var found = actual.Config.IPv4Addresses.Select(x => $"{x.Address}/{x.PrefixLength}").OrderBy(x => x);
                if (!expected.SequenceEqual(found, StringComparer.OrdinalIgnoreCase)) return false;
            }
        }

        return MatchesDnsTarget(actual.Config, target);
    }

    private static bool MatchesDnsTarget(NetworkConfig actual, NetworkConfig target)
    {
        // Windows 对“静态但没有 DNS”的状态可能无法区分 static/none 与 automatic。
        // 目标明确为静态空 DNS 时，只要求实际 DNS 列表为空，不能要求模式字符串相等。
        if (target.DnsMode == DnsMode.Static && target.DnsServers.Count == 0)
            return actual.DnsServers.Count == 0;
        if (target.DnsMode != actual.DnsMode) return false;
        return target.DnsMode != DnsMode.Static
            || target.DnsServers.SequenceEqual(actual.DnsServers, StringComparer.OrdinalIgnoreCase);
    }

    private static ManagementObject? FindNetworkAdapter(AdapterSnapshot adapter)
    {
        using var searcher = new ManagementObjectSearcher(
            "root\\CIMV2",
            "SELECT * FROM Win32_NetworkAdapter");
        foreach (ManagementObject networkAdapter in searcher.Get())
        {
            var guid = ReadString(networkAdapter, "GUID");
            if (MatchesAdapterIdentity(guid, adapter))
                return networkAdapter;
            networkAdapter.Dispose();
        }
        return null;
    }

    internal static bool MatchesAdapterIdentity(string guid, AdapterSnapshot requested) =>
        Guid.TryParse(requested.InterfaceGuid, out _) && SameId(guid, requested.InterfaceGuid);

    private static uint InvokeReturnCode(ManagementObject target, string method, object[] arguments,
        string operation, bool allowRebootRequired)
    {
        var output = target.InvokeMethod(method, arguments);
        var value = output is ManagementBaseObject result ? result["ReturnValue"] : output;
        var code = value is null ? 0u : Convert.ToUInt32(value, CultureInfo.InvariantCulture);
        if (code != 0 && !(allowRebootRequired && (code == 1 || code == 93)))
            throw new InvalidOperationException(FormatWmiError(code, operation));
        return code;
    }

    private static string GetConnectionStatus(ManagementObject adapter, bool enabled)
    {
        if (!enabled) return "Disabled";
        if (ReadInt(adapter, "ConfigManagerErrorCode", 0) == 45) return "Not Present";
        return ReadInt(adapter, "NetConnectionStatus", -1) switch
        {
            2 => "Up",
            7 => "Disconnected",
            0 => "Disconnected",
            _ => "Unknown"
        };
    }

    private static AdapterType Classify(string name, string description, bool physical)
    {
        var text = $"{name} {description}".ToLowerInvariant();
        if (text.Contains("vpn") || text.Contains("wireguard") || text.Contains("tun") || text.Contains("tap")) return AdapterType.Vpn;
        if (text.Contains("virtual") || text.Contains("hyper-v") || text.Contains("vmware") || text.Contains("loopback") || text.Contains("km-test") || text.Contains("npcap")) return AdapterType.Virtual;
        if (physical || text.Contains("ethernet") || text.Contains("wi-fi") || text.Contains("wireless")) return AdapterType.Physical;
        return AdapterType.Other;
    }

    private static string PrefixToMask(int prefixLength)
    {
        if (prefixLength is < 0 or > 32) throw new InvalidOperationException("IPv4 前缀长度无效。");
        var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        return $"{(mask >> 24) & 255}.{(mask >> 16) & 255}.{(mask >> 8) & 255}.{mask & 255}";
    }

    private static int ParsePrefix(string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var prefix)
            && prefix is >= 0 and <= 32) return prefix;
        if (!IPAddress.TryParse(value, out var mask) || mask.AddressFamily != AddressFamily.InterNetwork)
            return 32;
        var bytes = mask.GetAddressBytes();
        var result = 0;
        foreach (var b in bytes)
        {
            var current = b;
            for (var i = 0; i < 8 && (current & 0x80) != 0; i++)
            {
                result++;
                current <<= 1;
            }
            if (current != 0) return 32;
        }
        return result;
    }

    private static bool IsIPv4(string value) => IPAddress.TryParse(value, out var address)
        && address.AddressFamily == AddressFamily.InterNetwork;

    private static string[] ReadStrings(ManagementBaseObject source, string property)
    {
        object? value;
        try { value = source[property]; }
        catch { return []; }
        if (value is string text) return string.IsNullOrWhiteSpace(text) ? [] : [text];
        if (value is Array array)
            return array.Cast<object?>().Select(x => Convert.ToString(x, CultureInfo.InvariantCulture) ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        return [];
    }

    private static string ReadString(ManagementBaseObject source, string property)
    {
        try { return Convert.ToString(source[property], CultureInfo.InvariantCulture) ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static int ReadInt(ManagementBaseObject source, string property, int fallback)
    {
        try
        {
            var value = source[property];
            return value is null ? fallback : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch { return fallback; }
    }

    private static bool ReadBool(ManagementBaseObject source, string property, bool fallback)
    {
        try
        {
            var value = source[property];
            return value is null ? fallback : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch { return fallback; }
    }

    private static string NormalizeId(string? value) => (value ?? string.Empty).Trim().Trim('{', '}').ToUpperInvariant();

    private static bool SameId(string? left, string? right)
    {
        var normalizedLeft = NormalizeId(left);
        var normalizedRight = NormalizeId(right);
        return !string.IsNullOrWhiteSpace(normalizedLeft) && normalizedLeft == normalizedRight;
    }

    private static string FormatWmiError(uint code, string operation) => code switch
    {
        64 => $"{operation}失败：当前平台不支持此操作（WMI 64）。",
        84 => $"{operation}失败：网卡未启用 IPv4（WMI 84）。",
        91 => $"{operation}失败：权限不足（WMI 91）。",
        94 => $"{operation}失败：找不到路径或对象（WMI 94）。",
        97 => $"{operation}失败：接口不可配置（WMI 97）。",
        100 => $"{operation}失败：网卡未启用 DHCP（WMI 100）。",
        _ => $"{operation}失败，Windows 返回 WMI 错误码 {code}。"
    };

    private sealed record WmiConfigurationData(bool IpEnabled, bool DhcpEnabled,
        List<Ipv4AddressEntry> Addresses, List<string> DnsServers);
}
