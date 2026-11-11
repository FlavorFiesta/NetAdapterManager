from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets" / "AppIcon.ico"
SCALE = 4
SIZE = 256

image = Image.new("RGBA", (SIZE * SCALE, SIZE * SCALE), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)


def box(values):
    return tuple(int(value * SCALE) for value in values)


def line(points, fill, width):
    scaled = [(int(x * SCALE), int(y * SCALE)) for x, y in points]
    draw.line(scaled, fill=fill, width=width * SCALE, joint="curve")
    radius = width * SCALE / 2
    for x, y in scaled:
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=fill)


gray = "#71717A"
white = "#FFFFFF"

draw.rounded_rectangle(box((8, 8, 248, 248)), radius=52 * SCALE, fill=gray)

# Network nodes: approved proposal 03, gray/white variant B.
line([(128, 85), (128, 128)], white, 12)
line([(78, 171), (78, 132), (178, 132), (178, 171)], white, 12)
for x, y in ((128, 78), (78, 177), (178, 177)):
    draw.rounded_rectangle(box((x - 20, y - 20, x + 20, y + 20)), radius=11 * SCALE, fill=white)

image = image.resize((SIZE, SIZE), Image.Resampling.LANCZOS)
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
image.save(OUTPUT, format="ICO", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print(OUTPUT)
