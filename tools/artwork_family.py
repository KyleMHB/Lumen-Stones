"""Measured family-rule checks (rules 2, 4 and 5 of the art guide); silhouette, motif and tier calls stay human."""
import itertools
import json
from pathlib import Path

from artwork_measure import MIN_FEATURE, measure, targets

CONFIG = "artwork/family-checks.json"
REFERENCE_STONE = "Serenite"
HUE_STACKS = "Things/Item/Resource/%s/%s_c.png"


def hueGap(a, b):
    gap = abs(a - b) % 360
    return min(gap, 360 - gap)


def measureFamily(root):
    """Outline weight and islands of every covered sprite, and one hue per stone from its resource stack."""
    sprites = {name: measure(Path(root) / "Textures" / relative, facets=False)
               for name, (relative, _) in targets(root).items()}
    hues = {}
    for stone in {name.split()[0] for name in sprites if name.endswith("stack of three")}:
        hues[stone] = sprites[stone + " stack of three"]["hue"]
    return sprites, hues


def fixtureOf(name):
    stone, _, rest = name.partition(" ")
    return rest


def familyChecks(root):
    config = json.loads((Path(root) / CONFIG).read_text(encoding="utf-8"))
    exceptions = dict(config["exceptions"])
    errors = []

    def fail(key, message):
        if key not in exceptions:
            errors.append(f"{key}: {message}")

    sprites, hues = measureFamily(root)
    tolerance = config["outlineTolerancePx"]
    for name, found in sprites.items():
        if name.startswith(("style:", "floor:", "ceiling:")):
            continue
        reference = sprites.get(f"{REFERENCE_STONE} {fixtureOf(name)}")
        if reference and name.split()[0] != REFERENCE_STONE and abs(found["outlinePx"] - reference["outlinePx"]) > tolerance:
            fail(f"outline:{name}", f"outline {found['outlinePx']}px differs from {REFERENCE_STONE} "
                 f"{fixtureOf(name)} {reference['outlinePx']}px by more than {tolerance}px")
        if found["specks"]:
            fail(f"feature:{name}", f"{found['specks']} opaque pieces under {MIN_FEATURE}px at the 128px export")
    chromatic = {stone: hue for stone, hue in hues.items() if hue is not None}
    for (a, ha), (b, hb) in itertools.combinations(sorted(chromatic.items()), 2):
        if hueGap(ha, hb) < config["minHueGapDegrees"]:
            fail(f"hue:{a}/{b}", f"hues {ha} and {hb} are {hueGap(ha, hb):.1f} degrees apart, "
                 f"under {config['minHueGapDegrees']}")
    return errors, {"hues": hues, "sprites": len(sprites)}
