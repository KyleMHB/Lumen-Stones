"""Render Workshop cover candidates from the mod's sprites and local RimWordFont."""

import argparse
import colorsys
import json
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

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
# Every local resource family, with the dark stones grouped in the final row.
STONE_ROWS = [
    [("Lumenstone", "#d8e8ee"), ("Glacite", "#8ed5ff"),
     ("Vitalite", "#f5bdcd"), ("Serenite", "#caa9ed"), ("Verdite", "#c3e58b")],
    [("Aegite", "#f2d789"), ("Ferrite", "#bdc5d2"),
     ("Sunstone", "#ffbf55"), ("Animastone", "#8bd5b3")],
    [("DarkStone", "#b799df"), ("Bloodstone", "#ef70a3"),
     ("Sanguinite", "#f0828c"), ("Dreadstone", "#ed9c60"), ("Hollowstone", "#a5d5d5")],
]


def title(draw, font_path, text, x, y, size, width, color):
    # RimWordFont maps capital O to a symbol rather than the letter.
    text = text.replace("O", "o")
    font = ImageFont.truetype(str(font_path), size)
    while draw.textlength(text, font=font) > width:
        size -= 1
        font = ImageFont.truetype(str(font_path), size)
    draw.text((x, y), text, font=font, fill=color, anchor="mt")


def sprite(canvas, path, box, rotation=0):
    if isinstance(path, Image.Image):
        art = path.copy()
    else:
        with Image.open(ROOT / "Textures" / path) as image:
            art = image.convert("RGBA")
    bounds = art.getchannel("A").point(lambda alpha: 255 if alpha > 8 else 0).getbbox()
    art = art.crop(bounds)
    if rotation:
        art = art.rotate(rotation, resample=Image.Resampling.BICUBIC, expand=True)
        art = art.crop(art.getbbox())
    left, top, right, bottom = box
    scale = min((right - left) / art.width, (bottom - top) / art.height)
    art = art.resize((round(art.width * scale), round(art.height * scale)), Image.Resampling.LANCZOS)
    canvas.alpha_composite(art, (round((left + right - art.width) / 2), round(bottom - art.height)))


def stone_portrait(name):
    sources = ROOT / "artwork/design-sources"
    if name in ("DarkStone", "Bloodstone", "Hollowstone", "Sanguinite", "Dreadstone"):
        family = "darkstone" if name in ("Sanguinite", "Dreadstone") else name.lower()
        folder = sources / (family + "-resource")
        recipe = json.loads((folder / "export.json").read_text())
        art = Image.open(folder / recipe["source"]).convert("RGBA").crop(recipe["crop"])
        if name in ("Sanguinite", "Dreadstone"):
            # Apply the resource recipes at source resolution, before thumbnail reduction.
            pixels = []
            for r, g, b, a in art.getdata():
                h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
                if name == "Sanguinite":
                    rgb = colorsys.hsv_to_rgb(0, min(s * 1.35, .95), min(v * 1.12, 1))
                else:
                    t = max(0, min(1, (v - .24) / .4))
                    t = t * t * (3 - 2 * t)
                    dark = colorsys.hsv_to_rgb(.68, .72, v * .8)
                    hot = colorsys.hsv_to_rgb(.045, .95, v)
                    rgb = tuple(low * (1 - t) + high * t for low, high in zip(dark, hot))
                pixels.append((*[round(c * 255) for c in rgb], a))
            art.putdata(pixels)
        return art

    art = Image.open(ROOT / "artwork/workshop/stone-portraits/lumenstone-master.png").convert("RGBA")
    if name == "Lumenstone":
        return art
    if name == "Animastone":
        palette = json.loads((sources / "animastone-resource/recolor.json").read_text())["palette"]
    else:
        palette = json.loads((sources / "lightstone-resources/recolor.json").read_text())["palettes"][name]
        palette = [tuple(bytes.fromhex(color[1:])) for color in palette]
    stops = [0, 174, 203, 230, 245]
    channels = []
    for channel in range(3):
        lut = []
        for value in range(256):
            index = next((i for i in range(4) if value <= stops[i + 1]), 3)
            t = max(0, min(1, (value - stops[index]) / (stops[index + 1] - stops[index])))
            lut.append(round(palette[index][channel] * (1 - t) + palette[index + 1][channel] * t))
        channels.append(art.convert("L").point(lut))
    return Image.merge("RGBA", (*channels, art.getchannel("A")))


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


def cover_text(canvas, font_path, text, box, color, facets=None, seed=0):
    """Typeset exact font outlines at 2x, with optional broad polygon fills."""
    text = text.replace("O", "o")
    left, top, right, bottom = box
    width, height = 2 * (right - left), 2 * (bottom - top)
    size = height * 2
    while True:
        font = ImageFont.truetype(str(font_path), size)
        bounds = font.getbbox(text)
        if bounds[2] - bounds[0] <= width and bounds[3] - bounds[1] <= height:
            break
        size -= 1
    mask = Image.new("L", (width, height))
    start_x = (width - bounds[2] + bounds[0]) // 2 - bounds[0]
    ImageDraw.Draw(mask).text(
        (start_x, -bounds[1]),
        text, font=font, fill=255,
    )
    # Centre visible ink rather than the font's advance/side bearings.
    visible = mask.getbbox()
    shift_x = (width - visible[2] + visible[0]) // 2 - visible[0]
    centered = Image.new("L", mask.size)
    centered.paste(mask, (shift_x, 0))
    mask = centered
    start_x += shift_x
    ink = Image.new("RGBA", mask.size, color)
    if facets:
        rng = random.Random(seed)
        draw = ImageDraw.Draw(ink)
        # Each letter is one crystal. Independent seams avoid horizontal bands
        # across the word while preserving the font's exact outer silhouette.
        for index, letter in enumerate(text):
            x = start_x + font.getlength(text[:index])
            letter_width = font.getlength(letter)
            a = (rng.uniform(.30, .50), rng.uniform(.24, .43))
            b = (rng.uniform(.48, .72), rng.uniform(.60, .78))
            tl, tr, br, bl = (0, 0), (1, 0), (1, 1), (0, 1)
            t, r = (rng.uniform(.42, .72), 0), (1, rng.uniform(.36, .65))
            lower, l = (rng.uniform(.25, .55), 1), (0, rng.uniform(.32, .65))
            planes = [(tl,t,a), (t,tr,a), (tr,r,a), (r,b,a), (r,br,b),
                      (br,lower,b), (lower,bl,b), (bl,l,b), (l,a,b), (l,tl,a)]
            for plane in planes:
                depth = sum(p[1] for p in plane) / 3
                palette = facets[min(2, int(depth * 3))]
                draw.polygon([(round(x + px * letter_width), round(py * height))
                              for px, py in plane], fill=rng.choice(palette))
        # A fine directional highlight gives the title a crisp finished edge.
        shifted = Image.new("L", mask.size)
        shifted.paste(mask, (2, 3))
        edge = ImageChops.subtract(mask, shifted).point(lambda a: round(a * .55))
        ink.paste("#fff7e6", (0, 0), edge)
    ink.putalpha(mask)
    ink = ink.resize((right - left, bottom - top), Image.Resampling.LANCZOS)
    if facets:
        glow = Image.new("RGBA", canvas.size, color)
        glow_mask = Image.new("L", canvas.size)
        glow_mask.paste(ink.getchannel("A"), (left, top))
        glow.putalpha(glow_mask.filter(ImageFilter.GaussianBlur(10)).point(lambda a: round(a * .32)))
        canvas.alpha_composite(glow)
    canvas.alpha_composite(ink, (left, top))


def fixture_master(folder):
    folder = ROOT / "artwork/design-sources" / folder
    recipe = json.loads((folder / "master.json").read_text())
    return Image.open(folder / "source.png").convert("RGBA").crop(recipe["crop"])


def icon(font_path):
    image = Image.new("RGBA", (1254, 1254), "#001426")
    cover_text(image, font_path, "LUMEN", (64, 46, 1190, 271), "#fff2df", [
        ["#fff9ee", "#fff1df", "#f7eadc"],
        ["#f4f1ed", "#e2e9f2", "#d9e3f0", "#fff8ef"],
        ["#ccd9eb", "#b8cce5", "#e6ecf5", "#d7e2f0"],
    ], seed=11)
    cover_text(image, font_path, "STONES", (47, 292, 1225, 506), "#ffbd27", [
        ["#fff0ad", "#ffe187", "#ffdb72", "#ffe9a3"],
        ["#ffd25d", "#ffc439", "#ffb825", "#ffdf7c"],
        ["#f5a91c", "#ffbd35", "#ffd366", "#eea217"],
    ], seed=29)
    cover_text(image, font_path, "STONES THAT DO THINGS", (62, 597, 1205, 656), "#fff0db")
    for folder, box in [
        ("glacite-cooler", (146, 733, 392, 979)),
        ("vitalite-cluster-banded", (496, 733, 772, 979)),
        ("verdite-pylon-redesign", (928, 717, 1073, 979)),
    ]:
        sprite(image, fixture_master(folder), box)
    for text, box, color in [
        ("COOL", (151, 999, 377, 1055), "#5cdbfa"),
        ("HEAL", (525, 999, 742, 1055), "#f6a8d1"),
        ("GROW", (870, 999, 1137, 1055), "#cefa62"),
    ]:
        cover_text(image, font_path, text, box, color)
    stones = ["Lumenstone", "Serenite", "Aegite", "Ferrite", "Sunstone",
              "Animastone", "DarkStone", "Bloodstone", "Sanguinite", "Hollowstone"]
    for index, name in enumerate(stones):
        path = ROOT / "artwork/workshop/stone-portraits" / (name + ".png")
        sprite(image, Image.open(path).convert("RGBA"),
               (52 + index * 118, 1110, 143 + index * 118, 1217))
    return image.convert("RGB")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--font", type=Path, default=DEFAULT_FONT)
    parser.add_argument("--icon-only", action="store_true", help="Update the preview and mod icon without rendering the gallery cover")
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    master = icon(args.font)
    master.save(ROOT / "artwork/workshop/lumen-stones-typeset-cover-master.png", optimize=True)
    preview = master.resize((512, 512), Image.Resampling.LANCZOS)
    outputs = [(OUT / "lumen-stones-workshop-cover.png", master),
               (OUT / "lumen-stones-workshop-icon.png", preview),
               (ROOT / "About/Preview.png", preview),
               (ROOT / "About/ModIcon.png", preview)]
    if not args.icon_only:
        outputs.append((OUT / "lumen-stones-gallery-cover.png", gallery(args.font)))
    for path, rendered in outputs:
        rendered.save(path, optimize=True)
        print(path, rendered.size, path.stat().st_size)
