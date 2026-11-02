"""Draws the DropMyShelf logo as large PNGs in store/logo (Store logo, 1:1 box art, 2:3 poster).

Same design as Assets/DropMyShelf.ico, but drawn from its shapes so it stays sharp at any size.
Needs Pillow. Usage: python tools/make_logo_png.py
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
FONTS = Path("C:/Windows/Fonts")
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


def poster(width: int, height: int) -> Image.Image:
    """2:3 poster art: opaque backdrop (as in the screenshots), logo, name and tagline."""
    top, bottom = (22, 28, 52), (52, 36, 82)
    mask = Image.linear_gradient("L").resize((width, height))
    image = Image.composite(Image.new("RGB", (width, height), bottom), Image.new("RGB", (width, height), top), mask)
    glow = Image.new("L", (width, height), 0)
    ImageDraw.Draw(glow).ellipse((-width * 0.3, height * 0.08, width * 1.3, height * 0.62), fill=110)
    glow = glow.filter(ImageFilter.GaussianBlur(width / 6))
    image = Image.composite(Image.new("RGB", (width, height), (96, 84, 170)), image, glow).convert("RGBA")

    mark = logo(round(width * 0.46))
    image.alpha_composite(mark, ((width - mark.width) // 2, round(height * 0.24)))

    draw = ImageDraw.Draw(image)
    title = ImageFont.truetype(str(FONTS / "seguisb.ttf"), round(width * 0.105))
    tagline = ImageFont.truetype(str(FONTS / "segoeui.ttf"), round(width * 0.042))
    draw.text((width / 2, height * 0.64), "DropMyShelf", font=title, fill="white", anchor="mm")
    draw.text((width / 2, height * 0.715), "Drag. Shake. Drop.", font=tagline, fill=(255, 255, 255, 200), anchor="mm")
    return image.convert("RGB")


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for size in (300, 1080, 2160):
        path = OUT / f"DropMyShelf-logo-{size}.png"
        logo(size).save(path)
        print(path)
    path = OUT / "DropMyShelf-poster-1440x2160.png"
    poster(1440, 2160).save(path)
    print(path)


if __name__ == "__main__":
    main()
