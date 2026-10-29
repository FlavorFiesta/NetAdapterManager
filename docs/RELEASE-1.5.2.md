# NetAdapterManager v1.5.2

2026-10-29 · Windows 10/11 x64

## 中文

本次发布汇总自 v1.4.12 以来的全部更新，包含本地 v1.5.0、v1.5.1 的功能和本轮修复。

### 新功能与界面

- 新增设置窗口：简体中文、繁体中文、英语和跟随系统；界面即时切换，保留用户命名的网卡、分组和预设。
- 支持浅色、深色、跟随系统主题，适配系统高对比度配色；语言与主题偏好自动保存，升级保留。
- 安装器按 Windows 显示语言预选支持的语言，不支持时回退英语；安装前可手动改选。首次启动沿用安装语言，便携版默认跟随系统，已有设置不被覆盖。
- 底部管理员标识、置顶、设置、系统网络连接和刷新按钮整体靠右，版本与刷新时间留在左侧；优化英文长文案和窄窗口布局。
- 项目介绍新增独立英文 README，与中文首页互相链接。

### 修复与可靠性

- 修复成功应用预设后编辑区仍显示旧配置的问题，避免再次点击“应用配置”意外撤销刚应用的预设；失败时继续保留编辑草稿。
- 组合状态、校验错误与警告随界面语言重新渲染；保留原始系统错误文本。
- 恢复预设备份前验证版本、必需集合和空记录，拒绝不兼容备份，保护当前库。
- 网络配置或回滚失败后读取实际状态；读取失败清除旧快照并提示状态未知，保留原操作错误。
- 损坏预设库不再阻断首次网卡加载，故障文件仍受写保护。
- 网卡启停增加 15 秒等待上限；超时后保持未完成操作的写入互斥并阻止后续写入。界面超时不代表 Windows 操作已停止。
- 网络命令输出编码与应用界面语言分离，避免切换语言影响命令输出解码。
- 打包脚本显式按 UTF-8 读取 JSON 元数据，修复 Windows PowerShell 遇到中文诊断信息时的解析失败。

### 下载与升级

- 安装版：`NetAdapterManager-v1.5.2-Setup-win-x64.exe`
- 便携版：`NetAdapterManager-v1.5.2-portable-win-x64.zip`
- 校验文件：`SHA256SUMS.txt`

两个包均包含 .NET 运行时。安装版可覆盖升级；便携版退出程序后完整解压覆盖，保留个人数据文件。建议升级前备份程序目录。
预设库继续使用 v1 格式，无需迁移。旧系统分组标识保持兼容，仅翻译显示名称。恢复预设备份不改变语言或主题设置。

### 验证边界

严格 Release 构建 0 警告、0 错误；45 项隔离回归、77 项默认只读自测和 6 组语言/主题界面检查通过。

本轮使用合成网卡验证预设应用与语言切换回归，不写入真实网卡。先前 1.5.0 已验证三语言安装、覆盖升级、偏好保留与卸载数据保留。不同语言的 Windows、高 DPI、真实高对比度和驱动挂起行为尚未完成全部实机组合覆盖；原生系统对话框和错误原文由 Windows 决定。

## English

This release includes all changes since v1.4.12, incorporating the local v1.5.0/v1.5.1 candidates and the latest fixes.

### Features and interface

- New Settings window with Simplified Chinese, Traditional Chinese, English and system language. Changes apply immediately; user-created adapter, group and preset names remain unchanged.
- Light, dark and system themes, with Windows high-contrast colors. Language and theme preferences are saved and preserved during upgrades.
- The installer preselects a supported language from the Windows display language, falling back to English. You can choose another language before installing. First launch follows the installer language; portable installations follow the system by default. Existing settings take precedence.
- Administrator status and toolbar controls are right-aligned, with version and refresh time on the left. Improved narrow-window and long English text layouts.
- Separate English and Chinese project introductions with links between them.

### Fixes and reliability

- Successful preset application now updates the editor to the actual configuration, preventing a subsequent Apply Configuration click from restoring stale values. Failed operations continue to preserve the editing draft.
- Composed status messages, validation errors and warnings update when changing languages. Raw system error text is preserved.
- Backup restoration validates the format version, required collections and null records before replacing the current preset library.
- Failed configuration or rollback refreshes actual adapter state. Failed reads clear stale snapshots and report unknown state while retaining the original operation error.
- A damaged preset library no longer prevents initial adapter loading; the damaged file remains protected from overwriting.
- Adapter enable/disable operations have a 15-second wait limit. Unfinished operations retain write exclusion, and further writes are blocked. A UI timeout does not mean the Windows operation has stopped.
- Network command output decoding is independent of the UI language.
- Packaging reads JSON metadata explicitly as UTF-8, avoiding Windows PowerShell parsing failures when metadata contains Chinese diagnostics.

### Download and upgrade

Download the Setup EXE or portable ZIP listed above, with `SHA256SUMS.txt` for verification. Both packages include the .NET runtime. Install over the existing version, or close the portable application and extract all updated files while keeping personal data. Back up the application directory before upgrading.

The preset library remains in v1 format. Existing system group identifiers remain compatible; only their displayed labels are translated. Restoring a preset backup does not change language or theme preferences.

### Validation limits

The strict Release build passed with zero warnings and errors. All 45 isolated regressions, 77 default read-only self-tests and six language/theme UI combinations passed.

The latest regression checks use synthetic adapters without real network writes. Earlier 1.5.0 acceptance covered installation in all three languages, upgrade behavior, preference retention and preservation of user data after uninstall. The complete real-machine matrix for Windows display languages, high DPI, high contrast and hung drivers remains untested. Windows controls native dialogs and raw system error messages.
