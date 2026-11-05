"""Compose 1920x1080 Workshop gallery trees from current repository sprites.

Run from any directory with the Python runtime documented in docs/artwork-tooling.md.
Style reference: the existing mod thumbnail and render-workshop-cover.py.
Uses the same local RimWordFont as the cover renderer.
No upstream images, game assets or fonts are copied into the output directory.
"""
from pathlib import Path
import math
import json
import argparse
import re
import random
import textwrap
import xml.etree.ElementTree as ET

from PIL import Image, ImageDraw, ImageFont, ImageChops, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs/images/workshop"
OUT.mkdir(parents=True, exist_ok=True)
W = 1272
BG, INK, MUTED, GOLD = "#273b43", "#f5eedc", "#c2c3bd", "#edc76c"
BLUE, ROSE, GREEN, ORANGE = "#8ed5ff", "#f5bdcd", "#c3e58b", "#ffbd79"
FONT = Path("C:/Windows/Fonts")
TITLE_FONT = Path(r"E:\Coding\Rimworld\EverythingHasQualityReforked\artifacts\steam-page-mockup\RimWordFont.ttf")
RENDERED = []
# Workshop-only use of the current design recipes; no runtime textures or masks are changed.
DESIGN_ART = {
    "Things/Building/Aegite/ShieldProjector": "projector",
    "Things/Building/Misc/LuniteStabilizer": "stabilizer",
}


def font(size, title=False):
    return ImageFont.truetype(str(TITLE_FONT if title else FONT / "seguisb.ttf"), size)


def text(x, y, value, size=34, color=INK, title=False, center=False):
    if title:
        value = value.replace("&", "and").upper().replace("O", "o")
    f = font(size, title)
    if center:
        x -= draw.textlength(value, font=f) / 2
    if x < 0 or x + draw.textlength(value, font=f) > W - 16:
        raise ValueError("Text exceeds panel width: " + value)
    draw.text((x, y), value, font=f, fill=color, anchor="lt")


def panel(title, subtitle, height, accent):
    global canvas, draw
    canvas = Image.new("RGB", (W, height), BG)
    draw = ImageDraw.Draw(canvas)
    draw.line((48, 10, W - 48, 10), fill=GOLD, width=2)
    draw.polygon(((W - 72, 31), (W - 61, 46), (W - 72, 61), (W - 83, 46)), fill=accent)
    text(48, 30, title, 53, title=True)
    text(50, 105, subtitle, 32, MUTED)
    draw.line((48, 164, W - 48, 164), fill="#40505b", width=2)


def stone_portrait(path):
    if path.startswith("Things/Item/Resource/"):
        stone = path.split("/")[-1].removesuffix("_a")
        if stone == "LumenStonesPyrinth":
            stone = "Pyrinth"
        candidate = ROOT / "artwork/workshop/stone-portraits" / (stone + ".png")
        if candidate.is_file():
            return candidate
    return None


def sprite(path, box, rotation=0, max_scale=1.5, bottom=False):
    global draw
    portrait = stone_portrait(path)
    source = ROOT / "Textures" / (path + ".png")
    if not source.is_file() and path in DESIGN_ART:
        directory = ROOT / "artwork/design-sources/bastion-lunite" / DESIGN_ART[path]
        recipe = json.loads((directory / "master.json").read_text())
        source = directory / recipe.get("paintedColor", recipe["layers"][0]["path"])
    im = Image.open(portrait or source).convert("RGBA")
    # A map graphic stretches its full texture by drawSize before alpha cropping.
    # Preserve that presentation, especially the narrow lamp and tall pylon.
    ratio = 1 if portrait else ASPECTS.get(path, im.width / im.height) / (im.width / im.height)
    im = im.resize((max(1, round(im.width * ratio)), im.height), Image.Resampling.LANCZOS)
    im = im.crop(im.getbbox())
    if rotation:
        im = im.rotate(rotation, resample=Image.Resampling.BICUBIC, expand=True)
        im = im.crop(im.getbbox())
    scale = min(box[2] / im.width, box[3] / im.height, max_scale)
    im = im.resize((round(im.width * scale), round(im.height * scale)), Image.Resampling.LANCZOS)
    x, y = int(box[0] + (box[2] - im.width) / 2), int(box[1] + (box[3] - im.height) / 2)
    if bottom:
        y = int(box[1] + box[3] - im.height)
    if canvas.mode == "RGBA":
        canvas.alpha_composite(im, (x, y))
    else:
        canvas.paste(im, (x, y), im)
    # Alpha composition can replace Pillow's pixel storage; refresh the drawing handle.
    draw = ImageDraw.Draw(canvas)
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
            if key and node.tag == "ThingDef":
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


PALETTES = {
    "lumenstone": "#ded8cb", "glacite": "#7dcfff", "vitalite": "#f3bbcf",
    "serenite": "#c4a5f0", "verdite": "#c7e56c", "aegite": "#ead391",
    "ferrite": "#de9a74", "sunstone": "#ffbf55", "animastone": "#8bd5b3",
    "darkstone": "#b08cdd", "bloodstone": "#ef70a3", "sanguinite": "#f0828c",
    "dreadstone": "#ed9c60", "hollowstone": "#a5d5d5", "fireflies": "#d3ed80",
}
ROLES = {
    "lumenstone": "The raw mineral and its ritual furnishings",
    "glacite": "Cold light and deep cooling", "vitalite": "Restoration and hospital support",
    "serenite": "Rest, comfort and tranquil bedrooms", "verdite": "Growth, harvests and sustenance",
    "aegite": "Fire wards and protection", "ferrite": "Slow repairs for buildings and gear",
    "sunstone": "Daylight collected, stored and spent", "animastone": "Meditation and anima attunement  /  Royalty",
    "darkstone": "Sacrifice, corruption and ritual support  /  Ideology",
    "bloodstone": "Storage for harvested Warp energy  /  Ideology",
    "sanguinite": "Blood, scars and regeneration  /  Ideology",
    "dreadstone": "Subjugation, terror and capture  /  Ideology",
    "hollowstone": "Pain, endurance and bound labour  /  Ideology",
    "fireflies": "Living green light and optional Pyrinth warmth",
}
# Public panel copy follows the current ThingDef descriptions and DLC load folders.
EFFECTS = {
    "lumenstone": ("Infusion", "Raw material", "", ["Base stone for all infused Lumenstones", "Improve altar ritual quality", "Raise ritual yields"], ["Ideology enables altar rituals", "Mine, find or buy Lumenstone"]),
    "glacite": ("Cooling", "Solar for freezing", "", ["Cool rooms", "Chill food to 4 C", "Freeze food to -10 C"], ["Larger rooms need more cooling", "Severe damage can cause a burst"]),
    "vitalite": ("Restoration", "Solar for healing", "", ["Up to +10% immunity", "Up to +25% healing", "Fewer wound infections"], ["Roofed room; patients in bed", "Doctors and medicine still needed"]),
    "serenite": ("Tranquility", "Solar for faster sleep", "", ["More comfort", "Improve room mood", "Sleep faster"], ["Support needs an enclosed room", "Distinct fixture types count once"]),
    "verdite": ("Growth", "Solar grow lights", "", ["Faster plant growth", "Faster farm work", "Scheduled growing light"], ["Strongest source applies", "Works indoors and outdoors"]),
    "aegite": ("Warding", "Solar for active fixtures", "", ["Cleans nearby filth", "Suppress fires", "Shields for personal defence"], ["Strongest ward applies", "Shield dome shown on bastion slide"]),
    "ferrite": ("Repair", "Solar for fixture gear repair", "", ["Repair buildings", "Repair nearby items", "Mend worn gear"], ["Fixture repair capacity is shared", "Warhammer mending needs no energy"]),
    "sunstone": ("Solar network", "Harvest daylight", "", ["Gather Solar energy", "Store charge for night", "Power active stone effects"], ["No collection at night or in flares", "Lunite protects individual fixtures"]),
    "animastone": ("Meditation", "Solar active effects", "Requires Royalty", ["Faster psyfocus gain", "Recover neural heat", "Grow anima grass"], ["For tribal and noble psycasters", "Does not weaken anima trees"]),
    "fireflies": ("Living light", "No fuel or electricity", "", ["Moving green light", "No temperature effect", "Optional Pyrinth warmth"], ["All columns support roofs", "Pyrinth needs Epochs - Pyrinth"]),
    "darkstone": ("Rituals", "No energy needed", "Requires Ideology", ["Improve sacrifices", "Create more Dark Stone", "Harvest more Warp energy"], ["Matching fixtures support altars", "Distinct categories count once"]),
    "bloodstone": ("Warp storage", "Stores harvested Warp", "Requires Ideology", ["Store Warp energy", "Power dark-stone effects", "No charge decay"], ["Place near a Sacrificial altar", "Destroyed storage loses its charge"]),
    "sanguinite": ("Recovery", "Uses Warp energy", "Requires Ideology", ["Slow bleeding", "Heal old scars", "Regrow small parts and organs"], ["Patients resting in bed", "No limbs; bionics untouched"]),
    "dreadstone": ("Subjugation", "Uses Warp energy", "Requires Ideology", ["Subjugate captives", "Weaken hostile humanlikes", "Harvest energy from affected deaths"], ["Hostile effects need line of sight", "Does not affect mechanoids"]),
    "hollowstone": ("Endurance", "Uses Warp energy", "Requires Ideology", ["Dull pain", "Resist mental breaks", "Sustain work husks"], ["Work husks also require Anomaly", "Strongest fixture applies"]),
}


def stone_effects(group):
    title, cost, requirement, benefits, notes = EFFECTS[group]
    accent = PALETTES[group]
    left = 1368
    card((1328,260,1892,1052))
    title_size = 48
    while draw.textlength(title.upper().replace("O", "o"), font=font(title_size, True)) > 484:
        title_size -= 1
    text(left,284,title,title_size,accent,title=True)
    width = draw.textlength(cost,font=font(30))
    draw.rounded_rectangle((left,356,left+width+40,404),radius=24,fill="#3a2630" if group in ("darkstone","bloodstone","sanguinite","dreadstone","hollowstone") else "#2c4249")
    height = draw.textbbox((0,0),cost,font=font(30),anchor="lt")[3]
    text(left+20,356+(48-height)/2,cost,30,INK)
    y = 452
    for value in benefits:
        lines = label_lines(value,450,43)
        first_line_box = draw.textbbox((0,0),lines[0],font=font(43),anchor="lt")
        cy = y+(first_line_box[1]+first_line_box[3])/2
        draw.polygon(((left+8,cy-10),(left+18,cy),(left+8,cy+10),(left-2,cy)),fill=accent)
        for index,line in enumerate(lines):
            text(left+40,y+index*54,line,43,INK)
        y += len(lines)*54+28
    assert y <= 920, group + " effects overlap the notes"
    draw.line((left,920,1852,920),fill="#3d4f59",width=2)
    for index,value in enumerate(notes):
        size=30
        while draw.textlength(value,font=font(size))>484:
            size-=1
        assert size>=26,value
        text(left,942+index*46,value,size,MUTED)

GALLERY = []
STYLES = {
    "plain": {"background": "#273b43", "labels": INK},
    "inlay": {"background": "#243941", "labels": INK},
    "facets": {"background": "#1d2b36", "labels": INK},
    "archive": {"background": "#ded8c8", "labels": "#213540"},
    "quiet": {"background": "#152a35", "labels": INK},
    "balanced": {"background": "#273b43", "labels": INK},
}


def gallery_base(title, subtitle, accent, number):
    global canvas, draw, W
    W = 1920
    canvas = Image.new("RGBA", (W, 1080), STYLES[args.style]["background"])
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((0, 0, 1919, 223), fill="#10232f")
    title_size = 108
    display_title = title.replace("&", "and").upper().replace("O", "o")
    title_limit = 1140 if subtitle.startswith("Requires ") and args.style == "plain" else 1490
    while draw.textlength(display_title, font=font(title_size, True)) > title_limit:
        title_size -= 1
    assert title_size >= 80, title + " is too long for the gallery header"
    centered_header = args.style == "plain" and (not subtitle or subtitle.startswith("Requires "))
    title_height = draw.textbbox((0,0),display_title,font=font(title_size,True),anchor="lt")[3]
    text(340, 111-title_height/2 if centered_header else 28, title, title_size, "#f5eedc", title=True)
    if subtitle.startswith("Requires ") and args.style != "balanced":
        width = draw.textlength(subtitle, font=font(34))
        badge_left = 1856-width-44 if centered_header else 340
        badge_top = 84 if centered_header else 150
        badge_height = draw.textbbox((0,0),subtitle,font=font(34),anchor="lt")[3]
        draw.rounded_rectangle((badge_left,badge_top,badge_left+width+44,badge_top+54),radius=27,outline=accent,width=3)
        text(badge_left+22,badge_top+(54-badge_height)/2,subtitle,34,accent)
    elif subtitle:
        text(342, 153, subtitle, 42, "#f5eedc")
    draw.rectangle((0,224,1919,231),fill=accent)
    if args.style == "balanced":
        draw.rectangle((0,224,1919,231),fill="#10232f")
        draw.rectangle((64,225,1856,228),fill=accent)
    if args.style == "quiet":
        draw.rectangle((0,0,1919,231),fill="#152a35")
        text(340,28,title,title_size,INK,title=True)
        text(342,153,subtitle,42,"#b7c0c2")
    if args.style == "inlay":
        draw.line((24,24,24,212),fill="#657071",width=2)
        draw.line((24,24,290,24),fill=GOLD,width=2)
        draw.line((340,139,490,139),fill=accent,width=3)
        draw.polygon(((1824,61),(1844,85),(1824,109),(1804,85)),outline=GOLD,width=2)
    elif args.style == "facets":
        draw.polygon(((1120,232),(1320,232),(1320,775),(1170,610)),fill="#253340")
        draw.polygon(((0,232),(200,232),(0,575)),fill="#283744")
        draw.polygon(((1500,0),(1920,0),(1920,223),(1710,223)),fill="#352833")
        draw.line((1500,0,1710,223),fill="#8a535e",width=3)
    elif args.style == "archive":
        draw.line((24,255,1295,255),fill="#ab9974",width=2)
        draw.line((24,751,1295,751),fill="#ab9974",width=2)
        for x in (24,1295):
            draw.line((x,255,x,305),fill="#ab9974",width=2)
            draw.line((x,701,x,751),fill="#ab9974",width=2)


def label_lines(value, width, size=42):
    lines = []
    line = ""
    for word in value.split():
        candidate = (line+" "+word).strip()
        if draw.textlength(candidate,font=font(size)) > width and line:
            lines.append(line)
            line = word
        else:
            line = candidate
    if line:
        lines.append(line)
    assert len(lines) <= 2, value + " needs more than two label lines"
    assert all(draw.textlength(line,font=font(size)) <= width for line in lines), value
    return lines


def item_label(cx,y,value,width,color=INK,size=42):
    while any(draw.textlength(word,font=font(size))>width for word in value.split()):
        size-=1
    assert size>=30,value + " label is too small"
    for index,line in enumerate(label_lines(value,width,size)):
        text(cx,y+index*(size+7),line,size,color,center=True)


def local_texture(name):
    tex = field(name, "graphicData/texPath")
    paths = [tex, tex+"_c", tex+"_north", tex+"_south", tex+"/"+tex.split("/")[-1]+"_a"] if tex else []
    return next((p for p in paths if p in DESIGN_ART or (ROOT/"Textures"/(p+".png")).is_file()), None)


def family_label(value, group):
    value = {"Living vine bracer": "Vine bracer", "Lumen ritual circle": "Ritual circle"}.get(value,value)
    for prefix in ("Lumenstone ", "Dark Stone ", group.capitalize()+" "):
        if value.startswith(prefix):
            return value[len(prefix):].capitalize()
    return value


def gallery_implant(path, box):
    """Compose larger gallery icons from the retained box and glyph sources."""
    global draw
    source = ROOT / "artwork/design-sources/stone-implants"
    recipe = json.loads((source / "icons.json").read_text())
    entry = recipe["parts"].get(path.rsplit("/", 1)[-1])
    if entry is None:
        return sprite(path,box,max_scale=3)
    size = 144
    base = recipe["boxes"][entry["side"]]
    core = recipe["cores"][entry["stone"]]
    original = Image.open(source / recipe["boxSource"]).convert("RGBA")
    pixels = []
    for r, g, b, a in original.get_flattened_data():
        chroma = max(g, b) - r
        pixels.append(tuple(round((neutral*r + glow*chroma)/255)
                            for neutral,glow in zip(base,core)) + (a,))
    art = Image.new("RGBA", original.size)
    art.putdata(pixels)
    art = art.resize((size,size),Image.Resampling.LANCZOS)
    if entry["glyph"] == "Shard":
        # Use the retained 1254px shard master, not its 64px item export.
        spec = recipe["shard"]
        glyph = Image.open(source / spec["source"]).convert("RGBA").crop(spec["crop"])
        glyph = glyph.rotate(spec["rotation"],Image.Resampling.BICUBIC,expand=True)
        glyph = glyph.crop(glyph.getchannel("A").getbbox())
        glyph.thumbnail(tuple(round(v*size/64) for v in spec["fit"]),Image.Resampling.LANCZOS)
        art.alpha_composite(glyph,((size-glyph.width)//2,round(spec["top"]*size/64)))
    else:
        mask = Image.open(source / f"upstream/{entry['glyph']}.png").getchannel("G")
        mask = mask.resize((size,size),Image.Resampling.LANCZOS)
        glyph = Image.new("RGBA",(size,size),(205,196,213) if entry["side"]=="dark" else (58,57,54))
        glyph.putalpha(mask)
        art.alpha_composite(glyph)
    canvas.alpha_composite(art,(round(box[0]+(box[2]-size)/2),round(box[1]+(box[3]-size)/2)))
    draw = ImageDraw.Draw(canvas)


def card(box):
    draw.rounded_rectangle(box,radius=18,fill="#1c303a",outline="#33474f",width=2)


def glow(box, color, strength=46):
    global draw
    layer = Image.new("RGBA", canvas.size, (0,0,0,0))
    ImageDraw.Draw(layer).ellipse(box, fill=color+f"{strength:02x}")
    canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(70)))
    draw = ImageDraw.Draw(canvas)


def sanguinite_effects(mark_path):
    global draw
    left = 1408
    if args.style == "balanced":
        draw.rectangle((1320,232,1919,1079),fill="#10232f")
        draw.rounded_rectangle((1376,272,1856,337),radius=8,fill="#44333e")
        text(left,283,"Recovery",48,PALETTES["sanguinite"],title=True)
        text(left,367,"Uses Warp energy",36,"#b7c0c2")
        for y,value in ((460,"Slow bleeding"),(550,"Heal old scars"),(640,"Regrow small parts"),(693,"and organs")):
            text(left,y,value,43,INK)
        draw.line((left,797,1816,797),fill="#43535c",width=2)
        text(left,833,"Patients resting in bed",30,"#b7c0c2")
        text(left,876,"No limbs; bionics untouched",29,"#b7c0c2")
        return
    if args.style == "quiet":
        text(left,276,"Recovery",48,PALETTES["sanguinite"],title=True)
        text(left,351,"Uses Warp energy",36,"#b7c0c2")
        for y,value in ((447,"Slow bleeding"),(538,"Heal old scars"),(629,"Regrow small parts"),(682,"and organs")):
            text(left,y,value,43,INK)
        text(left,840,"Patients resting in bed",30,"#b7c0c2")
        text(left,883,"No limbs; bionics untouched",29,"#b7c0c2")
        return
    accent = PALETTES["sanguinite"]
    card((1328,260,1892,1052))
    left = 1368
    text(left,284,"Recovery",48,accent,title=True)
    cost_label = "Uses Warp energy"
    width = draw.textlength(cost_label,font=font(30))
    draw.rounded_rectangle((left,356,left+width+40,404),radius=24,fill="#3a2630")
    label_height = draw.textbbox((0,0),cost_label,font=font(30),anchor="lt")[3]
    text(left+20,356+(48-label_height)/2,cost_label,30,INK)
    for y,lines in ((452,("Slow bleeding",)),(534,("Heal old scars",)),(616,("Regrow small parts","and organs"))):
        first_line_box = draw.textbbox((0,0),lines[0],font=font(43),anchor="lt")
        cy = y+(first_line_box[1]+first_line_box[3])/2
        draw.polygon(((left+8,cy-10),(left+18,cy),(left+8,cy+10),(left-2,cy)),fill=accent)
        for index,value in enumerate(lines):
            text(left+40,y+index*54,value,43,INK)
    draw.line((left,920,1852,920),fill="#3d4f59",width=2)
    text(left,942,"Patients resting in bed",30,MUTED)
    text(left,988,"No limbs; bionics untouched",30,MUTED)
    if args.style == "inlay":
        draw.line((1360,260,1878,260),fill="#617077",width=2)
        draw.line((1360,1038,1878,1038),fill="#617077",width=2)
        for cy in (445,515,585):
            draw.polygon(((1378,cy-7),(1385,cy),(1378,cy+7),(1371,cy)),fill=PALETTES["sanguinite"])
    elif args.style == "facets":
        draw.polygon(((1322,234),(1919,234),(1919,252),(1368,252),(1322,310)),fill=PALETTES["sanguinite"])
        draw.polygon(((1780,1079),(1919,940),(1919,1079)),fill="#442d39")
        for cy in (445,515,585):
            draw.line((1372,cy-13,1387,cy,1372,cy+13),fill=PALETTES["sanguinite"],width=3)
    elif args.style == "archive":
        draw.rectangle((1360,270,1370,650),fill=GOLD)
        draw.line((1408,805,1816,805),fill="#657071",width=2)


def catalog(items, accent, filename):
    group = filename
    accent = PALETTES[group]
    number = list(GROUPS).index(group) + 2
    title = "Dark Stone" if group == "darkstone" else group.capitalize()
    if group == "fireflies":
        title = "Fireflies & Pyrinth"
    gallery_base(title, EFFECTS[group][2] if args.style == "plain" else ("Requires Ideology" if group == "sanguinite" else ROLES[group]), accent, number)
    resource = next((item for item in items if item[0] == "KM_"+title.replace(" ", "")), None)
    if group == "fireflies":
        resource = next(item for item in items if item[0] == "KM_LumenFireflyJar")
    if resource:
        if group != "fireflies":
            master = stone_portrait(resource[1])
            assert master is not None, group + " header lacks its high-resolution portrait"
            with Image.open(master) as image:
                assert min(image.size) >= 512, group + " header source is too small"
        sprite(resource[1], (42, 12, 242, 198), max_scale=3)
    furnishings, equipment, implants = [], [], []
    for item in items:
        name = item[0]
        if item == resource or name == "KM_MineableLumenstone":
            continue
        if any(part in name for part in ("Implant", "Arm", "Leg", "Heart", "Eye", "Jaw", "SeedCrystal")):
            implants.append(item)
        elif field(name, "thingClass").startswith("Building") or field(name, "designationCategory") or name == "KM_LumenCircle" or any(part in name for part in ("Column", "Lamp", "Pylon", "Cluster", "Bed", "Altar", "Vessel", "Obelisk", "Collector", "Array")):
            furnishings.append(item)
        else:
            equipment.append(item)
    # Give the shared five fixture types the main row; beds and ritual buildings
    # share the lower row with equipment rather than shrinking every fixture.
    special = []
    if args.style == "plain":
        special = [item for item in furnishings if any(part in item[0] for part in ("Bed", "Casket", "Altar")) or item[0] == "KM_LumenCircle"]
        furnishings = [item for item in furnishings if item not in special]
    heading_color = "#b7c0c2" if args.style == "quiet" else (INK if args.style == "balanced" else ("#74572e" if args.style == "archive" else GOLD))
    members=special+equipment+implants
    cards = args.style == "plain"
    right = 1300 if cards else (1300 if group == "sanguinite" else 1892)
    if cards:
        draw.rectangle((0,232,1919,1079),fill="#10232f")
        card((28,260,right,752 if members else 1052))
    text(64,284 if cards else 276,"Furnishings",48,heading_color,title=True)
    available = 1256 if cards or group == "sanguinite" else 1792
    width = available/max(len(furnishings),5)
    left = 32 if cards else (1320 if group == "sanguinite" else 1920)/2-width*len(furnishings)/2
    for i,(name,path,item_title,body,note) in enumerate(furnishings):
        x = left+i*width
        # The low-resolution firefly column must reach the same display height as Pyrinth.
        bounds = sprite(path,(x+24,360,width-48,280),bottom=True,max_scale=4 if group == "fireflies" else 1.5) if cards else sprite(path,(x+24,375,width-48,270))
        if name == "KM_LumenFireflyColumn":
            sx,sy,sw,sh=bounds
            for light in range(7):
                lx=sx+sw/2+math.sin(light*2.094395)*sw*.22
                ly=sy+sh/2+(light-3)*sh*.084
                draw.ellipse((lx-2,ly-2,lx+2,ly+2),fill="#d9ff1f")
        item_label(x+width/2,656 if cards else 668,family_label(item_title,group),width-18,STYLES[args.style]["labels"])
    if cards:
        stone_effects(group)
    elif group == "sanguinite":
        sanguinite_effects(resource[1])
    if members and cards:
        card((28,776,right,1052))
        heading = "Equipment & implants" if equipment and implants else ("Equipment" if equipment else "Implants & prosthetics")
        if special:
            heading = "Beds, equipment and implants" if group == "vitalite" else "Rituals and equipment"
        text(64,796,heading,40,GOLD,title=True)
        width=(1256 if cards or group == "sanguinite" else 1792)/max(len(members),5)
        left=32 if cards or group == "sanguinite" else (1920-width*len(members))/2
        for i,(name,path,item_title,body,note) in enumerate(members):
            x=left+i*width
            if path.startswith("Things/Item/Health/"):
                gallery_implant(path,(x,852,width,144))
            else:
                sprite(path,(x+(width-190)/2,852,190,144),max_scale=1.5)
            item_label(x+width/2,1004,family_label(item_title,group),width-12,size=34)
    elif members:
        if args.style != "quiet":
            draw.rectangle((0,775,1319 if group == "sanguinite" else 1919,1079),fill="#10232f")
        heading = "Equipment & implants" if equipment and implants else ("Equipment" if equipment else "Implants & prosthetics")
        text(64,789,heading,44,heading_color if args.style in ("quiet", "balanced") else GOLD,title=True)
        if args.style == "inlay":
            draw.line((64,849,1248,849),fill="#3e515c",width=2)
        elif args.style == "facets":
            draw.polygon(((0,775),(360,775),(0,1000)),fill="#202e3b")
            # Keep the section name above its decorative facet.
            text(64,789,heading,44,GOLD,title=True)
        width=(1256 if cards or group == "sanguinite" else 1792)/max(len(members),5)
        left=32 if cards or group == "sanguinite" else (1920-width*len(members))/2
        for i,(name,path,item_title,body,note) in enumerate(members):
            x=left+i*width
            is_icon=path.startswith("Things/Item/Health/")
            if is_icon:
                gallery_implant(path,(x,840,width,144))
            else:
                sprite(path,(x+(width-190)/2,856,190,96),max_scale=1.5)
            item_label(x+width/2,984,family_label(item_title,group),width-18)
    name=f"gallery-{number:02}-{group}"
    save(name)
    GALLERY.append(name+".png")


def overview():
    gallery_base("Lumen Stones", "", GOLD, 1)
    # The overview title describes the mod, so its stones belong in the two panels.
    draw.rectangle((0,0,1919,223),fill="#10232f")
    title="Lumen Stones"
    display_title=title.upper().replace("O","o")
    height=draw.textbbox((0,0),display_title,font=font(108,True),anchor="lt")[3]
    text(64,111-height/2,title,108,INK,title=True)
    draw.rectangle((0,232,1919,1079),fill="#10232f")
    card((28,260,1090,1052))
    card((1118,260,1892,1052))
    for cx,panel_width,label,resource,stone_size in (
        (559,1062,"Lumen stone","KM_Lumenstone",150),
        (1505,774,"Dark stone","KM_DarkStone",120),
    ):
        display=label.upper().replace("O","o")
        size=58
        while draw.textlength(display,font=font(size,True))+2*stone_size+96>panel_width:
            size-=1
        width=draw.textlength(display,font=font(size,True))
        height=draw.textbbox((0,0),display,font=font(size,True),anchor="lt")[3]
        for x in (cx-width/2-24-stone_size,cx+width/2+24):
            sprite(local_texture(resource),(x,346-stone_size/2,stone_size,stone_size),max_scale=3)
        text(cx,346-height/2,label,size,GOLD,title=True,center=True)
        text(cx,406,"Infuse; convert Sunstone to Lunite at night" if resource == "KM_Lumenstone" else "Infuse to create",29 if resource == "KM_Lumenstone" else 40,INK,center=True)
    lights=["Glacite","Vitalite","Serenite","Verdite","Aegite","Ferrite","Sunstone","Lunite","Animastone","Pyrinth"]
    for index,label in enumerate(lights):
        cx=160+(index%4)*260
        y=466+(index//4)*190
        path=local_texture("KM_"+label)
        if label == "Pyrinth":
            path="Things/Item/Resource/LumenStonesPyrinth/LumenStonesPyrinth_a"
        sprite(path,(cx-95,y,190,110))
        text(cx,y+124,label,36,PALETTES.get(label.lower(),"#ccd9f5" if label == "Lunite" else "#f0a36b"),center=True)
        if label == "Lunite":
            text(cx,y+170,"From Sunstone at night",23,INK,center=True)
        if label in ("Animastone","Pyrinth"):
            text(cx,y+172,"Royalty" if label=="Animastone" else "Optional mod",32,INK,center=True)
    for index,label in enumerate(["Bloodstone","Sanguinite","Dreadstone","Hollowstone"]):
        cx=1320+(index%2)*400
        y=490+(index//2)*300
        sprite(local_texture("KM_"+label),(cx-95,y,190,130))
        text(cx,y+156,label,42,PALETTES[label.lower()],center=True)
    arrow(774,710,"#ccd9f5")
    save("gallery-01-stone-lineage")


INTRO_NAMES = [
    "gallery-intro-01-stones-that-do-things.png",
    "gallery-intro-02-more-than-stones.png",
    "gallery-intro-03-your-balance.png",
    "gallery-intro-04-supply-and-infusion.png",
    "gallery-intro-05-meaningful-choices.png",
    "gallery-intro-06-why-lumen-stones.png",
    "gallery-intro-07-the-dark-path.png",
]


def intro_base(title):
    global canvas, draw, W
    W=1920
    canvas=Image.new("RGBA",(W,1080),"#10232f")
    draw=ImageDraw.Draw(canvas)
    display=title.upper().replace("O","o")
    size=102
    while draw.textlength(display,font=font(size,True))>1792:
        size-=1
    assert size>=76,title + " intro heading is too small"
    height=draw.textbbox((0,0),display,font=font(size,True),anchor="lt")[3]
    text(64,111-height/2,title,size,INK,title=True)
    draw.rectangle((0,224,1919,231),fill=GOLD)


def intro_art(name,box):
    path=local_texture(name)
    assert path,name + " intro artwork is missing"
    return sprite(path,box,max_scale=2)


def intro_copy(x,y,value,width,size=44,color=INK,limit=1052):
    end=wrap_text(x,y,value,width=width,size=size,color=color)
    assert end<=limit,value + " exceeds its intro panel"
    return end


def intro_save(index):
    save(Path(INTRO_NAMES[index]).stem)


def intro_slides():
    # The personal motivation closes the introduction after the broader value proposition.
    intro_base("A longer early game")
    card((28,260,1892,1052))
    text(64,296,"Wish the early game lasted longer?",68,GOLD)
    intro_copy(64,406,"Without rushing freezers or advanced medicine?",1760,50)
    intro_copy(64,516,"That's part of why I made Lumen Stones.",1760,46)
    intro_copy(64,590,"The other part is that I have a real love for things that glow and elemental stones in games that do cool things - and this is both.",1760,46,limit=704)
    for cx,name,label,accent in [(400,"Glacite","Cooling",BLUE),(960,"Vitalite","Restoration",ROSE),(1520,"Verdite","Growth",GREEN)]:
        intro_art("KM_"+name,(cx-160,714,320,200))
        text(cx,960,label,44,accent,center=True)
    intro_save(5)

    # The opening slide leads with the elemental fantasy and practical benefits.
    # Each promise has the actual fixture that provides it.
    intro_base("Stones that do things")
    examples=[
        ("Glacite","Freeze your food","KM_GlaciteCooler",BLUE),
        ("Vitalite","Help your wounded recover","KM_VitaliteCluster",ROSE),
        ("Verdite","Grow under crystal light","KM_VerditePylon",GREEN),
    ]
    for index,(stone,label,name,accent) in enumerate(examples):
        left=28+index*630
        card((left,260,left+604,934))
        text(left+36,292,stone,44,accent,title=True)
        intro_art(name,(left+72,354,460,326))
        for line_index,line in enumerate(label_lines(label,532,48)):
            text(left+302,734+line_index*59,line,48,accent,center=True)
    text(960,1002,"Balanced elemental stones with their own effects, furnishings and equipment.",44,INK,center=True)
    intro_save(0)

    # 3. Broad content range, using representative pieces rather than a second catalogue.
    intro_base("More than stones")
    intro_copy(64,280,"Multiple stone families, each with its own effects and mechanics.",1792,48,limit=390)
    categories=[
        ("Furnishings","KM_LumenVitaliteColumn"),
        ("Equipment","KM_AegiteShield"),
        ("Implants","KM_VitaliteImplant"),
        ("Prosthetics","KM_FerriteArm"),
        ("Rituals","KM_LumenstoneAltar"),
        ("Energy networks","KM_SunstoneVessel"),
    ]
    for index,(label,name) in enumerate(categories):
        left=28+(index%3)*630
        top=390+(index//3)*332
        card((left,top,left+604,top+308))
        text(left+302,top+26,label,44,GOLD,center=True)
        path=local_texture(name)
        if path.startswith("Things/Item/Health/"):
            gallery_implant(path,(left+202,top+109,200,144))
        else:
            intro_art(name,(left+102,top+98,400,180))
    intro_save(1)

    # 4. Configuration is an intentional answer to different mod lists.
    intro_base("Your colony. Your balance.")
    text(64,281,"One preset cannot suit every mod collection.",54,INK)
    text(64,362,"Start with vanilla-oriented defaults. Change the balance to suit your game.",42,MUTED)
    for left,title,lines in [
        (28,"Players",["Change effects and costs","Nerf or disable content","Choose your own rules"]),
        (974,"Collection authors",["Ship a custom balance preset","Use Auto Mod Config Continued","Set the rules for your modpack"]),
    ]:
        card((left,444,left+918,890))
        text(left+36,477,title,48,GOLD,title=True)
        for index,line in enumerate(lines):
            y=579+index*88
            bounds=draw.textbbox((0,0),line,font=font(43),anchor="lt")
            cy=y+(bounds[1]+bounds[3])/2
            x=left+46
            draw.polygon(((x,cy-9),(x+9,cy),(x,cy+9),(x-9,cy)),fill=GOLD)
            text(left+76,y,line,43,INK)
    text(64,937,"Don't want crystal freezers?",48,GOLD)
    text(64,1000,"Disable the coolers, or weaken their cooling.",42,INK)
    intro_save(2)

    # 5. Renewable ritual supply is explicitly DLC-gated; infusion is not.
    intro_base("Stone is your bottleneck")
    text(64,281,"Useful quantities take work.",62,GOLD)
    intro_copy(64,377,"Mine rare veins, find surface stones or pay traders. With Ideology, rituals provide a renewable supply of Lumenstone.",1792,44,limit=510)
    steps=[
        ("Gather","KM_Lumenstone","Every infused stone starts as raw Lumenstone."),
        ("Replenish","KM_LumenstoneAltar","With Ideology, altar rituals turn ordinary stone chunks into Lumenstone."),
        ("Research and infuse","KM_LumenCircle","Unlock new variants through research, then infuse Lumenstone with their reagents."),
    ]
    for index,(label,name,copy) in enumerate(steps):
        left=28+index*630
        card((left,532,left+604,1052))
        intro_copy(left+36,566,label,532,48,GOLD,limit=690)
        intro_art(name,(left+132,686,340,160))
        intro_copy(left+36,885,copy,532,36,limit=1036)
    intro_save(3)

    # 6. The payoff is the choice of what scarce material buys first.
    intro_base("Make each stone count")
    text(64,281,"A hospital, a farm, or a freezer?",62,GOLD)
    intro_copy(64,378,"Your first stones cannot do everything at once. Choose which family to pursue, then what to build or equip.",1792,46,limit=515)
    for index,(label,name,accent) in enumerate([
        ("Recovery","KM_VitaliteCasket",ROSE),
        ("Food production","KM_VerditePylon",GREEN),
        ("Food storage","KM_GlaciteCooler",BLUE),
    ]):
        left=28+index*630
        card((left,540,left+604,918))
        intro_art(name,(left+102,566,400,250))
        text(left+302,846,label,44,accent,center=True)
    intro_copy(64,962,"Let your colony's needs and available reagents set the order.",1792,44,limit=1060)
    intro_save(4)
    return INTRO_NAMES


def description_image():
    """A single tall image for the Workshop description column, not the gallery."""
    global canvas,draw,W
    W=1272
    canvas=Image.new("RGBA",(W,2760),"#10232f")
    draw=ImageDraw.Draw(canvas)
    text(636,36,"Lumen Stones",124,INK,title=True,center=True)
    text(636,166,"Stones that do things",50,GOLD,title=True,center=True)
    draw.rectangle((0,232,1271,237),fill=GOLD)
    for index,(stone,label,name,accent) in enumerate([
        ("Glacite","Freeze your food","KM_GlaciteCooler",BLUE),
        ("Vitalite","Help your wounded recover","KM_VitaliteCluster",ROSE),
        ("Verdite","Grow under crystal light","KM_VerditePylon",GREEN),
    ]):
        left=24+index*412
        card((left,266,left+400,842))
        text(left+200,298,stone,36,accent,title=True,center=True)
        intro_art(name,(left+40,380,320,250))
        for i,line in enumerate(label_lines(label,352,38)):
            text(left+200,692+i*48,line,38,accent,center=True)
    text(636,900,"Balanced elemental stones with their own effects,",40,INK,center=True)
    text(636,954,"furnishings, equipment, implants and prosthetics.",40,INK,center=True)

    card((24,1040,1248,1506))
    text(60,1074,"One base stone",48,GOLD,title=True)
    intro_art("KM_Lumenstone",(60,1180,170,180))
    intro_copy(276,1160,"Mine, find or buy Lumenstone. Research new stone families, then infuse it with reagents at a Lumen ritual circle.",912,36,limit=1360)
    intro_copy(276,1350,"With Ideology, rituals replenish Lumenstone and sacrifice unlocks Dark Stone.",912,36,limit=1484)

    card((24,1536,1248,2070))
    text(60,1572,"Balance you can shape",48,GOLD,title=True)
    for y,value in [
        (1660,"Scarce supply makes you choose which stones, buildings and equipment to make first."),
        (1760,"Adjust costs, effects and availability in Mod options."),
        (1860,"Nerf crystal freezers, or disable their coolers entirely."),
        (1960,"Collection authors can share balance presets through Auto Mod Config Continued."),
    ]:
        lines=label_lines(value,1104,34)
        bounds=draw.textbbox((0,0),lines[0],font=font(34),anchor="lt")
        cy=y+(bounds[1]+bounds[3])/2
        draw.polygon(((66,cy-8),(74,cy),(66,cy+8),(58,cy)),fill=GOLD)
        for i,line in enumerate(lines):
            text(94,y+i*45,line,34,INK)

    card((24,2100,624,2570))
    card((648,2100,1248,2570))
    text(60,2134,"Requirements",42,GOLD,title=True)
    intro_copy(60,2220,"Harmony required.",528,36,limit=2300)
    intro_copy(60,2292,"Royalty, Ideology and Anomaly add optional content.",528,34,limit=2410)
    intro_copy(60,2415,"Epochs - Pyrinth supported. No Combat Extended patch.",528,34,limit=2550)
    text(684,2134,"Save games",42,GOLD,title=True)
    intro_copy(684,2220,"Can be added mid-save.",528,36,limit=2300)
    intro_copy(684,2292,"Find stones through traders, new maps and Ideology rituals.",528,34,limit=2430)
    intro_copy(684,2435,"Removal deletes mod content, including implanted parts.",528,34,limit=2565)
    text(636,2610,"Effects use staggered updates. Fixture animation can be disabled.",29,MUTED,center=True)
    metadata=ET.parse(ROOT/"About/About.xml").getroot()
    version=metadata.findtext("modVersion")
    game=metadata.findtext("supportedVersions/li")
    text(636,2680,f"Version {version}  |  RimWorld {game}",34,INK,center=True)
    save("lumen-stones-description")


def description_wide_image():
    """A broader, shorter alternative for the inline description image."""
    global canvas,draw,W
    W=1600
    canvas=Image.new("RGBA",(W,2260),"#10232f")
    draw=ImageDraw.Draw(canvas)
    text(800,94,"Stones that do things",90,INK,title=True,center=True)
    draw.rectangle((0,254,1599,260),fill=GOLD)
    for index,(stone,label,name,accent) in enumerate([
        ("Glacite","Freeze your food","KM_GlaciteCooler",BLUE),
        ("Vitalite","Help your wounded recover","KM_VitaliteCluster",ROSE),
        ("Verdite","Grow under crystal light","KM_VerditePylon",GREEN),
    ]):
        left=24+index*524
        card((left,290,left+504,914))
        text(left+252,328,stone,44,accent,title=True,center=True)
        intro_art(name,(left+48,420,408,290))
        for index_line,line in enumerate(label_lines(label,456,44)):
            text(left+252,776+index_line*55,line,44,accent,center=True)
    text(800,968,"Balanced elemental stones with their own effects,",46,INK,center=True)
    text(800,1028,"furnishings, equipment, implants and prosthetics.",46,INK,center=True)

    card((24,1130,784,1810))
    card((812,1130,1576,1810))
    text(64,1170,"One base stone",52,GOLD,title=True)
    intro_art("KM_Lumenstone",(66,1410,220,190))
    text(176,1620,"Lumenstone",36,INK,center=True)
    text(360,1398,"Infusion",38,GOLD,center=True)
    draw.line(((304,1510),(452,1510)),fill=GOLD,width=5)
    draw.line(((452,1380),(452,1650)),fill=GOLD,width=5)
    for y in (1380,1650):
        draw.line(((452,y),(548,y)),fill=GOLD,width=5)
        draw.polygon(((548,y),(529,y-13),(529,y+13)),fill=GOLD)
    intro_art("KM_Glacite",(566,1290,176,160))
    text(654,1470,"Glacite",38,BLUE,center=True)
    intro_art("KM_Verdite",(566,1560,176,160))
    text(654,1740,"Verdite",38,GREEN,center=True)
    text(852,1170,"Your balance",52,GOLD,title=True)
    intro_art("KM_GlaciteCooler",(1044,1240,300,200))
    text(1194,1456,"Glacite cooler",38,BLUE,center=True)
    text(862,1530,"Temperature limit",38,INK)
    text(1434,1530,"4°C",38,BLUE)
    draw.rounded_rectangle((878,1596,1510,1608),radius=6,fill="#40545d")
    draw.rounded_rectangle((878,1596,1174,1608),radius=6,fill=BLUE)
    draw.ellipse((1154,1582,1194,1622),fill=INK,outline=BLUE,width=3)
    text(862,1661,"Enable coolers",38,INK)
    draw.rounded_rectangle((1374,1644,1526,1710),radius=33,fill=BLUE)
    text(1394,1663,"ON",30,"#10232f")
    draw.ellipse((1468,1652,1518,1702),fill=INK)
    text(1194,1750,"Supports autoconfig",34,MUTED,center=True)

    card((24,1842,784,2230))
    card((812,1842,1576,2230))
    text(64,1882,"Requirements",48,GOLD,title=True)
    intro_copy(64,1960,"Harmony required. DLC adds optional content.",680,40,limit=2072)
    intro_copy(64,2086,"Epochs - Pyrinth supported. No Combat Extended patch.",680,40,limit=2214)
    text(852,1882,"Save games",48,GOLD,title=True)
    intro_copy(852,1960,"Can be added mid-save. Find stones through traders, new maps and Ideology rituals.",680,39,limit=2126)
    intro_copy(852,2128,"Removal deletes mod content, including implanted parts.",680,36,limit=2226)
    save("lumen-stones-description-wide")


def dark_path_slide():
    intro_base("The dark path")
    text(1480,164,"Requires Ideology",32,ROSE)
    card((28,258,1892,566))
    for cx,name,label in [
        (290,"KM_Lumenstone","Lumenstone"),
        (960,"KM_SacrificialAltar","Sacrificial altar"),
        (1630,"KM_DarkStone","Dark Stone"),
    ]:
        intro_art(name,(cx-130,290,260,160))
        text(cx,472,label,38,INK,center=True)
    arrow(554,374,ROSE)
    arrow(1264,374,ROSE)
    text(960,530,"Research Warp sacrifice, then choose Crystallize Dark Stone and sacrifice a person.",32,MUTED,center=True)
    text(64,602,"The Dreadstone seed",50,ROSE,title=True)
    text(1250,620,"Research Dreadstone subjugation",28,MUTED)
    stages=[
        ("1. Shape","KM_DreadstoneSeedCrystal","Refine Dark Stone into Dreadstone. Shape 5 into a seed at the altar."),
        ("2. Hammer it in","KM_DreadstoneSeedCrystal","Hammer it into an adult's chest. The seed stays dormant."),
        ("3. Bind","KM_SacrificialAltar","Use Binding at the altar to start the seed growing."),
        ("4. Consume","KM_DarkStone","Over 4 days, the Dreadstone seed consumes the host, causing extreme pain and producing Dark Stone."),
    ]
    for i,(heading,name,copy) in enumerate(stages):
        left=28+i*470
        card((left,682,left+454,1000))
        text(left+227,706,heading,38,GOLD,center=True)
        intro_art(name,(left+147,752,160,80))
        intro_copy(left+24,838,copy,406,28,limit=994)
    text(960,1030,"There is no cure, and no way to relieve the pain.",28,MUTED,center=True)
    save("gallery-intro-07-the-dark-path")


def features_image():
    global canvas,draw,W
    W=1600
    canvas=Image.new("RGBA",(W,1820),"#10232f")
    draw=ImageDraw.Draw(canvas)
    text(800,60,"Features",100,INK,title=True,center=True)
    draw.rectangle((0,200,1599,206),fill=GOLD)
    for left,heading,accent,stones in [
        (24,"Light stones",GOLD,["Lumenstone","Glacite","Verdite"]),
        (812,"Dark stones",ROSE,["DarkStone","Bloodstone","Sanguinite"]),
    ]:
        card((left,236,left+760,850))
        text(left+380,274,heading,52,accent,title=True,center=True)
        for i,name in enumerate(stones):
            cx=left+140+i*240
            intro_art("KM_"+name,(cx-98,388,196,170))
            text(cx,584,"Dark Stone" if name=="DarkStone" else name,32,accent,center=True)
    text(404,676,"Eight core stones + Lunite",36,INK,center=True)
    text(404,738,"+ Animastone with Royalty",34,MUTED,center=True)
    text(404,788,"+ Pyrinth with Epochs - Pyrinth",32,MUTED,center=True)
    text(1192,676,"Five dark stones",40,INK,center=True)
    text(1192,738,"Requires Ideology",34,MUTED,center=True)
    text(1192,788,"Each with its own role and synergies",32,MUTED,center=True)

    card((24,878,784,1630))
    card((812,878,1572,1630))
    text(404,918,"Build your supply",48,GOLD,title=True,center=True)
    intro_art("KM_LumenstoneAltar",(214,1030,380,270))
    text(404,1342,"Mine and trade",42,INK,center=True)
    text(404,1410,"Renew stones through rituals",38,INK,center=True)
    text(404,1472,"Take the dark path through sacrifice",34,ROSE,center=True)
    text(404,1554,"Rituals and sacrifice require Ideology",30,MUTED,center=True)

    text(1192,918,"Energy networks",48,GOLD,title=True,center=True)
    intro_art("KM_SunstoneVessel",(882,1030,230,240))
    intro_art("KM_BloodstoneVessel",(1262,1030,230,240))
    text(1000,1310,"Solar",42,GOLD,center=True)
    text(1384,1310,"Warp",42,ROSE,center=True)
    text(1000,1374,"Sunstone",34,INK,center=True)
    text(1384,1374,"Bloodstone",34,INK,center=True)
    text(1192,1440,"Solar flares pause both networks",32,INK,center=True)
    text(1192,1494,"Lunite protects individual fixtures",32,INK,center=True)
    text(1192,1554,"Stored energy is still required",30,MUTED,center=True)
    text(800,1692,"Lore behind every stone, building and item.",42,INK,center=True)
    save("lumen-stones-features")


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
    "aegite": ["Aegite.xml", "AegiteShield.xml"],
    "ferrite": ["Ferrite.xml", "FerriteWarhammer.xml"],
    "sunstone": ["Sunstone.xml", "SunstoneAxe.xml"],
    "animastone": ["Animastone.xml", "AnimastoneFixtures.xml", "AnimastoneCirclet.xml", "AnimastoneStaff.xml"],
    "fireflies": ["FireflyJar.xml", "Columns.xml", "PyrinthColumn.xml"],
    "darkstone": ["SacrificialAltar.xml", "DarkStoneFixtures.xml"],
    "bloodstone": ["Bloodstone.xml"],
    "sanguinite": ["Sanguinite.xml"],
    "dreadstone": ["Dreadstone.xml"],
    "hollowstone": ["Hollowstone.xml", "WorkHusk.xml"],
}
def new_defence_slides():
    gallery_base("Lunite", "Moonlit Sunstone keeps a fixture working through solar flares", "#ccd9f5", 17)
    intro_art("KM_Lunite",(42,12,242,198))
    card((28,260,930,1052))
    card((958,260,1892,1052))
    text(64,290,"Convert at night",48,GOLD,title=True)
    intro_art("KM_Sunstone",(80,390,280,210))
    arrow(400,495)
    intro_art("KM_Lunite",(560,390,280,210))
    intro_copy(64,650,"25 Sunstone becomes 25 Lunite. Start between 20:00 and 04:00 at a ritual circle or altar.",820,40,limit=830)
    intro_copy(64,840,"With Ideology, use a ritual. Without it, a colonist works for four hours at the circle. Sunstone is spent only on success.",820,34,limit=1020)
    text(994,290,"Protect one fixture",48,GOLD,title=True)
    intro_art("KM_LuniteStabilizer",(1210,380,400,250))
    intro_copy(994,650,"One stabilizer links to one Solar or Warp fixture within four tiles. The fixture keeps spending stored energy during interruptions.",850,38,limit=850)
    intro_copy(994,860,"Collectors and arrays cannot be protected. Passive effects and worn gear ignore interruptions.",850,34,limit=1020)
    save("gallery-17-lunite")

    gallery_base("Aegite bastion", "Personal protection and a Solar-powered shield dome", PALETTES["aegite"], 18)
    intro_art("KM_Aegite",(42,12,242,198))
    card((28,260,930,1052))
    card((958,260,1892,1052))
    text(64,290,"Choose your shield",48,GOLD,title=True)
    intro_art("KM_AegiteShield",(100,390,300,210))
    intro_art("KM_AegiteShieldCharm",(540,390,300,210))
    intro_copy(64,650,"Shield: +25 percentage points of sharp and blunt armour, plus immunity to catching fire.",820,38,limit=815)
    intro_copy(64,830,"Charm: blocks ranged shots but prevents firing out. EMP breaks it. Both use the belt slot, need no Solar energy and ignore interruptions.",820,34,limit=1020)
    text(994,290,"Shield projector",48,GOLD,title=True)
    intro_art("KM_AegiteShieldProjector",(1190,380,440,250))
    intro_copy(994,650,"A 10-tile dome stops hostile shots from outside, mortars and bombardments. Colonists can shoot out.",850,37,limit=810)
    intro_copy(994,820,"Crushes hostile drop pods and tunneling infestations at a cost in shield health. Needs Solar energy. A broken dome reboots for a day; EMP disarms it briefly.",850,33,limit=1020)
    save("gallery-18-aegite-bastion")


SHARED = {"Resources.xml", "NewEquipment.xml", "StoneImplants.xml", "DarkImplants.xml",
          "VerditeChew.xml", "SunstoneFlare.xml", "DreadstoneSeed.xml", "PyrinthImplant.xml", "LuniteAndBastion.xml"}
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--family", choices=[*GROUPS, "overview", "intro", "description", "description-wide", "features", "dark-path", "all"], default="sanguinite",
                    help="Render one template only; use all after the layout is approved.")
parser.add_argument("--style", choices=STYLES, default="plain")
parser.add_argument("--output", type=Path, default=OUT)
args = parser.parse_args()
OUT = args.output
OUT.mkdir(parents=True,exist_ok=True)
for group, files in GROUPS.items():
    if args.family not in (group, "all"):
        continue
    items = []
    for folder in (ROOT / "Defs", ROOT / "Integrations"):
        for xml in folder.rglob("*.xml"):
            if xml.name not in files and xml.name not in SHARED:
                continue
            for node in ET.parse(xml).getroot():
                name, label = node.findtext("defName"), node.findtext("label")
                if node.tag != "ThingDef" or not name or not label or "Mote_" in name or name == "KM_AegiteShieldProjector":
                    continue
                if xml.name in SHARED and group not in name.lower() and not (group == "bloodstone" and name == "KM_TwinstoneCell") and not (group == "fireflies" and name == "KM_PyrinthImplant"):
                    continue
                if node.find("projectile") is not None or field(name, "category") == "Projectile":
                    continue
                tex = field(name, "graphicData/texPath")
                paths = [tex, tex + "_north", tex + "_south", tex + "_c", tex + "/" + tex.split("/")[-1] + "_a"] if tex else []
                path = next((v for v in paths if (ROOT / "Textures" / (v + ".png")).is_file()), None)
                if not path:
                    continue
                sentences = re.split(r"(?<=\.)\s+", node.findtext("description", "").replace("\\n", " "))
                description = next((v for v in sentences if re.search(r"\b(while|within|contributes|repairs|stores|gathers|absorbs|supports|infused|refined)\b", v, re.I)), sentences[0]).strip()
                body = textwrap.shorten(description, width=105, placeholder="... ") if description else label.capitalize() + "."
                items.append((name, path, label[0].upper() + label[1:], body, ""))
    if items:
        catalog(items, ROSE if group in ("vitalite", "sanguinite") else BLUE, group)
        print(group, len(items))


if args.family in ("all", "overview"):
    overview()
if args.family in ("intro", "all"):
    intro_slides()
    dark_path_slide()
    family_order = [f"gallery-{index+2:02}-{group}.png" for index,group in enumerate(GROUPS)]
    family_order.insert(family_order.index("gallery-08-ferrite.png"), "gallery-18-aegite-bastion.png")
    family_order.insert(family_order.index("gallery-10-animastone.png"), "gallery-17-lunite.png")
    (OUT / "gallery-order.txt").write_text("\n".join(INTRO_NAMES + ["gallery-01-stone-lineage.png"] + family_order) + "\n", encoding="utf-8")
    new_defence_slides()

if args.family in ("description", "all"):
    description_image()

if args.family in ("description-wide", "all"):
    description_wide_image()

if args.family in ("features", "all"):
    features_image()

if args.family == "dark-path":
    dark_path_slide()
