"""Builds store/trailer/DropMyShelf-trailer.mp4: a ~25 s 1920x1080 presentation video, plus its
thumbnail (both what the Microsoft Store asks for a trailer).

Uses the renders from tools/screenshots (docs/ and the sprites folder) and the demo scene from
make_demo_gif.py. Needs Pillow and FFmpeg (winget install Gyan.FFmpeg.Essentials).

Usage: python tools/make_trailer.py [sprites folder, default %TEMP%\\dropmyshelf-sprites]
"""

import os
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parent))
from make_demo_gif import backdrop, demo_frames, ease, fade, with_alpha  # noqa: E402
from make_logo_png import logo  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
DOCS = ROOT / "docs"
OUT = ROOT / "store" / "trailer"
WIDTH, HEIGHT = 1920, 1080
FPS = 30
FONTS = Path("C:/Windows/Fonts")

BACKGROUND = backdrop(WIDTH, HEIGHT).convert("RGBA")


def font(name: str, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(FONTS / name), size)


TITLE = font("seguisb.ttf", 120)
HEADLINE = font("seguisb.ttf", 72)
BODY = font("segoeui.ttf", 40)


def appear(t: float, start: float, length: float, end: float, out: float = 0.35) -> float:
    """Opacity of an element that fades in at `start` and out before the scene ends at `end`."""
    return ease(fade(t, start, start + length)) * (1 - ease(fade(t, end - out, end)))


def text(canvas: Image.Image, xy, words: str, typeface, alpha: float, color=(255, 255, 255), rise: float = 0) -> None:
    if alpha <= 0:
        return
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).text((xy[0], xy[1] + rise * (1 - alpha)), words, font=typeface,
                               fill=(*color, round(255 * alpha)), anchor="mm")
    canvas.alpha_composite(layer)


def image(canvas: Image.Image, picture: Image.Image, center, alpha: float, rise: float = 40) -> None:
    if alpha <= 0:
        return
    y = center[1] + rise * (1 - alpha)
    canvas.alpha_composite(with_alpha(picture, alpha), (round(center[0] - picture.width / 2), round(y - picture.height / 2)))


def fit(path: Path, height: int) -> Image.Image:
    picture = Image.open(path).convert("RGBA")
    return picture.resize((round(picture.width * height / picture.height), height), Image.LANCZOS)


# ---- Scenes: each takes the time within the scene and returns an RGBA frame ----

MARK = logo(300)
SHELF = fit(DOCS / "shelf.png", 430)
EXPANDED = fit(DOCS / "shelf-expanded.png", 600)
LIBRARY = fit(DOCS / "library.png", 700)


def title_scene(t: float, length: float) -> Image.Image:
    frame = BACKGROUND.copy()
    grow = ease(fade(t, 0.0, 0.7))
    out = 1 - ease(fade(t, length - 0.35, length))
    mark = MARK.resize((round(300 * (0.8 + 0.2 * grow)),) * 2, Image.LANCZOS)
    image(frame, mark, (WIDTH / 2, 380), grow * out, rise=0)
    text(frame, (WIDTH / 2, 640), "DropMyShelf", TITLE, appear(t, 0.5, 0.6, length), rise=30)
    text(frame, (WIDTH / 2, 775), "A drop zone that appears when you need it.", BODY,
         appear(t, 1.0, 0.6, length), color=(220, 220, 240), rise=20)
    return frame


def problem_scene(t: float, length: float) -> Image.Image:
    frame = BACKGROUND.copy()
    text(frame, (WIDTH / 2, 500), "Moving files shouldn't mean", HEADLINE, appear(t, 0.0, 0.5, length), rise=30)
    text(frame, (WIDTH / 2, 600), "juggling windows.", HEADLINE, appear(t, 0.3, 0.5, length), rise=30)
    return frame


def demo_scene_factory():
    frames = list(demo_frames(os.environ["DROPMYSHELF_SPRITES"], scale=WIDTH / 800, fps=FPS))

    def scene(t: float, length: float) -> Image.Image:
        frame = frames[min(len(frames) - 1, int(t * FPS))].convert("RGBA")
        # Ease in and out of the plain backdrop so the cut to and from the demo is soft.
        visible = min(fade(t, 0.0, 0.3), 1 - fade(t, length - 0.3, length))
        return Image.blend(BACKGROUND, frame, visible) if visible < 1 else frame

    return scene, len(frames) / FPS


def collect_scene(t: float, length: float) -> Image.Image:
    frame = BACKGROUND.copy()
    text(frame, (WIDTH / 2, 150), "Collect files from anywhere", HEADLINE, appear(t, 0.0, 0.5, length), rise=30)
    text(frame, (WIDTH / 2, 235), "Then move them all in one drag, or click to take them out one by one.", BODY,
         appear(t, 0.2, 0.5, length), color=(220, 220, 240))
    image(frame, SHELF, (WIDTH * 0.37, 650), appear(t, 0.4, 0.6, length))
    image(frame, EXPANDED, (WIDTH * 0.63, 650), appear(t, 0.7, 0.6, length))
    return frame


def library_scene(t: float, length: float) -> Image.Image:
    frame = BACKGROUND.copy()
    text(frame, (WIDTH / 2, 130), "Your recent shelves, one click away", HEADLINE, appear(t, 0.0, 0.5, length), rise=30)
    image(frame, LIBRARY, (WIDTH / 2, 640), appear(t, 0.3, 0.6, length))
    return frame


FEATURES = [
    "Files, folders, web images, text and links",
    "Never steals focus from the app you're in",
    "Starts with Windows, stays in the tray",
    "No account, no ads, no tracking",
]


def features_scene(t: float, length: float) -> Image.Image:
    frame = BACKGROUND.copy()
    # Center the block as a whole: check mark (44) + gap (28) + the longest line.
    widest = max(ImageDraw.Draw(frame).textlength(f, font=BODY) for f in FEATURES)
    left = (WIDTH - (72 + widest)) / 2 + 22
    for i, feature in enumerate(FEATURES):
        alpha = appear(t, 0.2 + i * 0.35, 0.45, length)
        y = 375 + i * 110
        if alpha > 0:
            layer = Image.new("RGBA", frame.size, (0, 0, 0, 0))
            draw = ImageDraw.Draw(layer)
            x = left + 40 * (1 - alpha)
            draw.ellipse((x - 22, y - 22, x + 22, y + 22), fill=(88, 101, 242, round(255 * alpha)))
            draw.line([(x - 10, y), (x - 3, y + 8), (x + 11, y - 8)], fill=(255, 255, 255, round(255 * alpha)), width=5)
            draw.text((x + 50, y), feature, font=BODY, fill=(255, 255, 255, round(255 * alpha)), anchor="lm")
            frame.alpha_composite(layer)
    return frame


def end_scene(t: float, length: float) -> Image.Image:
    frame = BACKGROUND.copy()
    image(frame, logo(240), (WIDTH / 2, 380), appear(t, 0.0, 0.6, length + 1), rise=0)
    text(frame, (WIDTH / 2, 600), "DropMyShelf", TITLE, appear(t, 0.3, 0.6, length + 1), rise=30)
    text(frame, (WIDTH / 2, 710), "Drag. Shake. Drop.", BODY, appear(t, 0.6, 0.6, length + 1), color=(220, 220, 240))
    text(frame, (WIDTH / 2, 800), "Free for Windows 10 and 11", BODY, appear(t, 0.9, 0.6, length + 1),
         color=(170, 178, 255))
    return frame


def find_ffmpeg() -> str:
    found = shutil.which("ffmpeg")
    if found:
        return found
    packages = Path(os.environ["LOCALAPPDATA"]) / "Microsoft" / "WinGet" / "Packages"
    for candidate in packages.glob("Gyan.FFmpeg*/**/bin/ffmpeg.exe"):
        return str(candidate)
    sys.exit("FFmpeg not found: winget install Gyan.FFmpeg.Essentials")


def main() -> None:
    sprites = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(os.environ["TEMP"]) / "dropmyshelf-sprites"
    os.environ["DROPMYSHELF_SPRITES"] = str(sprites)
    OUT.mkdir(parents=True, exist_ok=True)
    video = OUT / "DropMyShelf-trailer.mp4"

    demo, demo_length = demo_scene_factory()
    scenes = [
        (title_scene, 3.2),
        (problem_scene, 2.6),
        (demo, demo_length),
        (collect_scene, 3.8),
        (library_scene, 3.4),
        (features_scene, 3.6),
        (end_scene, 3.5),
    ]

    encoder = subprocess.Popen(
        [find_ffmpeg(), "-y", "-loglevel", "error",
         "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{WIDTH}x{HEIGHT}", "-r", str(FPS), "-i", "-",
         "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p",
         "-movflags", "+faststart", str(video)],
        stdin=subprocess.PIPE)
    frames = 0
    for scene, length in scenes:
        for i in range(round(length * FPS)):
            encoder.stdin.write(scene(i / FPS, length).convert("RGB").tobytes())
            frames += 1
    encoder.stdin.close()
    if encoder.wait():
        sys.exit("FFmpeg failed")

    # Trailer thumbnail: the title card once everything has appeared.
    title_scene(2.0, 3.2).convert("RGB").save(OUT / "DropMyShelf-trailer-thumbnail.png")
    print(f"{video}: {frames} frames, {frames / FPS:.1f} s, {video.stat().st_size / 1e6:.1f} MB")


if __name__ == "__main__":
    main()
