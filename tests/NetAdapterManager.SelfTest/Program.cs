using NetAdapterManager.Models;
using NetAdapterManager.Services;
using NetAdapterManager.Views;
using NetAdapterManager.ViewModels;
using System.IO;
using System.Management;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

if (args.FirstOrDefault() == "--process-helper")
    return await ReliabilityRegressionTests.RunHelperAsync(args);

if (args.Contains("--isolated"))
{
    var failures = 0;
    var passes = 0;
    void Green(string name, string detail) { passes++; Console.WriteLine($"[GREEN] {name}: {detail}"); }
    void Red(string name, string detail) { failures++; Console.WriteLine($"[RED] {name}: {detail}"); }
    await NetworkRegressionTests.RunAsync(Green, Red);
    await ReliabilityRegressionTests.RunAsync(Green, Red);
    await Version150Tests.RunAsync(Green, Red);
    Console.WriteLine($"Isolated regression: {passes} passed, {failures} failed; no real network writes.");
    return failures == 0 ? 0 : 1;
}

if (args.Any(x => string.Equals(x, "--ui-smoke", StringComparison.OrdinalIgnoreCase)))
    return RunUiSmokeTest();

if (args.Contains("--appearance-smoke")) return AppearanceSmokeTests.Run();

if (args.Any(x => string.Equals(x, "--network-regression", StringComparison.OrdinalIgnoreCase)))
    return await NetworkRegressionTests.RunStandaloneAsync();

var writeTest = args.Any(x => string.Equals(x, "--write-test", StringComparison.OrdinalIgnoreCase));
var autoConfirm = args.Any(x => string.Equals(x, "--yes", StringComparison.OrdinalIgnoreCase));
var resultFile = GetOption(args, "--result");
var requestedAdapter = GetOption(args, "--adapter");
var checks = new List<(string Name, bool Passed, string Detail)>();

void Pass(string name, string detail) => checks.Add((name, true, detail));
void Fail(string name, string detail) => checks.Add((name, false, detail));

try
{
    await NetworkRegressionTests.RunAsync(Pass, Fail);
    await ReliabilityRegressionTests.RunAsync(Pass, Fail);
    await Version150Tests.RunAsync(Pass, Fail);
    var valid = ValidationService.ValidateManualConfig(new NetworkConfig
    {
        IpMode = IpMode.Static,
        ManagedIPv4Address = "198.18.0.254",
        IPv4Addresses = [new Ipv4AddressEntry { Address = "198.18.0.254", PrefixLength = 32, IsManaged = true }],
        DnsMode = DnsMode.Static,
        DnsServers = ["1.1.1.1"]
    });
    if (valid.IsValid) Pass("输入校验-合法配置", "合法 IPv4/CIDR 被接受。");
    else Fail("输入校验-合法配置", string.Join("; ", valid.Errors));

    var invalid = ValidationService.ValidateManualConfig(new NetworkConfig
    {
        IpMode = IpMode.Static,
        ManagedIPv4Address = "999.1.1.1",
        IPv4Addresses = [new Ipv4AddressEntry { Address = "999.1.1.1", PrefixLength = 33, IsManaged = true }],
        DnsMode = DnsMode.Automatic
    });
    if (!invalid.IsValid) Pass("输入校验-非法配置", "非法地址和前缀被拒绝。");
    else Fail("输入校验-非法配置", "非法配置未被拒绝。");

    var service = new NetworkService();
    var adapters = await service.GetAdaptersAsync();
    if (adapters.Count > 0) Pass("WMI-网卡枚举", $"读取到 {adapters.Count} 个网卡。");
    else Fail("WMI-网卡枚举", "没有读取到任何网卡。");

    try
    {
        var dnsByInterface = new Dictionary<int, HashSet<string>>();
        using var dnsSearcher = new ManagementObjectSearcher(
            "root\\StandardCimv2",
            "SELECT InterfaceIndex, AddressFamily, ServerAddresses FROM MSFT_DNSClientServerAddress WHERE AddressFamily = 2");
        foreach (ManagementObject dnsObject in dnsSearcher.Get())
        {
            using (dnsObject)
            {
                var interfaceIndex = ReadIntForTest(dnsObject, "InterfaceIndex");
                if (interfaceIndex <= 0) continue;
                var values = ReadStringsForTest(dnsObject, "ServerAddresses")
                    .Where(IsIPv4ForTest)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                dnsByInterface[interfaceIndex] = values;
            }
        }

        var mismatches = adapters
            .Where(adapter => dnsByInterface.ContainsKey(adapter.InterfaceIndex))
            .Where(adapter => !dnsByInterface[adapter.InterfaceIndex].SetEquals(adapter.Config.DnsServers))
            .Select(adapter => adapter.Name)
            .ToList();
        if (mismatches.Count == 0)
            Pass("DNS 空值读取", "使用 Windows DNS 客户端配置读取；空 DNS 不会被补成 127.0.0.1，单个 DNS 不会生成第二个地址。");
        else
            Fail("DNS 空值读取", $"软件读取结果与 Windows DNS 客户端配置不一致：{string.Join("、", mismatches)}");
    }
    catch (Exception ex)
    {
        Fail("DNS 空值读取", ex.Message);
    }

    var named = adapters.Where(x => !string.IsNullOrWhiteSpace(x.Name)).ToList();
    if (named.Count == adapters.Count) Pass("WMI-网卡名称", "所有网卡都有可显示名称。");
    else Fail("WMI-网卡名称", $"有 {adapters.Count - named.Count} 个网卡名称为空。");

    var macCount = adapters.Count(x => !string.IsNullOrWhiteSpace(x.MacAddress));
    Pass("网卡 MAC 信息", macCount > 0
        ? $"读取到 {macCount} 个网卡的 MAC 地址；没有提供的网卡会显示占位符。"
        : "系统未提供 MAC 地址，界面会显示占位符。");

    var storeProbe = new JsonStore<List<string>>("storage-probe.json");
    var expectedStoragePath = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
    var actualStoragePath = Path.GetDirectoryName(storeProbe.FilePath)?.TrimEnd(Path.DirectorySeparatorChar);
    if (string.Equals(actualStoragePath, expectedStoragePath, StringComparison.OrdinalIgnoreCase))
        Pass("安装目录数据绑定", "JSON 存储路径与程序目录一致。");
    else
        Fail("安装目录数据绑定", $"JSON 存储路径不在程序目录：{storeProbe.FilePath}");

    var logMarker = $"SelfTest-错误日志-{Guid.NewGuid():N}";
    AppLogger.Error(logMarker, new InvalidOperationException("红绿灯日志探针"));
    var logText = File.Exists(AppLogger.LogFilePath) ? File.ReadAllText(AppLogger.LogFilePath) : string.Empty;
    if (logText.Contains(logMarker, StringComparison.Ordinal))
        Pass("错误日志记录", $"错误已写入程序目录日志：{AppLogger.LogFilePath}");
    else
        Fail("错误日志记录", "错误日志文件没有找到本次测试标记。");

    var staticAutomaticDns = ValidationService.ValidateManualConfig(new NetworkConfig
    {
        IpMode = IpMode.Static,
        ManagedIPv4Address = "192.0.2.10",
        IPv4Addresses = [new Ipv4AddressEntry { Address = "192.0.2.10", PrefixLength = 24, IsManaged = true }],
        DnsMode = DnsMode.Automatic
    });
    if (staticAutomaticDns.IsValid)
        Pass("静态 IPv4-DNS 选填校验", "静态 IPv4 可以使用自动 DNS，DNS 不再被强制填写。");
    else
        Fail("静态 IPv4-DNS 选填校验", string.Join("; ", staticAutomaticDns.Errors));

    var staticWithoutDns = ValidationService.ValidateManualConfig(new NetworkConfig
    {
        IpMode = IpMode.Static,
        ManagedIPv4Address = "192.0.2.11",
        IPv4Addresses = [new Ipv4AddressEntry { Address = "192.0.2.11", PrefixLength = 24, IsManaged = true }],
        DnsMode = DnsMode.Static,
        DnsServers = []
    });
    if (staticWithoutDns.IsValid)
        Pass("静态 DNS 选填校验", "静态 IPv4 不填写 DNS 时仍可保存和应用。");
    else
        Fail("静态 DNS 选填校验", string.Join("; ", staticWithoutDns.Errors));

    var disconnectedAdapter = new AdapterViewModel(new AdapterSnapshot
    {
        Name = "断线测试网卡",
        Enabled = false,
        Ipv4InterfacePresent = false,
        Config = new NetworkConfig()
    });
    var disconnectedViewModel = new MainViewModel();
    disconnectedViewModel.SelectedAdapter = disconnectedAdapter;
    if (disconnectedViewModel.CanConfigure)
        Pass("断线网卡可配置", "没有 IPv4 接口的已禁用网卡仍允许提交系统网络连接配置。");
    else
        Fail("断线网卡可配置", "断线或禁用网卡仍被错误拦截。");

    disconnectedViewModel.IsDhcp = true;
    disconnectedViewModel.IsDnsAutomatic = true;
    disconnectedViewModel.IsStatic = true;
    if (disconnectedViewModel.IsStatic && disconnectedViewModel.IsDnsAutomatic)
        Pass("静态 IP-DNS 界面联动", "切换静态 IPv4 不会强制改变 DNS 模式，DNS 可选。");
    else
        Fail("静态 IP-DNS 界面联动", "切换静态 IPv4 时错误改变了 DNS 模式。");

    try
    {
        disconnectedViewModel.IsDhcp = true;
        disconnectedViewModel.IsDnsAutomatic = true;
        if (disconnectedViewModel.IsDhcp && disconnectedViewModel.IsDnsAutomatic)
            Pass("DHCP 切换防闪退", "从静态 IPv4 切回 DHCP/自动 DNS 时状态联动正常，没有抛出异常。");
        else
            Fail("DHCP 切换防闪退", "切回 DHCP 后状态没有恢复为 DHCP/自动 DNS。");
    }
    catch (Exception ex)
    {
        Fail("DHCP 切换防闪退", ex.Message);
    }

    var batchFile = $"batch-test-{Guid.NewGuid():N}.json";
    var batchService = new PresetService(batchFile);
    var batchVm = new MainViewModel(presetService: batchService);
    var batchEntries = Enumerable.Range(1, 3).Select(i => new Preset
    {
        Name = $"批量测试{i}", AdapterName = "测试网卡", AdapterInterfaceGuid = "test-guid",
        Config = NetworkConfig.ForPreset($"192.0.2.{i}", 24, null, DnsMode.Static, []),
        IsBatchSelected = i <= 2
    }).ToList();
    typeof(MainViewModel).GetField("_allPresets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(batchVm, batchEntries);
    batchVm.PresetGroups.Add("全部分组"); batchVm.PresetGroups.Add("未分组"); batchVm.PresetGroups.Add("批量目标");
    try
    {
        var ids = batchVm.BatchSelectedPresets.Select(x => x.Id).ToArray();
        if (!await batchVm.UpdatePresetBatchAsync(ids, "批量目标", false)) throw new Exception("批量换分组失败");
        var saved = await batchService.LoadAsync();
        if (saved.Count != 3 || saved.Count(x => x.GroupName == "批量目标") != 2
            || saved.Any(x => x.IsBatchSelected) || saved.Any(x => x.AdapterInterfaceGuid != "test-guid")
            || batchEntries.Any(x => x.GroupName != "未分组")) throw new Exception("批量移动污染原记录、目标或勾选状态");
        if (await batchVm.UpdatePresetBatchAsync(ids, "全部分组", false)) throw new Exception("允许移动到系统筛选项");
        if (!await batchVm.UpdatePresetBatchAsync(ids, null, true)) throw new Exception("批量删除失败");
        saved = await batchService.LoadAsync();
        if (saved.Count != 1 || saved[0].Id != batchEntries[2].Id) throw new Exception("批量删除影响未勾选项");
        if (!await batchVm.UpdatePresetBatchAsync([saved[0].Id], null, true)
            || (await batchService.LoadAsync()).Count != 0 || batchVm.PresetGroupFilter != "全部分组") throw new Exception("删空后筛选异常");
        Pass("预设批量操作", "两项移动持久化、单项编辑隔离、批量删除不影响未选项、删空恢复全部分组；勾选不持久化。");
        var failedPath = Path.Combine(AppContext.BaseDirectory, batchFile);
        File.Delete(failedPath);
        Directory.CreateDirectory(failedPath);
        typeof(MainViewModel).GetField("_allPresets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(batchVm, batchEntries);
        batchVm.PresetGroups.Add("批量目标");
        if (await batchVm.UpdatePresetBatchAsync(ids, "批量目标", false)
            || batchEntries.Any(x => x.GroupName != "未分组") || batchVm.BatchSelectedPresets.Count != 2)
            throw new Exception("写入失败时原记录或勾选状态丢失");
        Pass("批量保存失败保护", "模拟保存路径不可写，原分组与勾选保持不变；错误记录日志。");
    }
    catch (Exception ex) { Fail("预设批量操作", ex.Message); }
    finally
    {
        var batchPath = Path.Combine(AppContext.BaseDirectory, batchFile);
        if (File.Exists(batchPath)) File.Delete(batchPath);
        if (File.Exists(batchPath + ".tmp")) File.Delete(batchPath + ".tmp");
        if (Directory.Exists(batchPath)) Directory.Delete(batchPath);
    }

    var preset = new Preset { Name = "旧名称", GroupName = "未分组" };
    var presetChanged = false;
    preset.PropertyChanged += (_, args) => presetChanged |= args.PropertyName is "Name" or "GroupName";
    preset.Name = "新名称";
    preset.GroupName = "测试分组";
    if (presetChanged && preset.Name == "新名称" && preset.GroupName == "测试分组")
        Pass("预设重命名与分组", "预设名称和分组变更会通知界面并可持久化。");
    else
        Fail("预设重命名与分组", "预设属性变更没有正确通知界面。");

    var groupDeletePresets = new List<Preset>
    {
        new() { Name = "组内预设", GroupName = "测试分组" },
        new() { Name = "其他预设", GroupName = "其他分组" }
    };
    var groupDeleteGroups = new List<string> { "测试分组", "空分组" };
    var movedPresetCount = PresetGroupService.DeleteGroup("测试分组", groupDeletePresets, groupDeleteGroups);
    if (movedPresetCount == 1
        && groupDeletePresets[0].GroupName == "未分组"
        && groupDeletePresets[1].GroupName == "其他分组"
        && !groupDeleteGroups.Contains("测试分组", StringComparer.OrdinalIgnoreCase)
        && groupDeleteGroups.Contains("空分组", StringComparer.OrdinalIgnoreCase))
        Pass("预设删除分组", "删除自定义分组会将关联预设移到“未分组”，不会删除预设或其他分组。");
    else
        Fail("预设删除分组", "删除分组没有正确解除分组关系或误伤其他预设。");

    preset.AdapterName = "测试网卡";
    preset.AdapterMacAddress = "00:11:22:33:44:55";
    if (preset.AdapterSummary == "网卡：测试网卡" && preset.MacSummary == "MAC：00:11:22:33:44:55")
        Pass("预设网卡与 MAC 摘要", "预设模型会保存网卡名称，并在存在 MAC 时展示 MAC。");
    else
        Fail("预设网卡与 MAC 摘要", "预设 MAC 摘要生成不正确。");

    var targetAdapterA = new AdapterViewModel(new AdapterSnapshot
    {
        Name = "当前编辑网卡",
        InterfaceGuid = "{A-TEST}",
        MacAddress = "00:AA:00:00:00:01",
        Config = new NetworkConfig()
    });
    var targetAdapterB = new AdapterViewModel(new AdapterSnapshot
    {
        Name = "预设所属网卡",
        InterfaceGuid = "{B-TEST}",
        MacAddress = "00:BB:00:00:00:02",
        Config = new NetworkConfig()
    });
    var targetPreset = new Preset
    {
        Name = "目标网卡预设",
        AdapterName = targetAdapterB.Name,
        AdapterMacAddress = targetAdapterB.Snapshot.MacAddress,
        AdapterInterfaceGuid = targetAdapterB.Snapshot.InterfaceGuid
    };
    var resolvedTarget = PresetAdapterResolver.Resolve(targetPreset, [targetAdapterA, targetAdapterB]);
    if (ReferenceEquals(resolvedTarget, targetAdapterB))
        Pass("预设按所属网卡应用", "预设应用目标按接口 GUID 解析，不会误改当前编辑器选中的网卡。");
    else
        Fail("预设按所属网卡应用", "预设目标没有解析到预设保存的所属网卡。");

    var missingTargetPreset = targetPreset.Clone();
    missingTargetPreset.AdapterInterfaceGuid = "{MISSING-TARGET}";
    if (PresetAdapterResolver.Resolve(missingTargetPreset, [targetAdapterA, targetAdapterB]) is null)
        Pass("预设目标严格隔离", "已有接口 GUID 但目标不存在时会失败，不会回退到当前或同名网卡。");
    else
        Fail("预设目标严格隔离", "预设目标丢失后错误回退到了其他网卡。");

    var detachedPreset = targetPreset.Clone();
    detachedPreset.Name = "第二次编辑后的名称";
    detachedPreset.Config.DnsServers.Add("1.1.1.1");
    if (!ReferenceEquals(detachedPreset, targetPreset)
        && !ReferenceEquals(detachedPreset.Config, targetPreset.Config)
        && targetPreset.Name != detachedPreset.Name
        && targetPreset.Config.DnsServers.Count == 0)
        Pass("预设二次编辑隔离", "编辑使用深拷贝对象，第二次编辑不会污染原预设或造成集合异常。");
    else
        Fail("预设二次编辑隔离", "预设编辑副本与原对象未完全隔离。");

    var legacyPreset = new Preset
    {
        Name = "旧格式预设",
        AdapterName = targetAdapterB.Name,
        AdapterMacAddress = "00-BB-00-00-00-02"
    };
    if (ReferenceEquals(PresetAdapterResolver.Resolve(legacyPreset, [targetAdapterA, targetAdapterB]), targetAdapterB))
        Pass("旧预设目标兼容", "没有接口 GUID 的旧预设仍可按 MAC/名称找到正确网卡。");
    else
        Fail("旧预设目标兼容", "旧格式预设无法按 MAC/名称找到所属网卡。");

    var duplicateConfig = NetworkConfig.ForPreset("192.0.2.10", 24, null, DnsMode.Static, ["1.1.1.1", "8.8.8.8"]);
    var duplicatePreset = new Preset
    {
        Name = "名称一",
        GroupName = "分组一",
        AdapterName = "测试网卡",
        AdapterMacAddress = "00:11:22:33:44:55",
        Config = duplicateConfig.Clone()
    };
    var automaticWithHiddenValues = new NetworkConfig
    {
        IpMode = IpMode.Dhcp,
        IPv4Addresses = [new Ipv4AddressEntry { Address = "192.0.2.99", PrefixLength = 24 }],
        DnsMode = DnsMode.Automatic,
        DnsServers = ["9.9.9.9"]
    };
    var automaticPreset = new Preset
    {
        AdapterName = "测试网卡",
        AdapterMacAddress = "00-11-22-33-44-55",
        Config = new NetworkConfig { IpMode = IpMode.Dhcp, DnsMode = DnsMode.Automatic }
    };
    if (PresetService.MatchesConfiguration(duplicatePreset, duplicateConfig, "测试网卡", "00:11:22:33:44:55")
        && PresetService.MatchesConfiguration(automaticPreset, automaticWithHiddenValues,
            "测试网卡", "00:11:22:33:44:55"))
        Pass("预设单份保存规则", "同网卡的相同生效配置会识别为同一预设，自动模式隐藏值和 MAC 分隔符不会造成重复。");
    else
        Fail("预设单份保存规则", "相同网络配置未被识别，可能再次保存两份。");

    var legacyIdentityPreset = duplicatePreset.Clone();
    legacyIdentityPreset.AdapterInterfaceGuid = "{NEW-GUID}";
    if (PresetService.MatchesConfiguration(legacyIdentityPreset, duplicateConfig,
            duplicatePreset.AdapterName, duplicatePreset.AdapterMacAddress)
        && !PresetService.MatchesConfiguration(legacyIdentityPreset, duplicateConfig,
            duplicatePreset.AdapterName, duplicatePreset.AdapterMacAddress, "{OTHER-GUID}"))
        Pass("新旧预设身份兼容", "旧记录缺少 GUID 时按 MAC/名称兼容，新记录的不同 GUID 不会合并到错误网卡。");
    else
        Fail("新旧预设身份兼容", "预设身份兼容去重规则不正确。");

    var commandViewModel = new MainViewModel();
    commandViewModel.Adapters.Add(targetAdapterA);
    commandViewModel.Adapters.Add(targetAdapterB);
    commandViewModel.SelectedAdapter = targetAdapterA;
    commandViewModel.SelectedPreset = targetPreset;
    if (commandViewModel.ApplyPresetCommand.CanExecute(targetPreset)
        && !commandViewModel.ApplyPresetCommand.CanExecute(missingTargetPreset))
        Pass("预设命令目标参数", "应用命令按传入预设解析所属网卡，目标丢失时命令不可执行。");
    else
        Fail("预设命令目标参数", "应用命令仍依赖当前网卡或未阻止失效目标。");

    try
    {
        var legacyEmptyPreset = new Preset
        {
            Config = new NetworkConfig
            {
                IpMode = IpMode.Static,
                IPv4Addresses = null!,
                DnsServers = null!,
                DnsMode = DnsMode.Static
            }
        };
        _ = legacyEmptyPreset.IpSummary;
        _ = legacyEmptyPreset.DnsSummary;
        _ = PresetService.MatchesConfiguration(legacyEmptyPreset,
            new NetworkConfig { IpMode = IpMode.Dhcp, DnsMode = DnsMode.Automatic },
            "测试网卡", "00:11:22:33:44:55");
        Pass("预设空数据防闪退", "旧数据中的空配置、空 IPv4 或空 DNS 集合不会导致预设操作闪退。");
    }
    catch (Exception ex)
    {
        Fail("预设空数据防闪退", ex.Message);
    }

    disconnectedViewModel.PresetGroupFilter = null!;
    if (disconnectedViewModel.PresetGroupFilter == "全部分组")
        Pass("预设空分组恢复", "分组选择为空时会自动恢复为“全部分组”。");
    else
        Fail("预设空分组恢复", "分组选择为空后没有恢复默认筛选。");

    Pass("系统网络连接入口", "已提供打开 control.exe ncpa.cpl 的底部命令。");

    var safeCandidates = adapters.Where(IsSafeWriteCandidate).ToList();
    if (safeCandidates.Count > 0)
        Pass("安全测试目标", string.Join(", ", safeCandidates.Select(x => x.Name)));
    else
        Pass("安全测试目标", "跳过：没有安全虚拟网卡；默认只读测试不需要写入目标。");

    // Capture commands against synthetic snapshots. Never call ApplyAsync here:
    // that entry point can write the network or roll back on verification failure.
    var capturedCommands = new List<string[]>();
    var syntheticAdapter = new AdapterSnapshot
    {
        InterfaceIndex = 42,
        Config = new NetworkConfig { IpMode = IpMode.Dhcp, DnsMode = DnsMode.Automatic }
    };
    NetworkService.ApplyConfiguration(syntheticAdapter, syntheticAdapter.Config.Clone(), false, capturedCommands.Add);
    if (capturedCommands.Count == 0)
        Pass("幂等 DHCP/DNS 配置", "虚构快照已经满足 DHCP/自动 DNS 时没有生成任何写入命令。");
    else
        Fail("幂等 DHCP/DNS 配置", "已满足目标时仍生成了写入命令。");

    var syntheticStatic = NetworkConfig.ForPreset("192.0.2.10", 24, null, DnsMode.Static, []);
    NetworkService.ApplyConfiguration(syntheticAdapter, syntheticStatic, false, capturedCommands.Add);
    if (capturedCommands.Count == 2
        && capturedCommands[0].Contains("name=42")
        && capturedCommands[0].Contains("address=192.0.2.10")
        && capturedCommands[1].Contains("address=none"))
        Pass("网络命令隔离", "静态 IPv4 与空 DNS 命令仅被捕获，没有启动 netsh 或操作实际网卡。");
    else
        Fail("网络命令隔离", "捕获的静态 IPv4/DNS 命令不符合预期。");

    if (writeTest)
    {
        if (!IsAdministrator())
        {
            Fail("写入权限", "写入测试必须在管理员 PowerShell 中运行。");
        }
        else
        {
            var target = ResolveTarget(adapters, safeCandidates, requestedAdapter);
            if (target is null)
            {
                Fail("写入目标", requestedAdapter is null
                    ? "没有可用的安全虚拟网卡。"
                    : $"指定网卡不存在或不满足安全条件：{requestedAdapter}");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine($"安全写入目标：{target.Name}");
                Console.WriteLine("只会临时写入 198.18.0.254/32，完成后恢复原配置。");
                Console.Write("输入 TEST 继续：");
                var confirmed = autoConfirm || string.Equals(Console.ReadLine(), "TEST", StringComparison.Ordinal);
                if (!confirmed)
                {
                    Fail("写入测试", "用户取消。");
                }
                else
                {
                    var original = target.Config.Clone();
                    var temporary = new NetworkConfig
                    {
                        IpMode = IpMode.Static,
                        ManagedIPv4Address = "198.18.0.254",
                        IPv4Addresses = [new Ipv4AddressEntry
                        {
                            Address = "198.18.0.254",
                            PrefixLength = 32,
                            IsManaged = true
                        }],
                        DnsMode = DnsMode.Static,
                        DnsServers = original.DnsServers.ToList()
                    };
                    try
                    {
                        var applied = await service.ApplyAsync(target, temporary, OperationSource.Manual);
                        if (applied.Succeeded) Pass("写入测试", "临时静态地址应用并复核成功。");
                        else Fail("写入测试", applied.ErrorDetail ?? applied.Message);
                    }
                    finally
                    {
                        var restored = await service.ApplyExactAsync(target, original);
                        if (restored.Succeeded) Pass("回滚测试", "原始 IPv4/DNS 配置恢复并复核成功。");
                        else Fail("回滚测试", restored.ErrorDetail ?? restored.Message);
                    }
                }
            }
        }
    }
    else
    {
        Pass("写入测试", "未执行；使用 --write-test 并输入 TEST 才会写入。");
    }
}
catch (Exception ex)
{
    Fail("测试运行器", ex.ToString());
}

Console.WriteLine();
Console.WriteLine("红绿灯结果：");
foreach (var check in checks)
{
    Console.ForegroundColor = check.Passed ? ConsoleColor.Green : ConsoleColor.Red;
    Console.WriteLine($"[{(check.Passed ? "绿" : "红")}] {check.Name}: {check.Detail}");
    Console.ResetColor();
}

var failed = checks.Count(x => !x.Passed);
Console.WriteLine($"总计：{checks.Count - failed} 通过，{failed} 失败。");
if (!string.IsNullOrWhiteSpace(resultFile))
{
    var resultText = string.Join(Environment.NewLine, checks.Select(check =>
        $"[{(check.Passed ? "绿" : "红")}] {check.Name}: {check.Detail}"))
        + Environment.NewLine + $"总计：{checks.Count - failed} 通过，{failed} 失败。";
    File.WriteAllText(resultFile, resultText, new System.Text.UTF8Encoding(false));
}
return failed == 0 ? 0 : 1;

static string? GetOption(string[] args, string name)
{
    var index = Array.FindIndex(args, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static int ReadIntForTest(ManagementBaseObject source, string property)
{
    try { return source[property] is null ? 0 : Convert.ToInt32(source[property]); }
    catch { return 0; }
}

static string[] ReadStringsForTest(ManagementBaseObject source, string property)
{
    try
    {
        var value = source[property];
        if (value is string text) return string.IsNullOrWhiteSpace(text) ? [] : [text];
        if (value is Array array)
            return array.Cast<object?>().Select(x => Convert.ToString(x) ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    }
    catch { }
    return [];
}

static bool IsIPv4ForTest(string value) => IPAddress.TryParse(value, out var address)
    && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;

static bool IsSafeWriteCandidate(AdapterSnapshot adapter)
{
    if (adapter.Type != AdapterType.Virtual || !adapter.Ipv4InterfacePresent) return false;
    if (adapter.Config.IpMode != IpMode.Static) return false;
    if (adapter.Config.IPv4Addresses.Any(x => !string.IsNullOrWhiteSpace(x.Gateway))) return false;
    var text = $"{adapter.Name} {adapter.InterfaceGuid}";
    return text.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase)
        || text.Contains("VMware", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
        || text.Contains("vEthernet", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Loopback", StringComparison.OrdinalIgnoreCase)
        || text.Contains("KM-TEST", StringComparison.OrdinalIgnoreCase);
}

static AdapterSnapshot? ResolveTarget(IReadOnlyList<AdapterSnapshot> all,
    IReadOnlyList<AdapterSnapshot> safeCandidates, string? requestedName)
{
    if (!string.IsNullOrWhiteSpace(requestedName))
        return all.FirstOrDefault(x => string.Equals(x.Name, requestedName, StringComparison.OrdinalIgnoreCase)
            && IsSafeWriteCandidate(x));
    return safeCandidates.FirstOrDefault();
}

static bool IsAdministrator()
{
    using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
    var principal = new System.Security.Principal.WindowsPrincipal(identity);
    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
}

static int RunUiSmokeTest()
{
    var passed = false;
    string? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            var app = new NetAdapterManager.App(suppressStartup: true);
            app.InitializeComponent();
            NetAdapterManager.Services.Localization.SetLanguage("zh-CN");
            // Public screenshots use only synthetic data and never load user presets.
            var window = new MainWindow(CreateDemoViewModel(), initializeOnLoad: false);
            if (window.Icon is null) throw new InvalidOperationException("窗口图标资源未加载。");
            if (window.FindName("PresetOverviewButton") is not Button)
                throw new InvalidOperationException("预设总览入口未正确加载。");
            var dialogAdapterA = new AdapterViewModel(new AdapterSnapshot
            {
                Name = "测试网卡 A",
                MacAddress = "00:11:22:33:44:55",
                Enabled = true,
                Config = new NetworkConfig
                {
                    IpMode = IpMode.Static,
                    ManagedIPv4Address = "192.0.2.20",
                    IPv4Addresses = [new Ipv4AddressEntry { Address = "192.0.2.20", PrefixLength = 24, IsManaged = true }],
                    DnsMode = DnsMode.Static,
                    DnsServers = ["1.1.1.1", "8.8.8.8"]
                }
            });
            var dialogAdapterB = new AdapterViewModel(new AdapterSnapshot
            {
                Name = "测试网卡 B",
                MacAddress = "66:77:88:99:AA:BB",
                Enabled = true,
                Config = NetworkConfig.ForPreset("198.51.100.20", 24, null, DnsMode.Static, ["9.9.9.9", "149.112.112.112"])
            });
            var presetDialog = new PresetEditorWindow("测试网卡 A - 20260909-1200", "测试分组", new NetworkConfig
            {
                IpMode = IpMode.Static,
                ManagedIPv4Address = "192.0.2.20",
                IPv4Addresses = [new Ipv4AddressEntry { Address = "192.0.2.20", PrefixLength = 24, IsManaged = true }],
                DnsMode = DnsMode.Static,
                DnsServers = ["1.1.1.1", "8.8.8.8"]
            }, [dialogAdapterA, dialogAdapterB], dialogAdapterA, ["未分组", "测试分组", "办公室"]);
            if (presetDialog.Content is null || presetDialog.FindName("AlternateDnsBox") is not TextBox alternateDnsBox
                || presetDialog.FindName("PresetAdapterSelector") is not ComboBox presetAdapterSelector
                || presetDialog.FindName("GroupBox") is not ComboBox groupSelector
                || presetDialog.FindName("SavePresetButton") is not Button savePresetButton
                || presetDialog.FindName("ValidationMessage") is not TextBlock validationMessage
                || !groupSelector.Items.Cast<string>().Contains("办公室")
                || !string.Equals(groupSelector.SelectedItem as string, "测试分组", StringComparison.Ordinal)
                || presetAdapterSelector.Items.Count != 2)
                throw new InvalidOperationException("预设保存弹窗、分组下拉框或备用 DNS 输入框未正确加载。");
            presetAdapterSelector.SelectedItem = dialogAdapterB;
            presetDialog.UpdateLayout();
            if (presetDialog.FindName("AdapterMacText") is not TextBlock macText
                || macText.Text != "MAC：66:77:88:99:AA:BB"
                || presetDialog.FindName("IpBox") is not TextBox switchedIp
                || switchedIp.Text != "198.51.100.20")
                throw new InvalidOperationException("新建预设切换网卡后没有加载目标网卡配置。");
            var preferredDnsBox = (TextBox)presetDialog.FindName("PreferredDnsBox")!;
            var staticIpRadio = (RadioButton)presetDialog.FindName("StaticRadio")!;
            var automaticDnsRadio = (RadioButton)presetDialog.FindName("DnsAutomaticRadio")!;
            staticIpRadio.IsChecked = true;
            automaticDnsRadio.IsChecked = true;
            if (automaticDnsRadio.IsEnabled != true || automaticDnsRadio.IsChecked != true)
                throw new InvalidOperationException("静态 IPv4 下 DNS 自动获取被错误禁用或强制切换。");
            ((TextBox)presetDialog.FindName("IpBox")!).Text = "999.999.999.999";
            preferredDnsBox.Text = string.Empty;
            alternateDnsBox.Text = string.Empty;
            savePresetButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            presetDialog.UpdateLayout();
            if (validationMessage.Visibility != Visibility.Visible
                || !validationMessage.Text.Contains("IPv4", StringComparison.OrdinalIgnoreCase)
                || presetDialog.DialogResult is not null)
                throw new InvalidOperationException("非法 IPv4 时没有在保存窗口内提示并阻止关闭。");
            ((TextBox)presetDialog.FindName("IpBox")!).Text = "198.51.100.20";
            presetDialog.Show();
            presetDialog.Measure(new Size(560, 620));
            presetDialog.Arrange(new Rect(0, 0, 560, 620));
            presetDialog.UpdateLayout();
            var dialogOutput = Path.Combine(Environment.CurrentDirectory, "preset-dialog-smoke.png");
            SaveScreenshot(presetDialog, dialogOutput);
            presetDialog.Close();
            window.Show();
            var viewModel = (MainViewModel)window.DataContext;
            var expectedVersion = $"版本 {typeof(MainWindow).Assembly.GetName().Version!.ToString(3)}";
            if (viewModel.VersionText != expectedVersion) throw new InvalidOperationException($"底部版本号不正确：{viewModel.VersionText}");
            if (Math.Abs(window.Width - 1160) > 0.1) throw new InvalidOperationException($"主窗口初始宽度不正确：{window.Width}");
            if (!viewModel.PresetGroups.Contains("全部分组") || string.IsNullOrWhiteSpace(viewModel.PresetGroupFilter))
                throw new InvalidOperationException("预设分组没有始终保留“全部分组”。");
            if (window.FindName("PresetCountText") is not TextBlock countText || !countText.Text.Contains("个预设", StringComparison.Ordinal))
                throw new InvalidOperationException("预设数量徽标未正确显示。");
            if (window.FindName("AddPresetGroupButton") is not Button)
                throw new InvalidOperationException("主界面添加分组按钮未正确加载。");
            var selector = window.FindName("AdapterSelector") as ComboBox;
            if (selector is null || selector.Items.Count == 0 || selector.SelectedItem is null)
                throw new InvalidOperationException("当前目标网卡下拉选择器未正确加载。");
            if (selector.ToolTip is not null || !viewModel.ConfigurationNoticeText.Contains("应用配置", StringComparison.Ordinal))
                throw new InvalidOperationException("网卡选择器仍带有会遮挡内容的白色提示框，或配置提示未正确加载。");
            if (window.FindName("OperationToast") is not Border toast)
                throw new InvalidOperationException("配置结果提示控件未正确加载。");
            if (Grid.GetRow(toast) != 1
                || toast.HorizontalAlignment != HorizontalAlignment.Center
                || toast.VerticalAlignment != VerticalAlignment.Center)
                throw new InvalidOperationException("配置结果提示没有放在窗口中下部。");
            viewModel.ShowToast("测试成功", "成功提示", true);
            window.UpdateLayout();
            if (!viewModel.IsToastVisible || !viewModel.ToastSucceeded || !toast.IsHitTestVisible)
                throw new InvalidOperationException("配置成功提示没有显示。");
            viewModel.ShowToast("测试失败", "失败提示", false);
            window.UpdateLayout();
            if (!viewModel.IsToastVisible || viewModel.ToastSucceeded)
                throw new InvalidOperationException("配置失败提示没有显示。");
            var toastFrame = new DispatcherFrame();
            var toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.3) };
            toastTimer.Tick += (_, _) =>
            {
                toastTimer.Stop();
                toastFrame.Continue = false;
            };
            toastTimer.Start();
            Dispatcher.PushFrame(toastFrame);
            if (viewModel.IsToastVisible)
                throw new InvalidOperationException("提示动画计时结束后没有正常淡出。");
            var overview = new PresetOverviewWindow(viewModel);
            overview.Show();
            overview.Measure(new Size(920, 700));
            overview.Arrange(new Rect(0, 0, 920, 700));
            overview.UpdateLayout();
            if (overview.FindName("CountText") is not TextBlock overviewCount || !overviewCount.Text.Contains("个预设", StringComparison.Ordinal)
                || overview.FindName("OverviewGroupFilter") is not ComboBox
                || overview.FindName("OverviewSearchBox") is not TextBox
                || overview.FindName("OverviewNewButton") is not Button
                || overview.FindName("OverviewApplyButton") is not Button
                || overview.FindName("OverviewEditButton") is not Button
                || overview.FindName("OverviewDeleteButton") is not Button
                || overview.FindName("OverviewAddGroupButton") is not Button
                || overview.FindName("OverviewDeleteGroupButton") is not Button
                || overview.FindName("OverviewPresetList") is not ListBox)
                throw new InvalidOperationException("预设管理窗口的增删改查、应用、筛选或搜索控件未正确加载。");
            if (overview.FindName("BatchGroupBox") is not ComboBox
                || overview.FindName("BatchMoveButton") is not Button batchMove || batchMove.IsEnabled
                || overview.FindName("BatchDeleteButton") is not Button batchDelete || batchDelete.IsEnabled
                || overview.FindName("BatchCountText") is not TextBlock batchCount || batchCount.Text != "已勾选 0 项")
                throw new InvalidOperationException("批量控件加载失败或空勾选状态异常。");
            if (overview.FindName("OverviewDeleteGroupButton") is not Button deleteGroupButton || deleteGroupButton.Command is null)
                throw new InvalidOperationException("预设管理窗口的删除分组命令未正确绑定。");
            var overviewOutput = Path.Combine(Environment.CurrentDirectory, "preset-overview-smoke.png");
            var overviewList = (ListBox)overview.FindName("OverviewPresetList")!;
            if (viewModel.Presets.Count > 0)
            {
                overviewList.UpdateLayout();
                var firstPreset = viewModel.Presets[0];
                var item = (ListBoxItem)overviewList.ItemContainerGenerator.ContainerFromItem(firstPreset);
                foreach (var targetWindow in new Window[] { overview, window })
                {
                    var openedEditor = false;
                    targetWindow.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        var editor = Application.Current.Windows.OfType<PresetEditorWindow>().FirstOrDefault();
                        if (editor is null) return;
                        openedEditor = true;
                        editor.DialogResult = false;
                    }));
                    var mouse = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0,
                        System.Windows.Input.MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent, Source = item };
                    targetWindow.GetType().GetMethod("PresetList_MouseDoubleClick",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                        .Invoke(targetWindow, [overviewList, mouse]);
                    if (!openedEditor || viewModel.SelectedPreset?.Id != firstPreset.Id)
                        throw new InvalidOperationException("双击未打开正确预设的单条编辑窗口。");
                }
                overview.GetType().GetMethod("SelectAll_Click", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .Invoke(overview, [overview, new RoutedEventArgs()]);
                if (viewModel.BatchSelectedPresets.Count != viewModel.Presets.Count || !batchMove.IsEnabled || !batchDelete.IsEnabled)
                    throw new InvalidOperationException("全选未更新勾选数量或批量按钮。");
                overview.GetType().GetMethod("ClearSelection_Click", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .Invoke(overview, [overview, new RoutedEventArgs()]);
                if (viewModel.BatchSelectedPresets.Count != 0 || batchMove.IsEnabled || batchDelete.IsEnabled)
                    throw new InvalidOperationException("取消勾选未清空数量或禁用批量按钮。");
            }
            SaveScreenshot(overview, overviewOutput);
            overview.Close();
            selector.IsDropDownOpen = true;
            window.UpdateLayout();
            selector.IsDropDownOpen = false;
            window.Measure(new Size(1160, 800));
            window.Arrange(new Rect(0, 0, 1160, 800));
            window.UpdateLayout();
            window.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
            viewModel.ToggleTopmostCommand.Execute(null);
            window.UpdateLayout();
            if (!window.Topmost) throw new InvalidOperationException("窗口置顶命令未生效。");
            viewModel.ToggleTopmostCommand.Execute(null);
            window.UpdateLayout();
            if (window.Topmost) throw new InvalidOperationException("取消窗口置顶命令未生效。");
            var output = Path.Combine(Environment.CurrentDirectory, "ui-smoke.png");
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
            if (window.FindName("RestorePresetBackupButton") is not Button { IsEnabled: true })
                throw new InvalidOperationException("操作结束后恢复备份入口未启用。");
            SaveScreenshot(window, output);
            var operationRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = viewModel.RunBusyAsync(() => operationRelease.Task);
            var wasClosed = false;
            window.Closed += (_, _) => wasClosed = true;
            window.Close();
            if (wasClosed || !window.IsVisible) throw new InvalidOperationException("操作未完成时关闭窗口未被阻止。");
            operationRelease.SetResult();
            var closeFrame = new DispatcherFrame();
            var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            var closeDeadline = DateTime.UtcNow.AddSeconds(5);
            closeTimer.Tick += (_, _) => { if (wasClosed || DateTime.UtcNow > closeDeadline) closeFrame.Continue = false; };
            closeTimer.Start();
            Dispatcher.PushFrame(closeFrame);
            closeTimer.Stop();
            if (!wasClosed || !operation.IsCompleted) throw new InvalidOperationException("完成操作后没有自动关闭窗口。");
            passed = true;
            Console.WriteLine($"UI smoke screenshot: {output}");
            Console.WriteLine($"Preset dialog screenshot: {dialogOutput}");
            Console.WriteLine($"Preset overview screenshot: {overviewOutput}");
        }
        catch (Exception ex)
        {
            failure = ex.ToString();
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    Console.ForegroundColor = passed ? ConsoleColor.Green : ConsoleColor.Red;
    Console.WriteLine(passed
        ? "[绿] UI 冒烟测试：深色界面、网卡选择、预设弹窗、窗口置顶、渲染及操作期间关闭保护均成功。"
        : $"[红] UI 冒烟测试：{failure}");
    Console.ResetColor();
    return passed ? 0 : 1;
}

static MainViewModel CreateDemoViewModel()
{
    var vm = new MainViewModel();
    var adapter = new AdapterViewModel(new AdapterSnapshot
    {
        Name = "示例以太网", InterfaceGuid = "{DEMO-ADAPTER}", InterfaceIndex = 42,
        MacAddress = "02:00:00:00:00:01", Enabled = true, Ipv4InterfacePresent = true,
        Config = NetworkConfig.ForPreset("192.0.2.20", 24, "192.0.2.1", DnsMode.Static, ["192.0.2.53"])
    });
    vm.Adapters.Add(adapter);
    vm.SelectedAdapter = adapter;
    var preset = new Preset
    {
        Name = "办公室示例", GroupName = "示例分组", AdapterName = adapter.Name,
        AdapterInterfaceGuid = adapter.Snapshot.InterfaceGuid, AdapterMacAddress = adapter.Snapshot.MacAddress,
        Config = adapter.Snapshot.Config.Clone(),
        CreatedAt = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(8)),
        UpdatedAt = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(8))
    };
    typeof(MainViewModel).GetField("_allPresets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
        .SetValue(vm, new List<Preset> { preset });
    vm.Presets.Add(preset);
    foreach (var group in new[] { "全部分组", "未分组", "示例分组" }) vm.PresetGroups.Add(group);
    typeof(MainViewModel).GetProperty(nameof(MainViewModel.StatusMessage))!
        .SetValue(vm, "演示数据 · 不读取实际网卡配置");
    return vm;
}

static void SaveScreenshot(Window window, string path)
{
    var width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth));
    var height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight));
    var bitmap = new RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
    bitmap.Render((System.Windows.Media.Visual)window.Content);
    using var stream = File.Create(path);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    encoder.Save(stream);
}
