# 1.4.11 发布说明

日期：2026-10-20

修复首次开源版本复审发现的 DHCP/DNS 配置与失败回滚问题。建议 1.4.10 用户升级，升级时保留程序目录中的用户数据。

## 修复

- DHCP 切换静态 IPv4 后发生中途失败时，回滚依据最新快照恢复原始 IPv4/DNS 配置，避免用旧快照跳过恢复，也避免重复开启已生效的 DHCP。
- 回滚后重新读取实际状态，仅在复核一致时显示恢复成功；恢复命令失败、目标丢失或状态不一致时报告回滚失败。
- 恢复使用独立取消令牌，调用方取消操作不会同时取消必要的恢复。
- DNS 自动/手动模式独立读取 Windows DNS 设置，修复 DHCP + 手动 DNS 被误判为失败，以及切回自动 DNS 时漏执行的问题。
- 静态 IPv4 复核明确检查 IP 模式；回滚可复核原有静态空地址状态。
- 新增 15 项无需真实网卡的网络回归并接入 GitHub Actions；UI 自测按实际程序集版本验证底部版本号。

DNS 模式优先使用 [Windows DNS 设置 API](https://learn.microsoft.com/windows/win32/api/netioapi/nf-netioapi-getinterfacednssettings)，较早版本 Windows 10 使用只读注册表兼容路径。有效 DNS 地址列表仍通过 Windows DNS 客户端读取。

## 验证

- 严格 Release 构建：0 警告、0 错误。
- 完整默认 SelfTest：47 通过、0 失败；独立网络回归 15 通过、0 失败。
- 隔离回归覆盖 DHCP + 手动 DNS、切回自动 DNS、静态 IP + 自动 DNS、写入中途失败、复核失败、管理地址保存失败、回滚未生效/报错/目标丢失、取消操作、静态多地址和空地址恢复。
- 全功能只读与 UI Smoke：13 通过、0 失败（SkipBuild）；Wi-Fi 网卡、IPv4 和 DNS 的前后快照一致。
- ZIP 484 项，包含 20 项许可/声明文件；没有预设、管理地址 JSON、运行日志或 PDB。
- 在唯一隔离目录静默安装成功，安装版本 1.4.11.0，许可文件完整；卸载成功，剩余文件 0。未启动主程序。
- 未执行真实网卡写入测试；网络写入和失败注入回归使用虚构快照与命令捕获器。

## 产物

| 文件 | 字节数 | SHA-256 |
| --- | ---: | --- |
| NetAdapterManager-v1.4.11-portable-win-x64.zip | 70143394 | `41B44F8A471278DDDE321570B0F667415A4E81306F55ED78223E8617FC0C13AF` |
| NetAdapterManager-v1.4.11-Setup-win-x64.exe | 50185160 | `ED33EE0BCDD606F52A03B9F5FFF0B25DBB3EF7799E8EC141AC8311DCE20130E5` |

完整产物：[GitHub Release v1.4.11](https://github.com/FlavorFiesta/NetAdapterManager/releases/tag/v1.4.11)。
