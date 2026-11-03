"""Compose Workshop panels from repository sprites. Requires Pillow and Windows fonts.

Run from any directory with the Python runtime documented in docs/artwork-tooling.md.
Reference layout: DetVisor's Pyrinth item cards, Workshop item 3336544632.
No upstream images, game assets or fonts are copied into the output directory.
"""
from pathlib import Path
import math
import re
import random
import textwrap
import xml.etree.ElementTree as ET

from PIL import Image, ImageDraw, ImageFont, ImageChops

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs/images/workshop"
OUT.mkdir(parents=True, exist_ok=True)
W = 1272
BG, INK, MUTED, GOLD = "#202529", "#f5eddf", "#c2c3bd", "#cfaf79"
BLUE, ROSE, GREEN, ORANGE = "#8ed5ff", "#f5bdcd", "#c3e58b", "#ffbd79"
FONT = Path("C:/Windows/Fonts")
RENDERED = []


def font(size, title=False):
    return ImageFont.truetype(str(FONT / ("georgia.ttf" if title else "segoeui.ttf")), size)


def text(x, y, value, size=34, color=INK, title=False, center=False):
    f = font(size, title)
    if center:
        x -= draw.textlength(value, font=f) / 2
    if x < 0 or x + draw.textlength(value, font=f) > W - 16:
        raise ValueError("Text exceeds panel width: " + value)
    draw.text((x, y), value, font=f, fill=color)


def panel(title, subtitle, height, accent):
    global canvas, draw
    canvas = Image.new("RGB", (W, height), BG)
    draw = ImageDraw.Draw(canvas)
    draw.line((48, 10, W - 48, 10), fill=GOLD, width=2)
    draw.polygon(((W - 72, 31), (W - 61, 46), (W - 72, 61), (W - 83, 46)), fill=accent)
    text(48, 30, title, 53, title=True)
    text(50, 105, subtitle, 32, MUTED)
    draw.line((48, 164, W - 48, 164), fill="#40505b", width=2)


def sprite(path, box, rotation=0):
    im = Image.open(ROOT / "Textures" / (path + ".png")).convert("RGBA")
    # A map graphic stretches its full texture by drawSize before alpha cropping.
    # Preserve that presentation, especially the narrow lamp and tall pylon.
    ratio = ASPECTS.get(path, im.width / im.height) / (im.width / im.height)
    im = im.resize((max(1, round(im.width * ratio)), im.height), Image.Resampling.LANCZOS)
    im = im.crop(im.getbbox())
    if rotation:
        im = im.rotate(rotation, resample=Image.Resampling.BICUBIC, expand=True)
        im = im.crop(im.getbbox())
    scale = min(box[2] / im.width, box[3] / im.height)
    im = im.resize((round(im.width * scale), round(im.height * scale)), Image.Resampling.LANCZOS)
    x, y = int(box[0] + (box[2] - im.width) / 2), int(box[1] + (box[3] - im.height) / 2)
    if canvas.mode == "RGBA":
        canvas.alpha_composite(im, (x, y))
    else:
        canvas.paste(im, (x, y), im)
    return x, y, im.width, im.height


def save(name):
    path = OUT / (name + ".png")
    canvas.save(path, optimize=True)
    RENDERED.append(path)


def definition(path, name):
    return next(d for d in ET.parse(ROOT / path).getroot() if d.findtext("defName") == name)


DEFS = {}
for folder in ("Defs", "Integrations"):
    for xml in (ROOT / folder).rglob("*.xml"):
        for node in ET.parse(xml).getroot():
            key = node.findtext("defName") or node.get("Name")
            if key:
                DEFS[key] = node


def field(name, path, default=""):
    node = DEFS.get(name)
    if node is None:
        return default
    found = node.findtext(path)
    return found if found is not None else field(node.get("ParentName"), path, default)


ASPECTS = {}
for key in DEFS:
    tex = field(key, "graphicData/texPath")
    size = field(key, "graphicData/drawSize", "1")
    values = [float(v) for v in re.findall(r"[\d.]+", size)]
    ratio = values[0] / values[-1]
    if tex:
        for suffix in ("", "_north", "_south", "_east", "_west", "/" + tex.split("/")[-1] + "_a"):
            ASPECTS[tex + suffix] = ratio


def cost(name):
    node = DEFS[name]
    names = {"KM_Glacite": "Glacite", "KM_Vitalite": "Vitalite", "WoodLog": "wood",
             "Steel": "steel", "MedicineHerbal": "herbal medicine", "KM_LumenFireflyJar": "firefly jar",
             "BlocksMarble": "marble blocks"}
    parts = [f"{li.text} {names.get(li.tag, field(li.tag, "label", li.tag))}" for li in node.findall("costList/*")]
    stuff = int(field(name, "costStuffCount", "0"))
    if stuff:
        categories = field(name, "stuffCategories/li")
        material = "stone blocks" if categories == "Stony" and "Bed" in name else "building material"
        parts.append(f"{stuff} {material}")
    return " + ".join(parts)


def wrap_text(x, y, value, width=820, size=33, color=MUTED):
    line = ""
    for word in value.split():
        candidate = (line + " " + word).strip()
        if draw.textlength(candidate, font=font(size)) > width and line:
            text(x, y, line, size, color)
            y += size + 11
            line = word
        else:
            line = candidate
    text(x, y, line, size, color)
    return y + size + 11


def catalog(items, accent, filename):
    global canvas, draw
    for index, (name, path, title, body, note) in enumerate(items):
        canvas = Image.new("RGBA", (W, 350), (0, 0, 0, 0))
        draw = ImageDraw.Draw(canvas)
        y = 0
        draw.rounded_rectangle((38, y, W - 38, y + 350), radius=14, fill="#2a3033", outline="#5b574d", width=2)
        draw.rounded_rectangle((39, y + 1, 317, y + 349), radius=14, fill="#ddd2bb")
        draw.polygon(((178, y + 50), (285, y + 175), (178, y + 300), (71, y + 175)), fill="#c8c0ad")
        if path:
            if name == "KM_GlaciteBlade":
                sprite(path, (58, y + 35, 238, 278), rotation=45)
            else:
                bounds = sprite(path, (78, y + 65, 198, 218))
                if name == "KM_LumenFireflyColumn":
                    sx, sy, sw, sh = bounds
                    for light in range(7):
                        phase = 1.2 * .8 + light * 2.094395
                        lx = sx + sw / 2 + math.sin(phase) * sw * .22
                        ly = sy + sh / 2 + (light - 3) * sh * .084 + math.cos(phase * .73) * sh * .02
                        draw.rectangle((round(lx - 2), round(ly - 2), round(lx + 2), round(ly + 2)), fill="#d9ff1f")
        else:
            draw.ellipse((111, y + 135, 246, y + 215), outline="#a5657c", width=6)
            draw.ellipse((132, y + 149, 225, y + 201), outline="#a5657c", width=3)
        text(351, y + 21, title, 43, INK, title=True)
        end = wrap_text(354, y + 86, body, size=32)
        if name and DEFS[name].find("costList") is not None:
            text(354, y + 242, cost(name), 30, accent)
        text(354, y + 298, note, 28, GOLD)
        assert end <= y + 230, title + " body exceeds its card"
        canvas = canvas.crop((38, 0, W - 37, 350))
        # Keep the opaque card and portrait intact; only its exterior is transparent.
        mask = Image.new("L", (canvas.width * 4, canvas.height * 4))
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, mask.width - 1, mask.height - 1), radius=56, fill=255)
        mask = mask.resize(canvas.size, Image.Resampling.LANCZOS)
        canvas.putalpha(ImageChops.multiply(canvas.getchannel("A"), mask))
        row_name = f"{filename}-{index + 1:02}"
        save(row_name)


def percent(value):
    return f"{float(value) * 100:g}%"


def arrow(x, y, color=GOLD):
    draw.line((x, y, x + 70, y), fill=color, width=5)
    draw.polygon(((x + 70, y), (x + 51, y - 14), (x + 51, y + 14)), fill=color)


def lineup(items, columns, top=190, row_height=260):
    width = (W - 96) / columns
    for i, (label, path, detail) in enumerate(items):
        x, y = 48 + (i % columns) * width, top + (i // columns) * row_height
        sprite(path, (x + 16, y, int(width - 32), 150))
        text(x + width / 2, y + 166, label, 32, center=True)
        if detail:
            text(x + width / 2, y + 209, detail, 27, MUTED, center=True)


V = "Things/Building/Vitalite/"
M = "Things/Building/Misc/"
R = "Things/Item/Resource/"
CHUNK = "Things/Item/Chunk/VitaliteChunk"
GLACITE = R + "Glacite/Glacite_c"
VITALITE = R + "Vitalite/Vitalite_c"

# Build item cards from current definitions and existing repository artwork.
GROUPS = {
    "lumenstone": ["Resources.xml", "LumenstoneCircle.xml", "LumenstoneFixtures.xml", "LumenstoneAltar.xml"],
    "glacite": ["Buildings.xml", "GlaciteColumn.xml", "Blade.xml"],
    "vitalite": ["Vitalite.xml"],
    "serenite": ["Serenite.xml", "SerenitePendant.xml"],
    "verdite": ["Verdite.xml", "VerditeScythe.xml"],
    "aegite": ["Aegite.xml", "AegiteWarhammer.xml"],
    "ferrite": ["Ferrite.xml", "FerriteShield.xml"],
    "sunstone": ["Sunstone.xml", "SunstoneAxe.xml"],
    "fireflies": ["FireflyJar.xml", "Columns.xml", "PyrinthColumn.xml"],
    "darkstone": ["SacrificialAltar.xml", "DarkStoneFixtures.xml", "Bloodstone.xml"],
    "sanguinite": ["Sanguinite.xml"],
    "dreadstone": ["Dreadstone.xml"],
    "hollowstone": ["Hollowstone.xml", "WorkHusk.xml"],
}
for group, files in GROUPS.items():
    items = []
    for folder in (ROOT / "Defs", ROOT / "Integrations"):
        for xml in folder.rglob("*.xml"):
            if xml.name not in files:
                continue
            for node in ET.parse(xml).getroot():
                name, label = node.findtext("defName"), node.findtext("label")
                if node.tag != "ThingDef" or not name or not label or "Mote_" in name:
                    continue
                tex = field(name, "graphicData/texPath")
                paths = [tex, tex + "_north", tex + "_south", tex + "_c", tex + "/" + tex.split("/")[-1] + "_a"] if tex else []
                path = next((v for v in paths if (ROOT / "Textures" / (v + ".png")).is_file()), None)
                if not path:
                    continue
                sentences = re.split(r"(?<=\.)\s+", node.findtext("description", ""))
                description = next((v for v in sentences if re.search(r"\b(while|within|contributes|repairs|stores|gathers|absorbs|supports|infused|refined)\b", v, re.I)), sentences[0]).strip()
                body = textwrap.shorten(description, width=105, placeholder="... ") if description else label.capitalize() + "."
                items.append((name, path, label[0].upper() + label[1:], body, ""))
    if items:
        catalog(items, ROSE if group in ("vitalite", "sanguinite") else BLUE, group + "-current")
        print(group, len(items))
