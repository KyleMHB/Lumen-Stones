"""Crystal light: looping banded light patterns clipped to a crystal-only mask, for the stone-animations spec.

Model only. The frames are written under work/ and the preview is a CPU composite, not an in-game result.
The crystal region is the sprite's opaque pixels minus the construction-tint mask (the housing) and everything
below an optional cut line (gem rows). The dark outline is weighted out by luminance. That weighting is a
preview approximation of visual weight, not material ownership: a production mask needs recorded polygons.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
FPS = 6
LEVELS = 4

OBJECTS = {
    "vitalite-pylon": {"colour": "Textures/Things/Building/Vitalite/Pylon.png", "kind": "bands", "tint": (255, 150, 200), "cut": None, "exclude": [(40, 212, 215, 282)]},
    "sunstone-obelisk": {"colour": "Textures/Things/Building/Misc/SunstoneObelisk.png", "kind": "flow", "tint": (255, 205, 90), "cut": 395},
    "bloodstone-obelisk": {"colour": "Textures/Things/Building/Dark/BloodstoneObelisk.png", "kind": "flow", "tint": (255, 70, 200), "cut": 415},
}


def maskPath(colourPath):
    """Project convention: Pylon.png has Pylon_m.png."""
    return colourPath.with_name(colourPath.stem + "_m.png")


def loadSprite(colourPath):
    colour = np.array(Image.open(colourPath).convert("RGBA"), dtype=np.float32) / 255
    tint = np.array(Image.open(maskPath(colourPath)).convert("RGBA"), dtype=np.float32) / 255
    return colour, tint


def erode(weight, radius):
    """Minimum filter over a square window, enough to pull the effect off the outline."""
    out = weight
    for _ in range(radius):
        padded = np.pad(out, 1, mode="edge")
        stack = [padded[dy:dy + out.shape[0], dx:dx + out.shape[1]] for dy in range(3) for dx in range(3)]
        out = np.minimum.reduce(stack)
    return out


def crystalWeight(colour, tint, cut=None, radius=2, exclude=()):
    """0..1 weight of the crystal interior: opaque, not construction tint, above the cut, away from the outline."""
    alpha = colour[..., 3]
    housing = tint[..., 0] * tint[..., 3]
    weight = np.clip(alpha - housing, 0, 1)
    if cut is not None:
        weight[cut:, :] = 0
    for left, top, right, bottom in exclude:
        weight[top:bottom, left:right] = 0
    weight = erode(weight, radius)
    luminance = colour[..., :3] @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    ink = np.clip((luminance - 0.12) / 0.2, 0, 1)
    return weight * ink


def quantise(values, levels=LEVELS):
    """Hard bands in keeping with the cel-shaded sprites: 0 is dark, 1 is brightest."""
    return np.floor(np.clip(values, 0, 0.9999) * levels) / (levels - 1)


def pattern(kind, shape, phase, seed=0):
    """Looping light pattern in 0..1. phase is in [0, 1) and wraps exactly, so frame 0 follows the last frame.

    Coordinates are pixel positions plus a seed offset, so fixtures at different seeds are out of step in space too.
    """
    height, width = shape
    y, x = np.mgrid[0:height, 0:width].astype(np.float32)
    x, y = x + seed * 37.0, y + seed * 53.0
    tau = 2 * np.pi
    if kind == "bands":
        wavelength = 140.0
        along = (x * 0.45 + y * 0.9) / wavelength
        value = 0.5 + 0.5 * np.cos(tau * (along - phase))
        value = value ** 1.8
    elif kind == "flow":
        wavelength = 170.0
        along = y / wavelength
        # Two vertical waves at related speeds make an uneven, flowing look and still loop exactly.
        value = 0.78 * (0.5 + 0.5 * np.cos(tau * (along + phase))) + 0.22 * (0.5 + 0.5 * np.cos(tau * (2.0 * along + x / 300.0 + 2 * phase)))
        value = value ** 1.6
    else:
        raise ValueError(f"Unknown pattern kind: {kind}")
    return quantise(value)


def frames(kind, weight, count, seed=0, reverse=False):
    """Frame alpha arrays, light above a threshold only so the crystal keeps dark gaps between bands."""
    out = []
    for index in range(count):
        phase = index / count
        if reverse:
            phase = -phase
        light = pattern(kind, weight.shape, phase, seed)
        light = np.clip((light - 0.34) / 0.66, 0, 1)
        out.append(light * weight)
    return out


def frameImage(alpha):
    """White RGB with the pattern as alpha: the game tints it with the stone colour."""
    rgba = np.zeros(alpha.shape + (4,), dtype=np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = np.round(alpha * 255).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def previewFrame(colour, alpha, tint, background):
    base = np.empty(colour.shape[:2] + (3,), dtype=np.float32)
    base[:] = background
    sprite = colour[..., :3] * colour[..., 3:4] + base * (1 - colour[..., 3:4])
    glow = alpha[..., None] * np.array(tint, dtype=np.float32) / 255 * 0.85
    result = np.clip(sprite + glow, 0, 1)
    return Image.fromarray((result * 255).astype(np.uint8), "RGB")


def boundingBox(alpha):
    columns, rows = np.where(alpha.max(axis=0) > 0)[0], np.where(alpha.max(axis=1) > 0)[0]
    return int(columns.min()), int(rows.min()), int(columns.max()) + 1, int(rows.max()) + 1


def digest(images):
    sha = hashlib.sha256()
    for image in images:
        sha.update(image.tobytes())
    return sha.hexdigest()


def build(name, count, outDir, seed=0, reverse=False):
    spec = OBJECTS[name]
    colour, tint = loadSprite(ROOT / spec["colour"])
    weight = crystalWeight(colour, tint, spec["cut"], exclude=spec.get("exclude", ()))
    alphas = frames(spec["kind"], weight, count, seed, reverse)
    box = boundingBox(weight)
    directory = Path(outDir) / name / f"{count}f"
    directory.mkdir(parents=True, exist_ok=True)
    images = []
    for index, alpha in enumerate(alphas):
        image = frameImage(alpha)
        image.save(directory / f"frame{index:02d}.png")
        images.append(image)
    cropArea = (box[2] - box[0]) * (box[3] - box[1])
    full = colour.shape[0] * colour.shape[1]
    # DXT5 stores one byte per pixel; RGBA32 stores four. The game compresses textures in memory.
    report = {
        "object": name, "kind": spec["kind"], "frames": count, "fps": FPS, "loopSeconds": count / FPS,
        "canvas": [colour.shape[1], colour.shape[0]], "crystalBox": list(box),
        "crystalPixels": int((weight > 0.5).sum()), "sha256": digest(images),
        "memoryKiBFullCanvasDXT5": round(full * count / 1024), "memoryKiBCroppedDXT5": round(cropArea * count / 1024),
    }
    (directory / "report.json").write_text(json.dumps(report, indent=2))
    Image.fromarray((weight * 255).astype(np.uint8), "L").save(directory / "crystal-mask.png")
    return colour, tint, alphas, report, directory


def preview(name, colour, tint, alphas, directory):
    """Looping GIFs on dark and light backgrounds, plus a contact sheet of the frames."""
    spec = OBJECTS[name]
    backgrounds = {"dark": (0.12, 0.13, 0.15), "light": (0.78, 0.74, 0.66)}
    for label, background in backgrounds.items():
        shots = [previewFrame(colour, alpha, spec["tint"], background) for alpha in alphas]
        shots[0].save(directory / f"preview-{label}.gif", save_all=True, append_images=shots[1:],
                      duration=round(1000 / FPS), loop=0, optimize=False)
    sheet = [previewFrame(colour, alpha, spec["tint"], backgrounds["dark"]) for alpha in alphas]
    width, height = sheet[0].size
    columns = min(6, len(sheet))
    rows = (len(sheet) + columns - 1) // columns
    contact = Image.new("RGB", (columns * width, rows * height))
    for index, shot in enumerate(sheet):
        contact.paste(shot, ((index % columns) * width, (index // columns) * height))
    contact.save(directory / "contact-sheet.png")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("names", nargs="*", default=list(OBJECTS))
    parser.add_argument("--frames", type=int, nargs="+", default=[8, 12])
    parser.add_argument("--out", default=str(ROOT / "work" / "crystal-light"))
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument("--reverse", action="store_true", help="Flow the other way, as when a battery is drawing.")
    args = parser.parse_args()
    for name in args.names:
        for count in args.frames:
            colour, tint, alphas, report, directory = build(name, count, args.out, args.seed, args.reverse)
            preview(name, colour, tint, alphas, directory)
            print(json.dumps(report))


if __name__ == "__main__":
    main()
