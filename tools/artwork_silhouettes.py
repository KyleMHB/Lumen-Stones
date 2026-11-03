"""Flat single-colour silhouettes of one fixture type across every stone: the sameness check (rule 10)."""
from pathlib import Path

from PIL import Image, ImageDraw

from artwork_measure import REFERENCES, targets

CELL = (150, 170)
FIT = 128
FIXTURE_SUFFIXES = {" column": "Column", " Pylon": "Pylon", " Cluster": "Cluster", " WallLamp": "WallLamp",
                    " Lamp": "Lamp", " stack of three": "Stack"}
INK, PAPER = (32, 36, 44, 255), (232, 232, 234, 255)


def fixtureType(name):
    for suffix, kind in FIXTURE_SUFFIXES.items():
        if name.endswith(suffix):
            return kind
    return "Equipment"


def silhouette(path):
    """Opaque pixels in one colour, cropped to the sprite and fitted into the cell."""
    with Image.open(path) as source:
        alpha = source.convert("RGBA").getchannel("A").point(lambda a: 255 if a > 128 else 0)
    box = alpha.getbbox()
    if box is None:
        raise ValueError(f"Empty sprite: {path}")
    alpha = alpha.crop(box)
    scale = FIT / max(alpha.size)
    alpha = alpha.resize((max(1, round(alpha.width * scale)), max(1, round(alpha.height * scale))),
                         Image.Resampling.BOX).point(lambda a: 255 if a > 127 else 0)
    flat = Image.new("RGBA", alpha.size, INK)
    flat.putalpha(alpha)
    return flat


def groups(root):
    result = {}
    for name, (relative, _) in targets(root).items():
        if name in REFERENCES:
            continue
        result.setdefault(fixtureType(name), []).append((name, Path(root) / "Textures" / relative))
    return result


def render(root, output):
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    written = []
    for kind, members in sorted(groups(root).items()):
        columns = min(len(members), 5)
        rows = -(-len(members) // columns)
        sheet = Image.new("RGBA", (CELL[0] * columns, CELL[1] * rows), PAPER)
        draw = ImageDraw.Draw(sheet)
        for index, (name, path) in enumerate(members):
            x, y = CELL[0] * (index % columns), CELL[1] * (index // columns)
            shape = silhouette(path)
            sheet.alpha_composite(shape, (x + (CELL[0] - shape.width) // 2, y + 4 + (FIT - shape.height) // 2))
            draw.text((x + 6, y + FIT + 12), name, fill=INK)
        target = output / f"silhouettes-{kind}.png"
        sheet.convert("RGB").save(target)
        written.append(target)
    return written
