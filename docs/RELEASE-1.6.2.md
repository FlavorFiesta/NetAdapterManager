# NetAdapterManager v1.6.2

2026-11-12 · Windows 10/11 x64

本次正式发布包含自 v1.5.2 以来的累计更新。

## 中文

- 新增配置变更预览、按网卡保留编辑草稿，以及预设导入/导出和跨电脑手动绑定。
- 新增操作记录、原始错误复制和默认隐藏敏感字段的诊断导出。
- 修复禁用网卡的已保存 IPv4 读取、DNS 复核和失效管理地址记录导致旧主 IP 被保留的问题。
- 静态 IPv4 禁用自动 DNS，静态 DNS 可留空；区分网卡管理状态与连接状态，修复英文状态裁切。
- 更新检查区分证书、DNS、代理、超时和限流错误，主源失败时自动尝试免费的 Cloudflare 备用源；支持自定义或禁用备用地址。
- 备用源自动查询 GitHub 正式版，缓存 5 分钟；API 限流时使用官方最新发布页确认版本，无需手动同步。
- 三语言、浅色/深色主题、窗口适配和图标更新。

下载 Setup EXE 安装包或 portable ZIP 便携包，两者均自带 .NET 运行时。升级前关闭软件并备份程序目录；便携版完整解压，保留个人配置和预设文件。SHA256SUMS.txt 提供校验值。

备用源只提供版本信息，下载仍来自 GitHub，没有自动安装。部分网络访问 workers.dev 仍需系统代理。本次本机证书修复不会随安装包修改其他电脑的证书库，TLS 校验保持开启。

验证：Release 构建零警告、零错误；113 项隔离回归、5 项 Worker 测试及 6 组语言/主题界面检查通过。GitHub 主源及模拟主源失败后的真实备用源查询均通过。未在本轮重复真实网卡写入、安装/卸载及真实多显示器 DPI 验收。

## English

This release includes the accumulated changes since v1.5.2:

- Configuration previews, per-adapter drafts, preset import/export and manual adapter binding.
- Operation history, raw error copying and diagnostic export with sensitive fields hidden by default.
- Fixes for saved IPv4 configuration on disabled adapters, DNS verification and stale managed-address records.
- Static IPv4 requires static DNS (which may be empty); administrative and connection states are displayed separately.
- Update checks classify network failures and automatically try a configurable Cloudflare fallback. The feed refreshes from official GitHub releases with a five-minute cache and handles API rate limits via GitHub's latest-release page.
- Three-language UI, theme/layout improvements and an updated icon.

Choose the Setup EXE or portable ZIP; both include the .NET runtime. Close the app and back up its directory before upgrading. Keep personal settings and presets when replacing portable files. Verify downloads with SHA256SUMS.txt.

The fallback mirrors version metadata only; downloads remain on GitHub. Some networks require a system proxy. There is no automatic installation or certificate validation bypass. The application does not modify the certificate store.

Validation: clean Release build; 113 isolated regressions, 5 Worker tests and six language/theme combinations passed, plus live primary and fallback checks. Real network writes, install/uninstall and physical multi-monitor DPI scenarios were not repeated in this round.
