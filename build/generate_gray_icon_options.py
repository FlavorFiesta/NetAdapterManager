"""Gray/white color studies for the network nodes proposal."""
from PIL import Image, ImageDraw
from generate_icon_options import OUT, make_icon, font


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    board = Image.new("RGB", (1380, 610), "#F3F4F6")
    draw = ImageDraw.Draw(board)
    draw.text((48, 32), "03 网络节点 / 灰白配色", font=font(30, True), fill="#27272A")
    variants = [
        ("A", "深灰底 · 白色节点", "#3F3F46", "#FFFFFF"),
        ("B", "中灰底 · 白色节点", "#71717A", "#FFFFFF"),
        ("C", "灰白底 · 深灰节点", "#E4E4E7", "#52525B"),
    ]
    for i, (label, title, bg, fg) in enumerate(variants):
        icon, svg = make_icon(2, bg, fg)
        stem = OUT / f"03-nodes-gray-{label.lower()}"
        stem.with_suffix(".svg").write_text(svg, encoding="utf-8")
        icon.save(stem.with_suffix(".png"))
        icon.save(stem.with_suffix(".ico"), sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
        x, y = 42+i*446, 104
        draw.rounded_rectangle((x,y,x+420,y+466), radius=22, fill="#FFFFFF")
        draw.text((x+24,y+22), f"{label}  {title}", font=font(22,True), fill="#3F3F46")
        large = icon.resize((224,224), Image.Resampling.LANCZOS)
        board.paste(large,(x+98,y+82),large)
        draw.text((x+24,y+328), "16 / 24 / 32 px", font=font(15), fill="#71717A")
        for j, background in enumerate(("#F4F4F5", "#27272A")):
            bx, by = x+22+j*194, y+367
            draw.rounded_rectangle((bx,by,bx+182,by+70), radius=12, fill=background)
            for dx,size in ((24,16),(72,24),(126,32)):
                small=icon.resize((size,size),Image.Resampling.LANCZOS)
                board.paste(small,(bx+dx,by+35-size//2),small)
    board.save(OUT / "03-nodes-gray-comparison.png")
    print(OUT / "03-nodes-gray-comparison.png")


if __name__ == "__main__":
    main()
