# 安装器

正式安装包由 `NetAdapterManager.iss` 构建，通过 `build/Build-Packages.ps1` 调用。
需要 Inno Setup 6.5.0 或更新的 6.x 版本。

`Program.cs` 和 `NetAdapterManagerSetup.csproj` 为保留的旧安装器原型，
不用于正式发布。它需要另外嵌入 Payload.zip；直接编译该项目不会得到可用安装包。

简体中文翻译的来源和许可见根目录 `THIRD-PARTY-NOTICES.md`。
