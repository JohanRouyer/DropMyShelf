"""Generates the MSIX logo images in packaging/Assets from Assets/DropMe.ico. Needs Pillow.

Usage: python tools/make_store_assets.py
"""

from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "packaging" / "Assets"


def icon(size: int) -> Image.Image:
    ico = Image.open(ROOT / "Assets" / "DropMe.ico")
    ico.size = max(ico.ico.sizes())  # the 256 px frame
    return ico.convert("RGBA").resize((size, size), Image.LANCZOS)


def tile(width: int, height: int, fill: float) -> Image.Image:
    """The icon centered on a transparent tile, taking `fill` of the tile's height."""
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    glyph = icon(round(height * fill))
    canvas.alpha_composite(glyph, ((width - glyph.width) // 2, (height - glyph.height) // 2))
    return canvas


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    images = {}
    for scale in (100, 200):
        f = scale / 100
        images[f"StoreLogo.scale-{scale}.png"] = icon(round(50 * f))
        images[f"Square44x44Logo.scale-{scale}.png"] = icon(round(44 * f))
        images[f"Square150x150Logo.scale-{scale}.png"] = tile(round(150 * f), round(150 * f), 0.6)
        images[f"Wide310x150Logo.scale-{scale}.png"] = tile(round(310 * f), round(150 * f), 0.6)
    # Taskbar, Start and Explorer icons at exact sizes, with and without the tile plate.
    for size in (16, 24, 32, 48, 256):
        images[f"Square44x44Logo.targetsize-{size}.png"] = icon(size)
        images[f"Square44x44Logo.targetsize-{size}_altform-unplated.png"] = icon(size)

    for name, image in images.items():
        image.save(OUT / name)
    print(f"{len(images)} images in {OUT}")


if __name__ == "__main__":
    main()
