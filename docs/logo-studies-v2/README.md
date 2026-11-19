# NetAdapterManager 新 Logo 提案

根据 App.xaml / ThemeService.cs 的蓝灰色、深浅主题和卡片式布局设计。所有方案使用一致配色，方便比较图形本身。

- **精简网口**：硬件轮廓，直接表达网卡管理。最推荐 · 直观、稳重。文件：`01-port.svg` / `.png` / `.ico`。
- **N 字路由**：两条路径组成 N，兼顾连接与品牌识别。品牌方向 · 简洁、有记忆点。文件：`02-route-n.svg` / `.png` / `.ico`。
- **配置切换**：两张配置卡片，突出预设与快速切换。功能方向 · 呼应软件卡片布局。文件：`03-presets.svg` / `.png` / `.ico`。
- **连接桥**：两端接口由中心链路接通。极简方向 · 适合标题栏与托盘。文件：`04-bridge.svg` / `.png` / `.ico`。

PNG 为 1024 × 1024 透明画布；SVG 可编辑；ICO 包含 16/20/24/32/40/48/64/128/256 px。
comparison.png 展示 4 款方案与深浅背景下的实际小尺寸。未替换当前应用图标。
生成命令：`python build/generate_logo_studies_v2.py`。
