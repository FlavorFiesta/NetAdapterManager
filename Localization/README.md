# 界面资源

`Strings.json` 作为程序集内嵌资源分发；每个资源包含 `zh-CN`、`zh-TW` 和 `en` 三种翻译。缺失某一语言的值时回退英语。

XAML 使用 `DynamicResource`；C# 使用 `L.T(key)` 和 `L.F(key, arguments)`。`Loc_` 开头的键来自首次提取时的原文摘要，后续改文案应保留键。新增资源可使用 `Settings.*` 一类语义键。

格式化资源使用 .NET 复合格式占位符，三种语言必须保留相同的参数与格式。不要把用户名称、IP、DNS、GUID 或系统原始错误内容当作资源键。

需要在切换语言时更新的组合消息使用 `L.Join(separator, parts)`，避免对翻译结果直接拼接或使用 `string.Join`。它保留各段资源键和参数，支持嵌套消息，并保持系统原始错误文本不变。

UI 语言不修改线程的 `CurrentCulture`/`CurrentUICulture`。数据序列化、网卡定位和命令解码保持与翻译独立。

`GroupIds` 保留 v1 库的固定标识，`GroupIds.Display` 仅生成显示名称。绑定分组列表时使用 `GroupItemTemplate`；保存时使用原始选中值，不能保存翻译后的标签。

修改资源后运行 `--isolated` 检查资源完整性和占位符，再运行 `--appearance-smoke` 检查即时切换和布局。
