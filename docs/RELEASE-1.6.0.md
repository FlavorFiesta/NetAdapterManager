# NetAdapterManager v1.6.0 本地候选版

日期：2026-10-30。未提交或发布 GitHub；v1.5.3 客户补丁包独立保留。

## 功能

- 手动应用与应用预设都先显示网卡名称、GUID、索引、MAC，以及当前 IP 模式、地址、网关和 DNS 与完整目标的比较。附加 IP 按现有保留规则进入预览，变化行加粗。取消不写入；确认后在写锁内重新读取身份及配置，发生变化则拒绝写入，要求重新预览。
- 编辑区显示实际配置和草稿状态。草稿按网卡在内存中保留，切换、筛选或刷新不静默丢弃；还原当前配置只清除此网卡草稿。成功应用同步实际配置；失败保留输入并刷新实际状态。草稿不跨进程保存。
- 预设支持单条、勾选项、全部导出，仍使用 v1 `Version/Presets/Groups` 格式。导入验证格式、枚举、IPv4、前缀、DNS 和大小限制，预览导入项与重复项；可选择跳过或替换，文件内重复项也按策略处理。一次原子保存成功后才更新内存，失败保留旧库。
- 其他电脑的 GUID 不会隐式回退到当前网卡。通过“绑定网卡”明确选择本机网卡并保存；绑定本身不写网络，有相同配置冲突时拒绝覆盖其他预设。
- 新增界面支持简中、繁中、英语与现有主题。预览支持实时翻译，深色/浅色和高对比度资源随主题更新。
- 加入 PerMonitorV2 清单，窗口按所在显示器工作区约束尺寸，在 DPI 或显示器拓扑改变后重新定位；小窗口提供滚动入口。新增 F5 刷新、Ctrl+Enter 预览应用、Ctrl+R 还原，以及输入框无障碍名称和按钮焦点边框。

## 验收边界

真实客户原机器未复测。v1.5.3 的空闲 KM-TEST 实测和恢复证据见其发布说明；本轮 v1.6.0 测试不写真实网卡，不修改 WLAN 或默认路由。

自动化布局缩放和高对比度调色板注入不等同于真实 Windows 缩放切换、系统高对比度开关或多显示器迁移。英文界面检查不等同于英文 Windows/不同代码页实测；这些环境仍需后续实机验收。

v1.6.1 继续保留操作记录、诊断导出与手动检查更新。

## 最终源码与包验收

- 严格 Release 构建 0 警告 / 0 错误；隔离回归 70 / 70，默认自测 102 / 102。新增用例覆盖保留附加地址的预览、身份/配置变化拒绝写入、确认/取消、非法草稿、按网卡保留与还原、v1 文件往返、非法导入、重复策略及跨电脑绑定。原有超时用例增加已审阅应用入口的写锁保护检查。
- 六组语言/主题，48 张外观截图；新增弹窗背景、实时翻译、正常窗口无横向溢出、预览列宽、真实 WPF 绑定刷新后草稿保留断言通过。模拟 125% / 150% / 200% 布局缩放、最小窗口和高对比度调色板注入通过。
- 安装包产品版本 1.6.0，程序文件版本 1.6.0.0。原始安装包只读解包后，479 个载荷文件与便携包逐文件 SHA-256 一致；四个主程序文件与最终源码生成的 publish 输出一致。未发现运行数据、日志、备份、调试符号、私钥文件、个人构建路径或令牌标记。
- 两种包已生成并复核 `artifacts/1.6.0/SHA256SUMS.txt`。v1.5.3 包与其校验值保留不变。
- 最终打包使用已还原的缓存依赖和 `build/Build-Packages.ps1 -NoRestore`，没有构建警告。在线 NuGet 漏洞数据此前无法读取，因此缓存依赖还原显式使用 `NuGetAudit=false`；本轮不声称完成在线依赖漏洞审计。
- 本机只读环境信息：简中 Windows，单显示器。真实多屏迁移、英文 Windows、实际系统缩放/高对比度切换仍未覆盖。

证据保存在本地 `test-results/build-1.6.0.txt`、`isolated-1.6.0.txt`、`selftest-1.6.0.txt`、`appearance-1.6.0.txt`、`package-build-1.6.0.txt`、`artifacts/audit-1.6.0/package-audit.json`；截图只使用虚构演示配置。

## 隔离安装、升级与卸载

最终 v1.6.0 便携载荷已使用同一安装脚本构建独立 AppId、禁止快捷方式修改的测试安装器。英语、简中、繁中均通过安装、安装语言种子、文件版本、许可证、重装保留设置/预设、卸载保留数据检查；英语组覆盖从已发布 v1.5.2 便携目录升级。原正式安装记录逐项核对一致，测试 AppId 卸载干净。未启动已安装主程序、未写真实网卡。

证据：`test-results/installer-acceptance-1.6.0.txt`。原始正式安装包仅进行只读解包、版本与字节核验，没有运行其安装程序；上述安装行为验收使用独立测试 AppId，避免影响现有安装。

## Review

人工审查了预览到写入的调用链、超时写锁、成功/失败刷新与草稿缓存、导入验证与原子提交、重复项和网卡绑定、窗口资源与滚动约束。已修正：预览应用在不确定写入期间等待写锁、弹窗未完整跟随主题、预览列标题语言未刷新、外层滚动导致无界测量/列宽不足、预设操作期间入口互斥不完整。未启用子代理。

## English

Local candidate only; not published to GitHub. Configuration review now precedes manual/preset application, followed by a fresh identity/state check under the write gate. Drafts are retained per adapter for the current session, with an explicit restore action. v1 preset JSON supports export/import, conflict preview with skip/replace policies, atomic storage and explicit local adapter binding. Importing or binding never applies network configuration.

New UI supports Simplified Chinese, Traditional Chinese and English, existing themes, keyboard shortcuts and accessible input names. Per-monitor DPI awareness and work-area handling are implemented. Simulated layout scaling and injected high-contrast colors do not constitute physical multi-monitor, actual Windows scaling or English Windows acceptance. The customer's original environment has not been retested.
