# NetAdapterManager v1.6.1 本地候选版

2026-10-30。未提交或发布 GitHub；v1.5.3、v1.6.0 发布物保持不变。

## 功能

- 新增“记录与诊断”：记录确认后的配置应用、启用/禁用操作，保留目标网卡、操作前配置、完整目标、刷新后的实际配置、执行结果、复核和回滚结果。取消预览、未通过输入校验的草稿不产生网络操作记录。启停的执行返回与实际状态复核分开保存，未确认成功不会标作复核成功。
- 独立 `operation-history.json` 最多保留 200 项、2 MiB，另保留一个原子替换备份；错误、消息、警告各截取最多 8192 字符。损坏或过大的原文件不覆盖；记录失败单独提示，不把已经完成的网络操作改为失败或触发回滚。
- 支持查看原始详情、手动复制原始错误，以及诊断 JSON 导出。导出默认隐藏名称/GUID、MAC、IP/DNS；隐藏选项独立。启用任一隐藏项时额外省略自由文本，避免原生错误、警告或驱动字段夹带身份信息；名称内的 IP/MAC 也按选择隐藏。导出不附带原始应用日志、其他文件或全机信息。
- 导出前显示准确 JSON 预览，保存的是同一份预览内容。复制原始错误是独立操作，界面注明其未经隐藏。原始记录只保存在本地，导出文件由用户选择位置。
- 应用文本日志在写入前检查轮转边界，限制单条文本长度和活动文件大小，保留一个轮转副本。
- 设置中新增手动检查更新：显示当前版本、最新正式版和摘要，仅点击检查时访问公共 GitHub API；不使用令牌，不自动下载、执行或发布软件。只接受正式三段版本、同一仓库且与版本标签对应的 HTTPS 发布链接；拒绝草稿、预发布和异常响应。摘要作为纯文本显示。
- 处理离线、10 秒超时、取消、限流、无正式版、服务错误，以及本地版本领先 GitHub 的情况。关闭设置会取消在途检查。只有成功且链接合法时才允许手动打开 GitHub 发布页。
- 新界面支持简中、繁中、英语及现有主题；保留 v1 预设库格式、v1.6.0 预览及草稿行为。

## 验证与边界

- 严格 Release 构建 0 警告、0 错误；96 项隔离回归、128 项默认自测通过。
- 六组语言/主题、54 张截图通过；包括诊断默认隐藏、更新结果即时翻译和模拟正式版响应。截图使用虚构配置；自动测试不改用户剪贴板、不打开浏览器、不写真实网卡。
- 新回归覆盖隐藏选项、跨字段标识、记录数量/字节上限、损坏保留、日志截断、记录失败与网络结果隔离、前后目标快照；HTTP 测试覆盖新/旧/同版、403/429/404/500、草稿/预发布、异常版本或链接、过大响应、离线/超时/取消。
- 本机真实更新请求得到 `Offline`，离线结果已正确处理。本轮未验证本机真实联网成功、打开外部浏览器或跨公网代理/证书组合；没有绕过 TLS 校验或修改系统代理。
- 继续保留 v1.6.0 的实机验收边界：客户原机器、真实多屏迁移、实际系统缩放/高对比度切换及英文 Windows 尚未覆盖。本轮未写 WLAN、默认路由或其他真实网卡。
- 构建沿用已还原缓存依赖，在线 NuGet 漏洞审计未完成。

本地证据：`test-results/build-1.6.1.txt`、`isolated-1.6.1.txt`、`selftest-1.6.1.txt`、`appearance-1.6.1.txt`、`live-update-1.6.1.txt`。

更新接口依据：[GitHub Releases REST API](https://docs.github.com/en/rest/releases/releases#get-the-latest-release)。

## 包与安装验收

- 安装包和便携包已生成；程序文件版本 1.6.1.0，安装包产品版本 1.6.1。
- 原始安装包只读解包后，479 个载荷文件与便携包逐文件 SHA-256 一致；四个主程序文件与最终 publish 输出一致。
- 未发现预设、设置、地址映射、操作记录、日志、备份、调试符号、私钥文件、个人构建路径或令牌标记。`artifacts/1.6.1/SHA256SUMS.txt` 已生成并核验；v1.5.3 和 v1.6.0 原包及校验值也再次核验，保持不变。
- 使用相同脚本和最终载荷、独立测试 AppId、禁用快捷方式修改，完成英语/简中/繁中安装、语言种子、版本、许可证、重装保留设置和预设、卸载保留数据；英语组覆盖从 v1.6.0 便携目录升级。原正式安装注册值不变，测试 AppId 已卸载。
- 原始正式安装包仅只读解包，没有执行其安装程序；安装行为验收使用独立测试安装器。未启动已安装主程序。

证据：`test-results/package-build-1.6.1.txt`、`installer-acceptance-1.6.1.txt`、`artifacts/audit-1.6.1/package-audit.json`。最终源码快照与逐文件摘要保存在本地 `artifacts/audit-1.6.1/`。

## Review

已人工审查：日志写入与网络结果隔离、原子保存与数量/字节上限、诊断隐藏与准确预览保存、原始错误复制边界、更新请求/读取超时与取消、HTTP 错误、稳定版本比较、固定仓库标签链接验证、纯文本摘要、三语言和主题切换。没有启用子代理，没有发布 GitHub。

## English

Local candidate only. Adds bounded operation history, raw error copying and a diagnostic JSON preview with names/GUIDs, MACs and IP/DNS hidden by default. Free text is excluded whenever any privacy option is enabled; original application logs are never attached. History failures do not change the network result.

Settings now offers an explicit stable-release check, version comparison and plain-text notes. Validated links open only the project's GitHub release page on user action; software is not downloaded or executed. Offline, timeout, cancellation, rate limits, prereleases and malformed responses are handled. The real request on this host returned Offline; successful live connectivity and browser launching were not claimed as tested.
