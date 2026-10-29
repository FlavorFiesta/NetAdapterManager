# 第三方组件与许可

本项目自有代码采用根目录 MIT 许可证。第三方组件保留各自版权和许可，
不因本项目的授权方式而改变。

| 组件 | 用途与来源 | 随附材料 |
| --- | --- | --- |
| System.Management 8.0.0 | WMI，[NuGet](https://www.nuget.org/packages/System.Management/8.0.0) | `licenses/system.management-8.0.0/` |
| System.CodeDom 8.0.0 | 间接依赖，[NuGet](https://www.nuget.org/packages/System.CodeDom/8.0.0) | `licenses/system.codedom-8.0.0/` |
| .NET / Windows Desktop Runtime | 自包含 Windows x64 发布，[dotnet/runtime](https://github.com/dotnet/runtime)、[dotnet/wpf](https://github.com/dotnet/wpf) | 构建时按实际版本复制到 `licenses/packages/` |
| Inno Setup | 安装器引擎，[官网](https://jrsoftware.org/isinfo.php) | `licenses/inno-setup/LICENSE.txt` |
| Inno Setup 简体中文翻译 | 安装向导，[上游](https://github.com/kira-96/Inno-Setup-Chinese-Simplified-Translation) | 文件头保留作者信息，MIT 文本位于 `licenses/inno-chinese-translation/LICENSE` |
| Inno Setup 繁体中文翻译 | 基于上述 MIT 简体中文副本转换并调整地区术语的项目衍生版本 | 保留原作者信息与同一 MIT 许可证，修改说明位于 `Installer/Languages/ChineseTraditional.isl` 文件头 |

NuGet 许可和第三方声明来自恢复后的官方包，原文保留。打包脚本从
`project.assets.json` 与发布目录 `runtimeconfig.json` 读取实际依赖和运行时版本，
将相应许可/声明复制进 ZIP 和安装包；许可文件缺失会使打包失败。

Pillow 11.1.0 只用于构建时绘制图标，不随应用分发。
简体中文翻译文件为项目现有副本，其许可核对日期为 2026-10-20；
后续替换翻译或升级安装器时，请同步检查版权和许可材料。
