"""Build a multi-resolution Windows .ico from an SVG.

Usage:
    python scripts/make-ico.py <input.svg> <output.ico>

Rasterizes the SVG at 16/24/32/48/64/128/256 px using resvg, then packs the
PNGs into an ICO container using Pillow. No Cairo/ImageMagick required.
"""

import io
import sys
from pathlib import Path

import resvg_py
from PIL import Image


SIZES = (16, 24, 32, 48, 64, 128, 256)


def render(svg_text: str, px: int) -> Image.Image:
    png_bytes = resvg_py.svg_to_bytes(svg_string=svg_text, width=px, height=px)
    return Image.open(io.BytesIO(bytes(png_bytes))).convert("RGBA")


def main(svg_path: Path, ico_path: Path) -> None:
    svg_text = svg_path.read_text(encoding="utf-8")
    frames = [render(svg_text, s) for s in SIZES]
    # Pillow packs all supplied sizes into the .ico when the primary image is the
    # largest and the `sizes` kwarg enumerates the extra resolutions.
    primary = frames[-1]
    primary.save(
        ico_path,
        format="ICO",
        sizes=[(f.width, f.height) for f in frames],
    )
    print(f"wrote {ico_path} ({len(frames)} sizes)")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(__doc__, file=sys.stderr)
        sys.exit(2)
    main(Path(sys.argv[1]), Path(sys.argv[2]))
