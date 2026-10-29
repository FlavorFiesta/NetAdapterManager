# 安装器

正式安装包由 `NetAdapterManager.iss` 构建，通过 `build/Build-Packages.ps1` 调用。
需要 Inno Setup 6.5.0 或更新的 6.x 版本。

`Program.cs` 和 `NetAdapterManagerSetup.csproj` 为保留的旧安装器原型，
不用于正式发布。它需要另外嵌入 Payload.zip；直接编译该项目不会得到可用安装包。

英语、简体中文和繁体中文安装向导支持系统显示语言预选与手动改选。
静默安装可用 `/LANG=english`、`/LANG=chinesesimp` 或 `/LANG=chinesetrad`。
安装器只在没有 `settings.json` 时写入初始语言种子；不覆盖已有应用偏好。
产品快捷方式统一命名 NetAdapterManager，升级时清理旧简中快捷方式，避免切换安装语言产生重复入口。
简体中文翻译与繁体中文衍生翻译的来源和许可见根目录 `THIRD-PARTY-NOTICES.md`。
