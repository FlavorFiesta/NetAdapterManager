"""Generate editable minimal icon proposals and a comparison sheet (Pillow)."""
from html import escape
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs" / "icon-options"
SCALE = 4
OPTIONS = [
    ("01-port", "极简网口", "直观 · 稳重", "#2563EB"),
    ("02-switch", "双向切换", "轻快 · 高辨识", "#0D9488"),
    ("03-nodes", "网络节点", "连接 · 秩序", "#4F46E5"),
    ("04-monogram", "字母 N", "克制 · 品牌感", "#2563EB"),
    ("05-controls", "配置滑杆", "工具感 · 中性", "#263448"),
    ("06-link", "连接环", "圆润 · 现代", "#0284C7"),
]


def make_icon(index, color, foreground="#FFFFFF"):
    im = Image.new("RGBA", (256 * SCALE, 256 * SCALE))
    d = ImageDraw.Draw(im)
    svg = ['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">']

    def rect(x, y, w, h, r, fill):
        d.rounded_rectangle(tuple(round(v * SCALE) for v in (x, y, x+w, y+h)), radius=r*SCALE, fill=fill)
        svg.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}"/>')

    def line(points, width=14, color=foreground):
        p = [(round(x*SCALE), round(y*SCALE)) for x, y in points]
        d.line(p, fill=color, width=width*SCALE, joint="curve")
        for x, y in p:
            r = width*SCALE/2
            d.ellipse((x-r, y-r, x+r, y+r), fill=color)
        svg.append(f'<polyline points="{" ".join(f"{x},{y}" for x,y in points)}" fill="none" stroke="{color}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round"/>')

    def circle(x, y, r, fill=foreground):
        d.ellipse(tuple(round(v*SCALE) for v in (x-r,y-r,x+r,y+r)), fill=fill)
        svg.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="{fill}"/>')

    rect(8, 8, 240, 240, 52, color)
    if index == 0:
        # A single socket silhouette with three readable contacts.
        line([(76,79),(180,79),(180,151),(151,151),(151,177),(105,177),(105,151),(76,151),(76,79)], 12)
        for x in (104,128,152):
            line([(x,99),(x,118)], 10)
    elif index == 1:
        line([(70,100),(184,100)], 15)
        line([(159,75),(184,100),(159,125)], 15)
        line([(186,156),(72,156)], 15)
        line([(97,131),(72,156),(97,181)], 15)
    elif index == 2:
        line([(128,85),(128,128)], 12)
        line([(78,171),(78,132),(178,132),(178,171)], 12)
        for x,y in ((128,78),(78,177),(178,177)):
            rect(x-20,y-20,40,40,11,foreground)
    elif index == 3:
        line([(80,176),(80,80),(176,176),(176,80)], 20)
    elif index == 4:
        for y in (88,128,168):
            line([(72,y),(184,y)], 10)
        for x,y in ((105,88),(155,128),(105,168)):
            circle(x,y,15,color)
            circle(x,y,10)
    elif index == 5:
        # Two open links meet at a shared diagonal stroke.
        line([(112,142),(97,157),(78,157),(64,143),(64,124),(100,88),(119,88),(133,102)], 14)
        line([(144,114),(159,99),(178,99),(192,113),(192,132),(156,168),(137,168),(123,154)], 14)
        line([(105,151),(151,105)], 14)
    svg.append('</svg>')
    return im.resize((256,256), Image.Resampling.LANCZOS), "\n".join(svg)


def font(size, bold=False):
    return ImageFont.truetype("C:/Windows/Fonts/msyh" + ("bd" if bold else "") + ".ttc", size)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    board = Image.new("RGB", (1500,1080), "#F3F5F9")
    d = ImageDraw.Draw(board)
    d.text((64,40), "NetAdapterManager / 简约图标提案", font=font(34,True), fill="#18253A")
    d.text((64,96), "6 个方向 · 纯色与简洁线条 · 下方为 16 / 24 / 32 px 实际尺寸", font=font(19), fill="#657086")
    for i,(slug,title,subtitle,color) in enumerate(OPTIONS):
        icon,svg = make_icon(i,color)
        (OUT / (slug+".svg")).write_text(svg, encoding="utf-8")
        icon.save(OUT / (slug+".png"))
        icon.save(OUT / (slug+".ico"), sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
        x,y = 60+(i%3)*466, 160+(i//3)*440
        d.rounded_rectangle((x,y,x+444,y+416), radius=24, fill="#FFFFFF")
        d.text((x+26,y+22), f"0{i+1}  {title}", font=font(24,True), fill="#18253A")
        large = icon.resize((184,184), Image.Resampling.LANCZOS)
        board.paste(large,(x+130,y+76),large)
        d.text((x+26,y+280),subtitle,font=font(18),fill="#657086")
        d.rounded_rectangle((x+24,y+326,x+216,y+390),radius=12,fill="#F1F4F8")
        d.rounded_rectangle((x+228,y+326,x+420,y+390),radius=12,fill="#172033")
        for ox in (24,228):
            for dx,size in ((30,16),(80,24),(136,32)):
                small=icon.resize((size,size),Image.Resampling.LANCZOS)
                board.paste(small,(x+ox+dx,y+358-size//2),small)
    board.save(OUT / "comparison.png")
    print(OUT / "comparison.png")


if __name__ == "__main__":
    main()
