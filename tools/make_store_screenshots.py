"""Builds 1920x1080 Microsoft Store screenshots in store/screenshots from the renders in docs/.

Run tools/screenshots first (see README). Needs Pillow.
Usage: python tools/make_store_screenshots.py [sprites folder, default %TEMP%\\dropmyshelf-sprites]
"""

import os
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
DOCS = ROOT / "docs"
OUT = ROOT / "store" / "screenshots"
WIDTH, HEIGHT = 1920, 1080
FONTS = Path("C:/Windows/Fonts")


def backdrop() -> Image.Image:
    top, bottom = (22, 28, 52), (52, 36, 82)
    mask = Image.linear_gradient("L").resize((WIDTH, HEIGHT))
    image = Image.composite(Image.new("RGB", (WIDTH, HEIGHT), bottom), Image.new("RGB", (WIDTH, HEIGHT), top), mask)
    glow = Image.new("L", (WIDTH, HEIGHT), 0)
    ImageDraw.Draw(glow).ellipse((900, -300, 2200, 700), fill=90)
    glow = glow.filter(ImageFilter.GaussianBlur(200))
    return Image.composite(Image.new("RGB", (WIDTH, HEIGHT), (96, 84, 170)), image, glow).convert("RGBA")


def headline(canvas: Image.Image, title: str, subtitle: str) -> None:
    draw = ImageDraw.Draw(canvas)
    draw.text((WIDTH // 2, 120), title, font=ImageFont.truetype(str(FONTS / "seguisb.ttf"), 64),
              fill="white", anchor="mm")
    draw.text((WIDTH // 2, 200), subtitle, font=ImageFont.truetype(str(FONTS / "segoeui.ttf"), 32),
              fill=(255, 255, 255, 190), anchor="mm")


def place(canvas: Image.Image, name: str, center, height: int) -> None:
    image = Image.open(DOCS / name).convert("RGBA")
    image = image.resize((round(image.width * height / image.height), height), Image.LANCZOS)
    canvas.alpha_composite(image, (round(center[0] - image.width / 2), round(center[1] - image.height / 2)))


def place_sprite(canvas: Image.Image, path: Path, center, height: int) -> None:
    image = Image.open(path).convert("RGBA")
    image = image.resize((round(image.width * height / image.height), height), Image.LANCZOS)
    canvas.alpha_composite(image, (round(center[0] - image.width / 2), round(center[1] - image.height / 2)))


def arrow(canvas: Image.Image, start, end) -> None:
    draw = ImageDraw.Draw(canvas)
    color = (255, 255, 255, 150)
    draw.line([start, end], fill=color, width=6)
    draw.polygon([(end[0] + 6, end[1]), (end[0] - 18, end[1] - 16), (end[0] - 18, end[1] + 16)], fill=color)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    sprites = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(os.environ["TEMP"]) / "dropmyshelf-sprites"

    shot = backdrop()
    headline(shot, "Shake the mouse. A shelf appears.", "Drop files on it, find where they go, drag them out.")
    place(shot, "shelf.png", (WIDTH * 0.36, 640), 620)
    place(shot, "shelf-expanded.png", (WIDTH * 0.64, 640), 700)
    shot.convert("RGB").save(OUT / "1-shelves.png")

    shot = backdrop()
    headline(shot, "Your recent shelves, one click away", "Reopen what you set aside earlier from the tray icon.")
    place(shot, "library.png", (WIDTH / 2, 660), 760)
    shot.convert("RGB").save(OUT / "2-library.png")

    # The story of the demo GIF as one still: photo on the desktop, on the shelf, then into a folder.
    shot = backdrop()
    headline(shot, "Drag. Shake. Drop.", "Works with files, folders, images from websites, text and links.")
    place_sprite(shot, sprites / "icon-file.png", (WIDTH * 0.2, 640), 380)
    place_sprite(shot, sprites / "shelf-one.png", (WIDTH * 0.5, 640), 640)
    place_sprite(shot, sprites / "icon-folder.png", (WIDTH * 0.8, 640), 380)
    for x in (WIDTH * 0.335, WIDTH * 0.665):
        arrow(shot, (x - 50, 640), (x + 50, 640))
    shot.convert("RGB").save(OUT / "3-drag-shake-drop.png")

    print(f"Screenshots in {OUT}")


if __name__ == "__main__":
    main()
