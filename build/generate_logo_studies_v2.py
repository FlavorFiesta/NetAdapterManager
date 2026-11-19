"""Four new vector-native logo studies, with Windows icon exports and previews."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'docs' / 'logo-studies-v2'
S = 4
INK, BLUE, WHITE = '#263448', '#81AED8', '#F2F5FA'
OPTIONS = [
    ('01-port', '精简网口', '硬件轮廓，直接表达网卡管理', '最推荐 · 直观、稳重'),
    ('02-route-n', 'N 字路由', '两条路径组成 N，兼顾连接与品牌识别', '品牌方向 · 简洁、有记忆点'),
    ('03-presets', '配置切换', '两张配置卡片，突出预设与快速切换', '功能方向 · 呼应软件卡片布局'),
    ('04-bridge', '连接桥', '两端接口由中心链路接通', '极简方向 · 适合标题栏与托盘'),
]


def font(size, bold=False):
    return ImageFont.truetype('C:/Windows/Fonts/msyh' + ('bd' if bold else '') + '.ttc', size)


def make_icon(index):
    im = Image.new('RGBA', (256*S, 256*S))
    d = ImageDraw.Draw(im)
    svg = ['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">',
           f'<title>{OPTIONS[index][1]} / NetAdapterManager</title>']

    def rect(box, radius, fill):
        x,y,w,h = box
        d.rounded_rectangle(tuple(round(v*S) for v in (x,y,x+w,y+h)), radius=radius*S, fill=fill)
        svg.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{radius}" fill="{fill}"/>')

    def poly(points, fill):
        d.polygon([(x*S,y*S) for x,y in points], fill=fill)
        svg.append(f'<polygon points="{" ".join(f"{x},{y}" for x,y in points)}" fill="{fill}"/>')

    def line(points, fill, width):
        pts = [(x*S,y*S) for x,y in points]
        d.line(pts, fill=fill, width=width*S, joint='curve')
        for x,y in pts:
            r=width*S/2
            d.ellipse((x-r,y-r,x+r,y+r), fill=fill)
        svg.append(f'<polyline points="{" ".join(f"{x},{y}" for x,y in points)}" fill="none" stroke="{fill}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round"/>')

    rect((8,8,240,240), 48, INK)
    if index == 0:
        # Filled socket silhouette, generous interior space, only three contacts.
        poly([(66,70),(190,70),(190,158),(160,158),(160,186),(96,186),(96,158),(66,158)], WHITE)
        poly([(84,88),(172,88),(172,140),(142,140),(142,168),(114,168),(114,140),(84,140)], INK)
        for x in (101,123,145):
            rect((x,88,10,30), 3, BLUE)
    elif index == 1:
        # Distinct continuous N silhouette; blue endpoints denote two adapters.
        line([(75,181),(75,75),(181,181),(181,75)], WHITE, 21)
        rect((61,59,28,32), 7, BLUE)
        rect((167,165,28,32), 7, BLUE)
    elif index == 2:
        rect((57,61,111,101), 13, BLUE)
        rect((77,81,111,111), 13, INK)
        rect((88,92,100,100), 11, WHITE)
        line([(108,119),(167,119)], INK, 9)
        line([(108,143),(148,143)], INK, 9)
        rect((108,163,20,10), 4, '#4F739E')
        rect((137,163,30,10), 4, '#4F739E')
    else:
        # Open brackets and a single link remain legible at small sizes.
        line([(94,78),(66,78),(66,178),(94,178)], WHITE, 18)
        line([(162,78),(190,78),(190,178),(162,178)], WHITE, 18)
        line([(96,128),(160,128)], BLUE, 20)
        rect((85,112,24,32), 6, BLUE)
        rect((147,112,24,32), 6, BLUE)
    svg.append('</svg>')
    return im.resize((1024,1024), Image.Resampling.LANCZOS), '\n'.join(svg)


def paste_icon(board, icon, pos, size):
    small=icon.resize((size,size), Image.Resampling.LANCZOS)
    board.paste(small, pos, small)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    board=Image.new('RGB', (1600,1150), '#EDF1F6')
    d=ImageDraw.Draw(board)
    d.text((56,34), 'NetAdapterManager', font=font(36,True), fill='#263448')
    d.text((58,88), '新 Logo 设计 / 蓝灰配色 · 清晰轮廓 · 深浅主题适配', font=font(21), fill='#506078')
    for i,(slug,title,desc,note) in enumerate(OPTIONS):
        icon,svg=make_icon(i)
        (OUT/f'{slug}.svg').write_text(svg, encoding='utf-8')
        icon.save(OUT/f'{slug}.png')
        icon.resize((256,256),Image.Resampling.LANCZOS).save(OUT/f'{slug}.ico', sizes=[(16,16),(20,20),(24,24),(32,32),(40,40),(48,48),(64,64),(128,128),(256,256)])
        x,y=48+(i%2)*772,145+(i//2)*488
        d.rounded_rectangle((x,y,x+732,y+462), radius=18, fill='white')
        d.text((x+28,y+23),f'0{i+1}  {title}', font=font(26,True),fill='#263448')
        d.text((x+28,y+67),note,font=font(17),fill='#506078')
        paste_icon(board,icon,(x+34,y+119),200)
        d.text((x+266,y+132),'NetAdapterManager',font=font(24,True),fill='#263448')
        d.text((x+267,y+178),desc,font=font(17),fill='#506078')
        d.text((x+267,y+224),'#263448   /   #81AED8   /   #F2F5FA',font=font(15),fill='#506078')
        for j,c in enumerate((INK,BLUE,WHITE)):
            d.rounded_rectangle((x+268+j*44,y+267,x+300+j*44,y+283),radius=4,fill=c)
        for j,(bg,fg,label) in enumerate((('#F3F5F9','#506078','浅色'),('#0F1117','#94A3BC','深色'))):
            bx,by=x+26+j*346,y+344
            d.rounded_rectangle((bx,by,bx+334,by+92),radius=10,fill=bg)
            d.text((bx+14,by+13),label,font=font(13),fill=fg)
            for dx,size in ((70,16),(114,24),(166,32),(226,48)):
                paste_icon(board,icon,(bx+dx,by+46-size//2),size)
                d.text((bx+dx,by+72),str(size),font=font(10),fill=fg)
    board.save(OUT/'comparison.png')
    (OUT/'README.md').write_text('# NetAdapterManager 新 Logo 提案\n\n'
        '根据 App.xaml / ThemeService.cs 的蓝灰色、深浅主题和卡片式布局设计。所有方案使用一致配色，方便比较图形本身。\n\n'
        + '\n'.join(f'- **{title}**：{desc}。{note}。文件：`{slug}.svg` / `.png` / `.ico`。' for slug,title,desc,note in OPTIONS)
        + '\n\nPNG 为 1024 × 1024 透明画布；SVG 可编辑；ICO 包含 16/20/24/32/40/48/64/128/256 px。\n'
        'comparison.png 展示 4 款方案与深浅背景下的实际小尺寸。未替换当前应用图标。\n'
        '生成命令：`python build/generate_logo_studies_v2.py`。\n',encoding='utf-8')
    print(OUT/'comparison.png')


if __name__ == '__main__':
    main()
