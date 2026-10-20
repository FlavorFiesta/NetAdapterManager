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
    draw.line([(int(x * SCALE), int(y * SCALE)) for x, y in points], fill=fill, width=width * SCALE, joint="curve")


navy = "#0B1B38"
blue = "#4F86F7"
white = "#FFFFFF"

draw.rounded_rectangle(box((4, 4, 252, 252)), radius=42 * SCALE, fill=navy)

# Ethernet socket body.
draw.rounded_rectangle(box((42, 62, 214, 168)), radius=16 * SCALE, outline=white, width=10 * SCALE)
draw.rectangle(box((63, 78, 193, 139)), fill=blue)
for x in (74, 103, 132, 161):
    draw.rectangle(box((x, 78, x + 15, 112)), fill=white)

# Plug and cable.
line([(95, 168), (95, 205), (161, 205), (161, 168)], white, 10)
line([(128, 205), (128, 239)], white, 10)
line([(113, 205), (143, 205)], blue, 8)

image = image.resize((SIZE, SIZE), Image.Resampling.LANCZOS)
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
image.save(OUTPUT, format="ICO", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print(OUTPUT)
