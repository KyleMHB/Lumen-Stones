"""Render Workshop cover candidates from the mod's sprites and local RimWordFont."""

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_FONT = Path(r"E:\Coding\Rimworld\EverythingHasQualityReforked\artifacts\steam-page-mockup\RimWordFont.ttf")
OUT = ROOT / "docs/images/workshop"
BACKGROUND = "#10232f"
CREAM = "#f5eedc"
GOLD = "#edc76c"
FIXTURES = [
    ("LUMENSTONE", "Things/Building/Lumenstone/Cluster.png", "#d8e8ee"),
    ("GLACITE", "Things/Building/Misc/GlaciteCooler.png", "#8ed5ff"),
    ("VITALITE", "Things/Building/Vitalite/Cluster.png", "#f5bdcd"),
    ("SERENITE", "Things/Building/Serenite/Cluster.png", "#caa9ed"),
    ("VERDITE", "Things/Building/Verdite/Cluster.png", "#c3e58b"),
    ("AEGITE", "Things/Building/Aegite/Cluster.png", "#f2d789"),
    ("FERRITE", "Things/Building/Ferrite/Cluster.png", "#bdc5d2"),
    ("DARK STONE", "Things/Building/DarkStone/Cluster.png", "#b799df"),
    ("PYRINTH", "Lumenstone/PyrinthColumn.png", "#f0a36b"),
]


def title(draw, font_path, text, x, y, size, width, color):
    # RimWordFont maps capital O to a symbol rather than the letter.
    text = text.replace("O", "o")
    font = ImageFont.truetype(str(font_path), size)
    while draw.textlength(text, font=font) > width:
        size -= 1
        font = ImageFont.truetype(str(font_path), size)
    draw.text((x, y), text, font=font, fill=color, anchor="mt")


def sprite(canvas, path, box):
    with Image.open(ROOT / "Textures" / path) as image:
        art = image.convert("RGBA")
    bounds = art.getchannel("A").point(lambda alpha: 255 if alpha > 8 else 0).getbbox()
    art = art.crop(bounds)
    left, top, right, bottom = box
    scale = min((right - left) / art.width, (bottom - top) / art.height)
    art = art.resize((round(art.width * scale), round(art.height * scale)), Image.Resampling.LANCZOS)
    canvas.alpha_composite(art, (round((left + right - art.width) / 2), round(bottom - art.height)))


def gallery(font_path):
    image = Image.new("RGBA", (1920, 1080), BACKGROUND)
    draw = ImageDraw.Draw(image)
    title(draw, font_path, "LUMEN", 960, 24, 155, 1700, CREAM)
    title(draw, font_path, "STONES", 960, 171, 185, 1700, GOLD)
    title(draw, font_path, "CRYSTALS, FURNISHINGS AND RITUALS", 960, 375, 45, 1700, CREAM)
    for i, (_, _, color) in enumerate(FIXTURES):
        draw.rectangle((round(i * 1920 / len(FIXTURES)), 440, round((i + 1) * 1920 / len(FIXTURES)), 449), fill=color)
    draw.rectangle((0, 450, 1919, 1079), fill="#273b43")
    for i, (label, path, color) in enumerate(FIXTURES):
        row, column = divmod(i, 5)
        x = column * 384 + (192 if row else 0)
        y = 455 + row * 310
        sprite(image, path, (x + 65, y + 8, x + 319, y + 222))
        title(draw, font_path, label, x + 192, y + 230, 38, 340, color)
        if label == "PYRINTH":
            title(draw, font_path, "SUPPoRTS EPoCHS - PYRINTH", x + 192, y + 270, 21, 350, CREAM)
            title(draw, font_path, "BY DETVISoR", x + 192, y + 294, 20, 340, CREAM)
    return image.convert("RGB")


def icon(font_path):
    image = Image.new("RGBA", (512, 512), BACKGROUND)
    draw = ImageDraw.Draw(image)
    title(draw, font_path, "LUMEN", 256, 28, 100, 460, CREAM)
    title(draw, font_path, "STONES", 256, 126, 115, 460, GOLD)
    own_fixtures = [item for item in FIXTURES if item[0] != "PYRINTH"]
    for i, (_, _, color) in enumerate(own_fixtures):
        draw.rectangle((round(i * 512 / len(own_fixtures)), 255, round((i + 1) * 512 / len(own_fixtures)), 259), fill=color)
    draw.rectangle((0, 260, 511, 511), fill="#273b43")
    for i, (_, path, _) in enumerate(own_fixtures):
        row, column = divmod(i, 4)
        x = column * 128
        y = 264 + row * 121
        sprite(image, path, (x + 12, y + 4, x + 116, y + 110))
    return image.convert("RGB")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--font", type=Path, default=DEFAULT_FONT)
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    for name, rendered in (("lumen-stones-workshop-icon.png", icon(args.font)),
                           ("lumen-stones-gallery-cover.png", gallery(args.font))):
        path = OUT / name
        rendered.save(path, optimize=True)
        print(path, rendered.size, path.stat().st_size)
