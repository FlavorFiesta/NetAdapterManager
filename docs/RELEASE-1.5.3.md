# NetAdapterManager v1.5.3 修复候选版 / Fix candidate

日期 / Date：2026-10-30。此为本地修复版本；GitHub 正式下载以实际发布的 Release 为准。

## 中文

针对客户反馈的“之前设置的 IP 读不到”和“设置生效但程序报复核失败”两类问题，修正 Windows 保存配置与运行时状态混用的读取路径。

- 禁用或未启用 IPv4 的网卡，从系统接口保存配置读取静态 IP、掩码和网关；界面注明正在显示保存值。活动网卡继续读取活动 IPv4 状态，避免用保存值掩盖真正的应用失败。
- 手动 DNS 从接口配置读取，不再把运行时解析器列表混入首选/备用 DNS；自动 DNS 继续使用运行时服务器列表。用户明确配置的 `127.0.0.1` 会原样保留，不做全局过滤。
- 所选 DNS 已保存但当前解析器列表不同，报告成功并提示检查 VPN、代理或网络策略；不因该差异执行误回滚。
- 某些驱动链路断开时 `NetEnabled=false` 不再直接当成管理员禁用，结合设备错误码和连接状态判断。
- 复核真正失败时附期望和实际 IPv4、网关与 DNS。未连接网卡通过保存配置复核时，提示“配置已保存，连接后生效”。

## 验证

2026-10-30 收尾核对：最终源码严格构建 0 警告 / 0 错误；重新执行隔离回归 53 / 53（沙箱阻止原子文件替换后，沙箱外重跑通过，未写真实网络）。现有默认自测日志为 85 / 85，外观日志为六组语言/主题、25 张截图。真实 KM-TEST 测试日志九项通过、退出码 0，包含原配置恢复和其他网卡/默认路由不变；这些是此前本机测试证据，非客户原机器复测。

便携包共 484 个条目，未包含预设、设置、地址记录、历史、安装语言种子、日志、备份或 PDB。以最终源码重新 publish 后，主程序 EXE、DLL、deps.json、runtimeconfig.json 与包内文件 SHA-256 完全一致；安装包产品版本为 1.5.3。校验值见 `artifacts/1.5.3/SHA256SUMS.txt`。源码快照及逐文件摘要保留在本地 `artifacts/audit-1.5.3/`，开发 v1.6.0 不覆盖 v1.5.3 发布物。

- 严格 Release 构建：0 警告、0 错误。
- 隔离回归：53 项通过，包含保存配置、无效掩码、自动/静态 DNS、运行时代理、明确配置的本地 DNS、链路状态与三语言提示。
- 使用已确认无默认网关的空闲 KM-TEST 回环网卡：禁用后保存配置读取、静态 IPv4、单/双 DNS、显式本地 DNS、空 DNS、自动 DNS、多 IPv4 均通过。
- 测试后恢复原网卡配置并读回复核；其他网卡与默认路由前后相同。真实写入验收需要管理员权限，默认测试不执行真实写入。
- 客户原机器的驱动、代理和策略组合尚未直接验证，建议在客户环境复测并保留新的期望/实际诊断信息。

后续六项体验改进已纳入 [v1.6.0 / v1.6.1 更新计划](PLAN-1.6.0.md)，本补丁优先交付客户问题修复。

安装包追加只读解包核对：使用发行方 SHA-256 校验通过的 innoextract，将原始安装包解到隔离目录；479 个有效载荷文件与便携包逐文件、逐字节摘要完全一致，无额外用户数据。484 个 ZIP 条目中含 5 个目录条目。未执行此正式安装包的安装/卸载流程；该项不记为已实测。证据：`artifacts/audit-1.5.3/package-audit.json`。

## English

This local candidate addresses missing saved IP settings on inactive adapters and false verification failures caused by mixing configured settings with runtime state.

- Disabled adapters or interfaces without active IPv4 read saved static addresses, masks and gateways. The interface labels these as saved settings. Active IPv4 verification continues to use active state.
- Manual DNS reads configured servers; automatic DNS uses the current resolver list. Explicitly configured localhost DNS is preserved.
- A different runtime resolver list produces a warning after successful configuration verification, rather than an incorrect rollback. VPN, proxy or policy settings may affect runtime resolvers.
- A disconnected link is no longer treated as administrative disablement solely because a driver reports `NetEnabled=false`.
- Genuine verification failures include expected and actual IPv4, gateway and DNS details. Saved settings verified on an inactive adapter are identified as taking effect on connection.

The strict Release build passed with zero warnings and errors; all 53 isolated regressions passed. Real acceptance on an explicitly selected idle KM-TEST loopback adapter covered disabled reads, static IPv4, single/dual DNS, explicit localhost DNS, empty/automatic DNS and multiple IPv4 addresses. Original settings were restored and verified; other adapters and default routes remained unchanged. The customer's exact driver/proxy/policy environment still needs confirmation.

Planned improvements are documented in the [v1.6.0 / v1.6.1 plan (Chinese)](PLAN-1.6.0.md).

## 隔离安装验收追加

使用相同载荷及安装脚本另行生成独立 AppId、禁用快捷方式修改的测试安装器，三种语言均完成安装、语言种子、版本、许可证、重装保留设置/预设、卸载保留数据检查；英文组覆盖从 v1.4.12 便携目录升级。原正式安装记录前后一致，测试 AppId 卸载干净。未启动已安装应用、未写真实网络。证据：`test-results/installer-acceptance-1.5.3.txt`。这验证了同脚本和载荷的安装行为，不将其表述为运行原始正式安装包。
