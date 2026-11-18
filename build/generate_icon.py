"""Build the Windows icon from the approved transparent PNG master.

Selected design: cable/location variant 05, geometric cable.
Assets/AppIcon.svg is the editable vector source; Assets/AppIcon.png is its
approved 256px export, also consumed directly by WPF. Preserve that master
when packaging instead of redrawing an earlier proposal over it.
"""
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets" / "AppIcon.ico"
SIZES = [(size, size) for size in (16, 20, 24, 32, 40, 48, 64, 128, 256)]

with Image.open(OUTPUT.with_suffix(".png")) as source:
    image = source.convert("RGBA").resize((256, 256), Image.Resampling.LANCZOS)
    image.save(OUTPUT, format="ICO", sizes=SIZES)
print(OUTPUT)
