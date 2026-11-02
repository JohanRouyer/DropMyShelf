"""Draws the DropMyShelf logo as large PNGs in store/logo (Store logo and 1:1 box art).

Same design as Assets/DropMyShelf.ico, but drawn from its shapes so it stays sharp at any size.
Needs Pillow. Usage: python tools/make_logo_png.py
"""

from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "store" / "logo"
ACCENT = (88, 101, 242, 255)
SUPERSAMPLE = 4


def logo(size: int) -> Image.Image:
    """The icon is designed on a 32-unit grid: a rounded tile with a download-style arrow."""
    big = size * SUPERSAMPLE
    unit = big / 32
    image = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((1 * unit, 1 * unit, 31 * unit, 31 * unit), radius=6 * unit, fill=ACCENT)

    width = 3 * unit
    lines = [
        [(16, 8), (16, 20)],            # arrow shaft
        [(11, 15), (16, 20), (21, 15)],  # arrow head
        [(10, 24), (22, 24)],           # tray line
    ]
    for line in lines:
        points = [(x * unit, y * unit) for x, y in line]
        draw.line(points, fill="white", width=round(width), joint="curve")
        for x, y in points:  # round caps
            r = width / 2
            draw.ellipse((x - r, y - r, x + r, y + r), fill="white")
    return image.resize((size, size), Image.LANCZOS)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for size in (300, 1080, 2160):
        path = OUT / f"DropMyShelf-logo-{size}.png"
        logo(size).save(path)
        print(path)


if __name__ == "__main__":
    main()
