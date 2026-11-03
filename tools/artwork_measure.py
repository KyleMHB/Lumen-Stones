"""Measure outline weight, flat facets and tonal values of a sprite at the 128px export size."""
import json
from pathlib import Path

import numpy as np
from PIL import Image

EXPORT = 128
FACET_LEVELS = 8      # colour quantisation per channel when grouping flat facets
FACET_MIN_AREA = 16   # smaller regions are noise at 128px
MAX_DEPTH = 12
MIN_FEATURE = 8       # opaque pieces smaller than this many pixels at 128px vanish at 1x


def luminance(rgb):
    return rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722


def normalised(path):
    """Fit the sprite's canvas into 128px, resizing channels separately so colour is not premultiplied."""
    with Image.open(path) as source:
        image = source.convert("RGBA")
    scale = EXPORT / max(image.size)
    if scale != 1:
        size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
        image = Image.merge("RGBA", [ch.resize(size, Image.Resampling.BOX) for ch in image.split()])
    return np.asarray(image).astype(float)


def depths(inside):
    """Steps from the sprite edge for each opaque pixel (1 = outermost ring), 4-neighbour erosion."""
    result = np.zeros(inside.shape, int)
    current = inside.copy()
    for depth in range(1, MAX_DEPTH + 1):
        result[current] = depth
        padded = np.pad(current, 1)
        current = (current & padded[:-2, 1:-1] & padded[2:, 1:-1] & padded[1:-1, :-2] & padded[1:-1, 2:])
        if not current.any():
            break
    result[current] = MAX_DEPTH + 1
    return result


def regions(codes, valid, minimum=FACET_MIN_AREA):
    """Areas of 4-connected same-code regions of at least `minimum` pixels."""
    seen = np.zeros(codes.shape, bool)
    areas = []
    height, width = codes.shape
    for y in range(height):
        for x in range(width):
            if seen[y, x] or not valid[y, x]:
                continue
            code, stack, area = codes[y, x], [(y, x)], 0
            seen[y, x] = True
            while stack:
                cy, cx = stack.pop()
                area += 1
                for ny, nx in ((cy + 1, cx), (cy - 1, cx), (cy, cx + 1), (cy, cx - 1)):
                    if 0 <= ny < height and 0 <= nx < width and valid[ny, nx] and not seen[ny, nx] \
                            and codes[ny, nx] == code:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
            if area >= minimum:
                areas.append(area)
    return areas


def dominantHue(rgb, inside):
    """Saturation-weighted circular mean hue in degrees, with the mean saturation; None for achromatic art."""
    scaled = rgb[inside] / 255
    high, low = scaled.max(axis=1), scaled.min(axis=1)
    saturation = np.where(high > 0, (high - low) / np.where(high > 0, high, 1), 0)
    chroma = high - low
    safe = np.where(chroma > 0, chroma, 1)
    red, green, blue = scaled[:, 0], scaled[:, 1], scaled[:, 2]
    hue = np.where(high == red, ((green - blue) / safe) % 6,
                   np.where(high == green, (blue - red) / safe + 2, (red - green) / safe + 4)) * 60
    weight = saturation * high * (saturation > 0.2)
    if weight.sum() == 0:
        return None
    angle = np.radians(hue)
    return round(float(np.degrees(np.arctan2((weight * np.sin(angle)).sum(), (weight * np.cos(angle)).sum())) % 360), 1)


def measure(path, facets=True):
    pixels = normalised(path)
    inside = pixels[..., 3] > 128
    if not inside.any():
        raise ValueError(f"Empty sprite: {path}")
    rgb = pixels[..., :3]
    lum = luminance(rgb)
    depth = depths(inside)
    interior = lum[depth > 6]
    reference = float(np.median(interior if interior.size else lum[inside]))
    profile = [round(float(lum[depth == d].mean()), 1) for d in range(1, 7) if (depth == d).any()]
    # Outline weight: how many ring-widths of near-black the edge carries, counting a half-dark ring as half.
    thickness = 0.0
    for mean in profile:
        darkness = max(0.0, 1 - mean / reference)
        if darkness < 0.15:
            break
        thickness += darkness
    if not facets:
        return {"outlinePx": round(thickness, 1), "hue": dominantHue(rgb, inside),
                "specks": sum(a < MIN_FEATURE for a in regions(np.zeros(inside.shape, int), inside, 1))}
    body = depth > max(round(thickness), 1)
    codes = (rgb // (256 // FACET_LEVELS)).astype(int)
    codes = codes[..., 0] * FACET_LEVELS ** 2 + codes[..., 1] * FACET_LEVELS + codes[..., 2]
    areas = regions(codes, body)
    values = lum[inside]
    colours, counts = np.unique(codes[body], return_counts=True)
    top = [f"#{int(c // FACET_LEVELS ** 2 * 32 + 16):02x}{int(c // FACET_LEVELS % FACET_LEVELS * 32 + 16):02x}"
           f"{int(c % FACET_LEVELS * 32 + 16):02x}" for c in colours[np.argsort(-counts)][:5]]
    return {"outlinePx": round(thickness, 1), "outlineProfile": profile, "interiorLuminance": round(reference, 1),
            "facets": len(areas), "smallestFacetPx": min(areas) if areas else 0,
            "luminanceP5P50P95": [round(float(np.percentile(values, p)), 1) for p in (5, 50, 95)],
            "topColours": top, "coveragePx": int(inside.sum())}


STONES = ("Aegite", "Ferrite", "Glacite", "Lumenstone", "Serenite", "Verdite", "Vitalite", "DarkStone",
          "Bloodstone", "Sunstone")
REFERENCES = {
    "style: Serenite column": ("Lumenstone/SereniteColumn.png", "reference"),
    "style: Serenite pylon": ("Things/Building/Serenite/Pylon.png", "reference"),
    "floor: Verdite column (protected)": ("Lumenstone/VerditeColumn.png", "reference"),
    "ceiling: Bloodstone vessel": ("Things/Building/Dark/BloodstoneVessel.png", "reference"),
    "ceiling: Sunstone vessel": ("Things/Building/Misc/SunstoneVessel.png", "reference"),
}


EXTRAS = (
    ("Glacite Lamp", "Things/Building/Misc/GlaciteLamp.png", "floor"),
    ("Glacite Pylon", "Things/Building/Misc/GlacitePylon/GlacitePylon_south.png", "mid"),
    ("Glacite Cooler", "Things/Building/Misc/GlaciteCooler.png", "mid"),
    ("Sunstone Obelisk", "Things/Building/Misc/SunstoneObelisk.png", "ceiling"),
    ("Sunstone Collector", "Things/Building/Misc/SunstoneCollector.png", "ceiling"),
)


def targets(root):
    """Every sprite the family rules cover, with the tier its role expects."""
    textures = Path(root) / "Textures"
    found = {name: (path, tier) for name, (path, tier) in REFERENCES.items() if (textures / path).exists()}
    for stone in STONES:
        column = "Lumenstone/%sColumn.png" % stone
        if (textures / column).exists():
            found["%s column" % stone] = (column, "mid")
        for fixture, tier in (("Pylon", "mid"), ("Cluster", "floor"), ("Lamp", "floor"), ("WallLamp", "floor")):
            relative = "Things/Building/%s/%s.png" % (stone, fixture)
            if (textures / relative).exists():
                found["%s %s" % (stone, fixture)] = (relative, tier)
        stack = "Things/Item/Resource/%s/%s_c.png" % (stone, stone)
        if (textures / stack).exists():
            found["%s stack of three" % stone] = (stack, "floor")
    for name, relative, tier in EXTRAS:
        if (textures / relative).exists():
            found[name] = (relative, tier)
    for weapon in sorted((textures / "Things/Item/Equipment").rglob("*.png")):
        found["%s" % weapon.stem] = (weapon.relative_to(textures).as_posix(), "ceiling")
    return found


FLOOR_SHARE = 0.75  # a piece needs this share of its reference's facets to count as meeting the tier


def floorFacets(sprites, name, tier):
    """Facets a piece needs: Serenite's same-type piece for floor and mid, the vessels for the ceiling."""
    if tier == "ceiling":
        vessels = [sprites[key]["facets"] for key in REFERENCES if key.startswith("ceiling:")]
        return round(min(vessels) * FLOOR_SHARE)
    reference = sprites.get("Serenite " + name.partition(" ")[2])
    return round(reference["facets"] * FLOOR_SHARE) if reference else None


def measureAll(root):
    result = {}
    for name, (relative, tier) in targets(root).items():
        entry = measure(Path(root) / "Textures" / relative)
        entry.update(path=relative, tier=tier,
                     facetsPerKiloPixel=round(entry["facets"] * 1000 / entry["coveragePx"], 1))
        result[name] = entry
    for name, entry in result.items():
        entry["floorFacets"] = floorFacets(result, name, entry["tier"]) if entry["tier"] != "reference" else None
    return result


if __name__ == "__main__":
    import sys
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1]
    output = root / "artwork/style-measurements.json"
    output.write_text(json.dumps({"method": __doc__, "exportPx": EXPORT, "facetLevels": FACET_LEVELS,
                                  "facetMinAreaPx": FACET_MIN_AREA, "sprites": measureAll(root)}, indent=1) + "\n")
    print("Wrote", output)
