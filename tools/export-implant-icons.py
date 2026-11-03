"""Export the native Bionic Icons derivatives and small command symbols for ticket 08."""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "artwork/design-sources/stone-implants"


def symbol(shapes, color, size=64):
    image = Image.new("RGBA", (size * 4, size * 4))
    draw = ImageDraw.Draw(image)
    for shape in shapes:
        points = [tuple(v * 4 for v in point) for point in shape["points"]]
        fill = tuple(color) + (shape.get("alpha", 255),)
        if shape["type"] == "polygon":
            draw.polygon(points, fill=fill)
        else:
            draw.line(points, fill=fill, width=shape.get("width", 2) * 4, joint="curve")
    return image.resize((size, size), Image.Resampling.LANCZOS)


def export(output):
    recipe = json.loads((SOURCE / "icons.json").read_text())
    box = Image.open(SOURCE / recipe["boxSource"]).convert("RGBA")
    shardSource = Image.open(SOURCE / recipe["shard"]["source"]).convert("RGBA")
    shardSource = shardSource.crop(recipe["shard"]["crop"])
    shardSource = shardSource.rotate(recipe["shard"]["rotation"],
                                     Image.Resampling.BICUBIC, expand=True)
    shardSource = shardSource.crop(shardSource.getchannel("A").getbbox())
    shardSource.thumbnail(tuple(recipe["shard"]["fit"]), Image.Resampling.LANCZOS)
    shard = Image.new("RGBA", (64, 64))
    shard.alpha_composite(shardSource, (32 - shardSource.width // 2, recipe["shard"]["top"]))
    glyphs = {"Shard": shard.getchannel("A")}
    for name in ("Shoulder", "Leg", "Heart", "Eye", "Jaw"):
        # Bionic Icons stores the body-part drawing in the mask's green channel.
        glyphs[name] = Image.open(SOURCE / f"upstream/{name}.png").getchannel("G")
    assets = []

    def save(path, image, **metadata):
        target = output / (path + ".png")
        target.parent.mkdir(parents=True, exist_ok=True)
        image.save(target)
        assets.append({"texture": path, "canvas": [64, 64],
                       "sha256": hashlib.sha256(target.read_bytes()).hexdigest(), **metadata})

    for name, entry in recipe["parts"].items():
        base = recipe["boxes"][entry["side"]]
        core = recipe["cores"][entry["stone"]]
        pixels = []
        for index, (r, g, b, a) in enumerate(box.get_flattened_data()):
            x, y = index % 64, index // 64
            strength = (0 if entry["glyph"] == "Shard" else
                        glyphs[entry["glyph"]].getpixel((x, y)) / 255)
            # Remap the actual Ultratech box's neutral and cyan channels.
            # Its original band, glow falloff, shading and alpha stay in place.
            chroma = max(g, b) - r
            color = [(neutral * r + glow * chroma) / 255
                     for neutral, glow in zip(base, core)]
            glyphColor = (205, 196, 213) if entry["side"] == "dark" else [v * .24 for v in color]
            pixels.append(tuple(round(v * (1 - strength) + g * strength)
                                for v, g in zip(color, glyphColor)) + (a,))
        item = Image.new("RGBA", (64, 64))
        item.putdata(pixels)
        if entry["glyph"] == "Shard":
            item.alpha_composite(shard)
        save("Things/Item/Health/LumenStones/" + name, item,
             consumer={"type": "ThingDef", "defName": "KM_" + name,
                       "field": "graphicData/texPath"})
    for name, glyph in glyphs.items():
        image = Image.new("RGBA", (64, 64), (230, 230, 230, 0))
        image.putalpha(glyph)
        if name == "Shard":
            image = shard
        save("UI/Health/LumenStones/" + name, image, role="health glyph; consumer pending")
    for name in ("Attunement", "Binding", "AttunementSickness"):
        color = recipe["commandColors"][name]
        image = symbol(recipe["symbols"][name], color)
        save("UI/Icons/" + name, image, role="ritual or sickness icon; consumer pending")
    return assets


def preview(output, destination, assets):
    sheet = Image.new("RGB", (720, 90 + 130 * ((len(assets) + 5) // 6)), "#36383e")
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), "Stone implants: CANDIDATES - design and in-game review pending", fill="white")
    draw.text((12, 28), "64px icons and 32px previews; fixed colours; Twinstone cell excluded", fill="white")
    for index, asset in enumerate(assets):
        x, y = (index % 6) * 120, 62 + (index // 6) * 130
        image = Image.open(output / (asset["texture"] + ".png")).convert("RGBA")
        sheet.paste(image, (x + 4, y), image)
        small = image.resize((32, 32), Image.Resampling.LANCZOS)
        sheet.paste(small, (x + 77, y + 16), small)
        name = asset["texture"].rsplit("/", 1)[1]
        for part in ("Implant", "Sickness", "Heart", "Arm", "Leg", "Eye", "Jaw"):
            name = name.replace(part, "\n" + part)
        draw.multiline_text((x + 4, y + 66), name, fill="white")
    destination.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(destination)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=SOURCE / "candidate/Textures")
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()
    assets = export(args.output)
    if args.output.resolve() == (SOURCE / "candidate/Textures").resolve():
        manifestPath = SOURCE / "assets.json"
        manifest = json.loads(manifestPath.read_text()) if manifestPath.exists() else {}
        manifest.update({"schema": 1, "status": "candidate; design and in-game review pending",
                         "recipe": (SOURCE / "icons.json").relative_to(ROOT).as_posix(),
                         "recipeSha256": hashlib.sha256((SOURCE / "icons.json").read_bytes()).hexdigest(),
                         "assets": assets})
        manifestPath.write_text(json.dumps(manifest, indent=2) + "\n")
    if args.preview:
        preview(args.output, args.preview, assets)
    print(f"Exported {len(assets)} icons to {args.output}")
