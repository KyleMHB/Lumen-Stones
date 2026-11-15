"""Add an outside stroke to a sprite's exterior line (docs/rimworld-art-guide.md#outline-layer).

Run on every new candidate, and on an approved piece only when the user names it. The stroke is black added
under the export, outside the art's own drawn silhouette, so the art is never changed: its fill, its drawn
line and its contour all stay as drawn. The stroke's width is the aim less the visible line's median width
(`band`), so a line drawn a little thin comes up to the aim and keeps its drawn shape and its variation.

How: the export's alpha is enlarged SUPER times (bicubic) and cut at half coverage, which finds the drawn
edge to a fraction of a pixel; everything within the stroke width of it is covered there, then reduced back,
so the new edge is antialiased and follows the drawn curve. A spike's tip rounds only by the stroke width.

What it leaves alone: a line already within AS_DRAWN of the aim, and a line wider than the aim (nothing is
trimmed). With --max-add, a line too far under the aim, or an edge that is mostly not black (black outside a
coloured keyline is a double line), is reported for a redo instead. A crop that cuts through the art leaves
its open edge without a stroke.

This replaced a layer that rebuilt the whole line from the body (2026-11-15): measured from the inner edge,
it copied that edge's wobble outside, rounded spikes, thinned a smooth rim and carved trimmed borders unevenly.

`outline-mask.png` is the stroke as opaque red coverage at canvas size, editable in the mask editor beside
`outline-preview.png`; rerun with --from-mask to rebuild from the edit. Everything is written under --output,
which must be in work/; promotion to Textures/ is a separate, user-approved step.
"""
import argparse
import importlib.util
import json
import math
import os
import shutil
from pathlib import Path
import sys

import numpy as np
from PIL import Image

from artwork_outline import BLACK, OPAQUE, RANGES, VANILLA_PPC, measure, parseSize

ROOT = Path(__file__).resolve().parents[1]
SUPER = 4             # the stroke is planned at this many times the export's resolution
AS_DRAWN = 0.25       # game px: a visible line this close to the aim (or wider) is left as drawn
SPECK = 0.4           # game px: alpha islands thinner than twice this are debris, and get no stroke
EDGE_NOT_BLACK = 0.5  # share of the outer 1 game px not black at which black outside it is a double line
KEYLINE = 0.7         # an edge pixel darker than this share of the brightest fill nearby is drawn line, not fill
KEYLINE_REACH = 2.0   # game px: how far 'nearby' reaches, and how deep a coloured line can run from the edge
KEYLINE_BLEND = 0.3   # game px: dark pixels this close to black line are its soft edge, not a keyline


def exportRecipe(manifestPath, output):
    spec = importlib.util.spec_from_file_location("lumen_export_layered", ROOT / "tools/export-layered-artwork.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    with open(os.devnull, "w") as quiet:
        stdout, sys.stdout = sys.stdout, quiet
        try:
            module.export(manifestPath, output)
        finally:
            sys.stdout = stdout


def distanceTo(target, sx, sy, limit):
    """Euclidean distance in game px from each pixel to the nearest True pixel, exact up to `limit`.

    sx and sy are game px per pixel across and down, so the draw size's stretch is respected. Beyond the
    limit the value is only known to be larger.
    """
    cap = limit + max(sx, sy)
    big = np.float32(cap * 4)
    gy = np.where(target, np.float32(0), big)
    for d in range(1, int(np.ceil(cap / sy)) + 1):
        value = np.float32(d * sy)
        gy[d:] = np.minimum(gy[d:], np.where(target[:-d], value, big))
        gy[:-d] = np.minimum(gy[:-d], np.where(target[d:], value, big))
    squared = gy * gy
    best = squared.copy()
    for d in range(1, int(np.ceil(cap / sx)) + 1):
        add = np.float32((d * sx) ** 2)
        best[:, d:] = np.minimum(best[:, d:], squared[:, :-d] + add)
        best[:, :-d] = np.minimum(best[:, :-d], squared[:, d:] + add)
    return np.sqrt(best)




def localMax(values, rx, ry):
    """Largest value within rx pixels across and ry pixels down of each pixel."""
    rows = values.copy()
    for d in range(1, ry + 1):
        rows[d:] = np.maximum(rows[d:], values[:-d])
        rows[:-d] = np.maximum(rows[:-d], values[d:])
    out = rows.copy()
    for d in range(1, rx + 1):
        out[:, d:] = np.maximum(out[:, d:], rows[:, :-d])
        out[:, :-d] = np.maximum(out[:, :-d], rows[:, d:])
    return out


def keyline(rgba, sx, sy):
    """Drawn exterior line that is not black: a coloured or dark-grey keyline round the silhouette.

    A pixel counts when it is clearly darker than the brightest fill within KEYLINE_REACH, lies within that
    reach of the background or the black exterior line, and joins them through other such pixels. A dark
    facet is not line: away from its lighter neighbour nothing nearby is brighter. Used only to report the
    line as it shows (`band`). The layer does not plan from it: adding just what a keyline lacks was tried
    on 2026-11-15 and left a two-tone edge of uneven black, which the user rejected.
    """
    opaque = rgba[..., 3] > OPAQUE * 255
    black = opaque & (rgba[..., :3].max(axis=2) <= BLACK * 255)
    lum = rgba[..., :3].astype(np.float32) @ np.array([0.299, 0.587, 0.114], np.float32)
    lum[~opaque] = 0
    near = localMax(lum, int(round(KEYLINE_REACH / sx)), int(round(KEYLINE_REACH / sy)))
    dark = opaque & ~black & (lum < KEYLINE * near)
    # The soft pixels where a black line blends into the fill are dark too, but they are that line's edge,
    # not a second line, and the width measurement (pure black, set against vanilla) already allows for them.
    # So a dark strip beside black counts only where it is wider than that blend. A texture too coarse to
    # show the difference (a pixel wider than the blend) keeps every dark pixel.
    step = max(sx, sy)
    if step < KEYLINE_BLEND:
        wide = dark & (distanceTo(black, sx, sy, KEYLINE_BLEND) > KEYLINE_BLEND)
        dark &= distanceTo(wide, sx, sy, KEYLINE_BLEND + step) <= KEYLINE_BLEND + step
    shell = outside(~opaque | black | dark)
    return dark & shell & (distanceTo(shell & ~dark, sx, sy, KEYLINE_REACH) <= KEYLINE_REACH)


def band(path, draw, aim, cut=None):
    """The exterior line as it shows on an exported sprite: black plus any coloured keyline.

    Returns the 5th, 50th and 95th percentile of its width in game px, measured at each pixel of the outer
    rim (pixels on the canvas edge, and near `cut`, the canvas region past a crop's open edge, left out), the
    share of it that is coloured, and `notchPx2`: game px² of line lying deeper than the aim from the open
    background, which is line filling a valley or notch.
    """
    rgba = np.asarray(Image.open(path).convert("RGBA"))
    sx, sy = VANILLA_PPC * draw[0] / rgba.shape[1], VANILLA_PPC * draw[1] / rgba.shape[0]
    step = max(sx, sy)
    opaque = rgba[..., 3] > OPAQUE * 255
    coloured = keyline(rgba, sx, sy)
    line = (opaque & (rgba[..., :3].max(axis=2) <= BLACK * 255)) | coloured
    open_ = outside(~opaque)
    rim = opaque & (distanceTo(open_, sx, sy, step) <= step * 1.01)
    rim[[0, -1], :] = False
    rim[:, [0, -1]] = False
    if cut is not None and cut.shape == rim.shape:
        rim &= distanceTo(cut, sx, sy, aim + 2 * step) > aim + step
    if not rim.any():
        return {"bandPx": [0.0, 0.0, 0.0], "notchPx2": 0.0, "keylineShare": 0.0}
    limit = 3 * aim
    widths = np.minimum(distanceTo(opaque & ~line, sx, sy, limit)[rim], limit)
    shell = outside(~opaque | line) & opaque
    deep = shell & (distanceTo(open_, sx, sy, aim + 2 * step) > aim + step)
    return {"bandPx": [round(float(v), 2) for v in np.percentile(widths, [5, 50, 95])],
            "notchPx2": round(float(deep.sum() * sx * sy), 1),
            "keylineShare": round(float((coloured & shell).sum() / max(int(shell.sum()), 1)), 2)}


def composeSource(manifest, folder):
    """The recipe's colour stack at source size, placed exactly as the exporter places it."""
    size = tuple(manifest["sourceSize"])
    color = Image.new("RGBA", size)
    for entry in manifest["layers"]:
        layer = Image.open(folder / entry["path"]).convert("RGBA")
        if "offset" in entry or "scale" in entry:
            scale = entry.get("scale", 1)
            if scale != 1:
                layer = layer.resize(tuple(max(1, round(v * scale)) for v in layer.size), Image.Resampling.LANCZOS)
            placed = Image.new("RGBA", size)
            placed.paste(layer, tuple(entry.get("offset", (0, 0))))
            layer = placed
        color = Image.alpha_composite(color, layer)
    return color


def outside(mask):
    """Pixels of `mask` 4-connected to the image border through `mask`."""
    from PIL import ImageDraw
    image = Image.fromarray(np.pad(mask, 1, constant_values=True).astype(np.uint8) * 255).copy()
    ImageDraw.floodfill(image, (0, 0), 128)
    return (np.asarray(image) == 128)[1:-1, 1:-1]


def cropCut(art, crop, sx, sy, width):
    """Pixels beyond each stretch of crop edge that cuts through the art (a wall lamp's south view keeps only
    the tip above the wall): the art crosses the edge there, so the cut is an open edge, not a contour, and
    no line goes past it. Each stretch is widened by the line width so the side lines end at the cut too.
    `art` is the source's alpha before the crop removed anything."""
    height, wide = art.shape
    alongX, alongY = math.ceil((width + 1) / sx), math.ceil((width + 1) / sy)
    forbid = np.zeros(art.shape, bool)
    x0, y0, x1, y1 = crop
    # Each side: pixels just inside, just outside, and the region beyond it as (rows, cols) for a stretch.
    sides = []
    if y1 < height:
        sides.append((art[y1 - 1, x0:x1], art[y1, x0:x1], x0, alongX, lambda a, b: (slice(y1, None), slice(a, b))))
    if y0 > 0:
        sides.append((art[y0, x0:x1], art[y0 - 1, x0:x1], x0, alongX, lambda a, b: (slice(None, y0), slice(a, b))))
    if x1 < wide:
        sides.append((art[y0:y1, x1 - 1], art[y0:y1, x1], y0, alongY, lambda a, b: (slice(a, b), slice(x1, None))))
    if x0 > 0:
        sides.append((art[y0:y1, x0], art[y0:y1, x0 - 1], y0, alongY, lambda a, b: (slice(a, b), slice(None, x0))))
    for inside, beyond, start, reach, region in sides:
        crossing = np.flatnonzero(inside & beyond)
        if not len(crossing):
            continue
        breaks = np.flatnonzero(np.diff(crossing) > 1)
        for first, last in zip(np.r_[crossing[0], crossing[breaks + 1]], np.r_[crossing[breaks], crossing[-1]]):
            forbid[region(max(0, start + first - reach), start + last + reach + 1)] = True
    return forbid


def edgeNotBlack(rgba, sx, sy):
    """Share of the silhouette's outer 1 game px that is not black. High when the drawn exterior line is
    coloured (a brown rim, a navy keyline) or missing: the layer then adds black outside it, a double line."""
    opaque = rgba[..., 3] > OPAQUE * 255
    band = opaque & (distanceTo(~opaque, sx, sy, 1.0) <= 1.0)
    coloured = rgba[..., :3].max(axis=2) > BLACK * 255
    return float((band & coloured).sum() / max(int(band.sum()), 1))


def placement(manifest):
    """The exporter's mapping: output px per source px across and down, and the crop corner's canvas position."""
    crop, box = manifest["crop"], manifest["targetBox"]
    scale = min((box[2] - box[0]) / (crop[2] - crop[0]), (box[3] - box[1]) / (crop[3] - crop[1]))
    size = (max(1, round((crop[2] - crop[0]) * scale)), max(1, round((crop[3] - crop[1]) * scale)))
    offset = (box[0] + (box[2] - box[0] - size[0]) // 2, box[1] + (box[3] - box[1] - size[1]) // 2)
    return (size[0] / (crop[2] - crop[0]), size[1] / (crop[3] - crop[1])), offset


def toCanvas(mask, manifest, supersample=4):
    """Carry a source-size mask (0-255) onto the output canvas with the export's mapping, antialiased."""
    (ax, ay), offset = placement(manifest)
    crop, canvas, k = manifest["crop"], manifest["canvas"], supersample
    data = (1 / (k * ax), 0, crop[0] - offset[0] / ax, 0, 1 / (k * ay), crop[1] - offset[1] / ay)
    big = Image.fromarray(mask).transform((canvas[0] * k, canvas[1] * k), Image.Transform.AFFINE, data,
                                          resample=Image.Resampling.BILINEAR)
    return big.reduce(k)




def stroke(art, draw, width, cut=None):
    """Black coverage (0-255, canvas size) reaching `width` game px outside the drawn silhouette of `art`
    (RGBA image), and the canvas px it would run past each edge. `cut`, a canvas-size bool mask, gets none."""
    w, h = art.size
    sx, sy = VANILLA_PPC * draw[0] / (w * SUPER), VANILLA_PPC * draw[1] / (h * SUPER)
    big = art.getchannel("A").resize((w * SUPER, h * SUPER), Image.Resampling.BICUBIC)
    margin = math.ceil(width / min(sx, sy)) + SUPER
    shape = np.pad(np.asarray(big) >= 128, margin)
    # Debris (a stray speck of alpha) would get a ring of its own: an opening drops it first.
    shape = distanceTo(distanceTo(~shape, sx, sy, SPECK) > SPECK, sx, sy, SPECK) <= SPECK
    reach = distanceTo(shape, sx, sy, width + max(sx, sy)) <= width
    rows, cols = np.nonzero(reach.any(axis=1))[0], np.nonzero(reach.any(axis=0))[0]
    past = {}
    if len(rows):
        over = {"top": margin - rows[0], "bottom": rows[-1] - (margin + h * SUPER - 1),
                "left": margin - cols[0], "right": cols[-1] - (margin + w * SUPER - 1)}
        past = {side: math.ceil(n / SUPER) for side, n in over.items() if n > 0}
    reach = reach[margin:-margin, margin:-margin]
    if cut is not None and cut.any():
        reach &= np.asarray(Image.fromarray(cut.astype(np.uint8) * 255).resize(reach.shape[::-1])) == 0
    return np.asarray(Image.fromarray(reach.astype(np.uint8) * 255).reduce(SUPER)), past


def openEdge(recipe, manifest, draw, width):
    """Canvas pixels past a crop edge that cuts through the art (a wall lamp's south view), where the stroke
    stops: the art runs on behind the wall there, so the cut is an open edge, not a contour."""
    (ax, ay), _ = placement(manifest)
    canvas = manifest["canvas"]
    sx, sy = ax * VANILLA_PPC * draw[0] / canvas[0], ay * VANILLA_PPC * draw[1] / canvas[1]
    art = np.asarray(composeSource(manifest, recipe.parent))[..., 3] > OPAQUE * 255
    beyond = cropCut(art, manifest["crop"], sx, sy, width)
    if not beyond.any():
        return None
    return np.asarray(toCanvas((beyond * 255).astype(np.uint8), manifest)) > 0


def build(colour, output, draw, target, width, cut, fromMask):
    """Write export/ (the colour with the stroke under it), outline-mask.png and outline-preview.png."""
    art = Image.open(colour).convert("RGBA")
    maskPath = output / "outline-mask.png"
    if fromMask:
        edited = Image.open(maskPath).convert("RGBA")
        if edited.size != art.size:
            raise SystemExit("outline-mask.png must match the export's canvas")
        cover, past = np.asarray(edited)[..., 0].copy(), {}
    else:
        cover, past = stroke(art, draw, width, cut)
        zero = np.zeros_like(cover)
        Image.fromarray(np.dstack([cover, zero, zero, np.full_like(cover, 255)]), "RGBA").save(maskPath)
    black = Image.new("RGBA", art.size)
    black.putalpha(Image.fromarray(cover))
    result = Image.alpha_composite(black, art)
    (output / "export").mkdir(exist_ok=True)
    result.save(output / "export" / colour.name)
    preview = np.zeros((*cover.shape, 4), np.uint8)
    preview[..., 0], preview[..., 3] = 255, cover
    Image.alpha_composite(Image.fromarray(preview, "RGBA"), art).save(output / "outline-preview.png")
    return result, past


def run(source, output, draw, target, kind, maxAdd=None, fromMask=False, png=False):
    """Measure the drawn line, decide, stroke, and measure again; returns and writes outline-layer.json."""
    output = Path(os.path.abspath(output))
    if not output.is_relative_to(ROOT / "work"):
        raise SystemExit("--output must be under work/")
    output.mkdir(parents=True, exist_ok=True)
    (output / "baseline").mkdir(exist_ok=True)
    source = Path(os.path.abspath(source))
    cut = None
    if png:
        if not draw:
            raise SystemExit("Give --draw-size: a deployed PNG records none")
        mask = source.with_name(source.stem + "_m" + source.suffix)
        for f in (source, mask):
            if f.exists():
                shutil.copyfile(f, output / "baseline" / f.name)
        # The batch reads the canvas and file names from a small record, as it does from a recipe.
        record = {"png": str(source), "canvas": list(Image.open(source).size), "colorFilename": source.name,
                  "maskFilename": mask.name}
        (output / "png").mkdir(exist_ok=True)
        (output / "png" / "master.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
        recipe, name, maskName = output / "png" / "master.json", source.name, mask.name
    else:
        manifest = json.loads(source.read_text(encoding="utf-8"))
        if manifest.get("paintedColor"):
            raise SystemExit("This recipe overrides colour with paintedColor; stroke its deployed PNG with --png")
        draw = draw or tuple(manifest.get("candidateChecks", {}).get("drawSize") or ()) or None
        if not draw:
            raise SystemExit("Give --draw-size: the recipe does not record one")
        exportRecipe(source, output / "baseline")
        recipe, name, maskName = source, manifest["colorFilename"], manifest["maskFilename"]
    colour = output / "baseline" / name
    drawn, shown = measure(colour, draw, None, kind), band(colour, draw, target)
    needed = round(target - shown["bandPx"][1], 2)
    rel = str(recipe.relative_to(ROOT)).replace(os.sep, "/")
    before = {**{k: drawn[k] for k in ("widthPx", "evennessPx", "clipped")}, **shown}
    report = {"recipe": rel, "drawSize": list(draw), "target": target, "needed": needed, "before": before}

    def skip(reason, **extra):
        shutil.rmtree(output / "export", ignore_errors=True)
        report.update(skipped=reason, **extra)
        (output / "outline-layer.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        return report

    if not fromMask:
        if needed <= AS_DRAWN:
            return skip(f"the visible line is {shown['bandPx'][1]:g}px, within {AS_DRAWN:g}px of the aim or wider",
                        asDrawn=True)
        if maxAdd is not None and needed > maxAdd:
            return skip(f"the visible line is {shown['bandPx'][1]:g}px, {needed:g}px under the aim; the stroke "
                        f"adds at most {maxAdd:g}px")
        rgba = np.asarray(Image.open(colour).convert("RGBA"))
        edge = edgeNotBlack(rgba, VANILLA_PPC * draw[0] / rgba.shape[1], VANILLA_PPC * draw[1] / rgba.shape[0])
        if maxAdd is not None and edge >= EDGE_NOT_BLACK:
            return skip(f"{edge:.0%} of the drawn edge is not black; black added outside it would be a double line",
                        edgeNotBlack=round(edge, 2))
        if not png:
            cut = openEdge(source, manifest, draw, needed + 1)
    notes = ["left the edge open where the crop cuts through the art"] if cut is not None else []
    width = needed
    result, past = build(colour, output, draw, target, width, cut, fromMask)
    after = band(output / "export" / name, draw, target, cut)
    if not fromMask:
        # The median rarely lands exactly: one correction by the miss.
        miss = target - after["bandPx"][1]
        step = VANILLA_PPC * max(draw[0] / result.width, draw[1] / result.height)
        if abs(miss) > max(0.1, step / 2):
            width = max(0.0, width + miss)
            result, past = build(colour, output, draw, target, width, cut, fromMask)
            after = band(output / "export" / name, draw, target, cut)
    mask = output / "baseline" / maskName
    if mask.exists():
        shutil.copyfile(mask, output / "export" / maskName)
    measured = measure(output / "export" / name, draw, None, kind)
    art = np.asarray(Image.open(colour).convert("RGBA"))
    solid = art[..., 3] > OPAQUE * 255
    # Black under a nearly opaque pixel (alpha 253, common in exports) shifts it a level or two: unchanged.
    same = (np.abs(art.astype(int) - np.asarray(result).astype(int)) <= 8).all(axis=2) & solid
    if cut is not None:
        shown = band(colour, draw, target, cut)
        report["before"].update(shown)
    report.update({
        "added": round(width, 2), "cutByCanvas": past, "notes": notes,
        "after": {**{k: measured[k] for k in ("widthPx", "evennessPx", "clipped")}, **after},
        "solidPixelsUnchanged": [int(same.sum()), int(solid.sum())],
    })
    (output / "outline-layer.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return report


TINTS = (("neutral", (1, 1, 1)), ("slate", (0.33, 0.33, 0.35)), ("wood", (0.55, 0.35, 0.2)))


def lineup(items, output, zoom=3):
    """A set of sprites side by side at game scale (64 px per cell, enlarged `zoom` times without
    smoothing), one row per construction tint, on a soil colour. items: (label, colour path, draw size)."""
    from PIL import ImageDraw
    from artwork import tinted
    sizes = [(round(w * VANILLA_PPC), round(h * VANILLA_PPC)) for _, _, (w, h) in items]
    cell = (max(s[0] for s in sizes) * zoom + 16, max(s[1] for s in sizes) * zoom + 28)
    sheet = Image.new("RGBA", (70 + cell[0] * len(items), cell[1] * len(TINTS)), (138, 122, 94, 255))
    draw = ImageDraw.Draw(sheet)
    for row, (material, tint) in enumerate(TINTS):
        draw.text((6, row * cell[1] + cell[1] // 2), material, fill="white")
        for col, ((label, path, _), size) in enumerate(zip(items, sizes)):
            path = Path(path)
            maskPath = path.with_name(path.stem + "_m" + path.suffix)
            mask = Image.open(maskPath).convert("RGBA") if maskPath.exists() else None
            view = tinted(Image.open(path).convert("RGBA"), mask, size, tint, 0)
            view = view.resize((size[0] * zoom, size[1] * zoom), Image.Resampling.NEAREST)
            x = 70 + col * cell[0] + (cell[0] - view.width) // 2
            sheet.alpha_composite(view, (x, row * cell[1] + cell[1] - 24 - view.height))
            if row == len(TINTS) - 1 or row == 0:
                draw.text((70 + col * cell[0] + 4, row * cell[1] + (cell[1] - 16 if row else 2)), label, fill="white")
    sheet.save(output)


def lineupMain(argv):
    parser = argparse.ArgumentParser(description="Show sprites side by side at game scale under construction tints.")
    parser.add_argument("items", nargs="+", metavar="LABEL=PATH@W,H",
                        help="a label, the colour PNG (its _m mask beside it is used) and the draw size")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--zoom", type=int, default=3)
    args = parser.parse_args(argv)
    items = []
    for item in args.items:
        label, rest = item.split("=", 1)
        path, size = rest.rsplit("@", 1)
        items.append((label, path, parseSize(size)))
    lineup(items, args.output, args.zoom)
    return 0




def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("recipe", type=Path,
                        help="the piece's export recipe (artwork/objects.json recipes), or with --png a deployed PNG")
    parser.add_argument("--png", action="store_true",
                        help="the input is a deployed PNG with no recipe: stroke it as it is")
    parser.add_argument("--output", type=Path, required=True, help="a folder under work/")
    parser.add_argument("--target", type=float,
                        help="exterior width at 64 px per cell (default: the --kind's aim in the outline rule)")
    parser.add_argument("--draw-size", type=parseSize, metavar="W,H")
    parser.add_argument("--from-mask", action="store_true", help="rebuild from an edited outline-mask.png")
    parser.add_argument("--kind", choices=sorted(RANGES), default="building")
    parser.add_argument("--max-add", type=float, metavar="PX",
                        help="leave the piece for a redo when its visible line is more than this many game px "
                             "under the target, or when half or more of its drawn edge is not black")
    args = parser.parse_args(argv)
    if args.target is None:
        args.target = RANGES[args.kind][2]
    report = run(args.recipe, args.output, args.draw_size, args.target, args.kind, args.max_add, args.from_mask,
                 args.png)
    median = report["before"]["bandPx"][1]
    if "skipped" in report:
        print(f"{report['recipe']}: {'left as drawn' if report.get('asDrawn') else 'not stroked'}: "
              f"{report['skipped']}")
        return 0
    same, total = report["solidPixelsUnchanged"]
    cut = report["cutByCanvas"]
    print(f"{report['recipe']}: visible line {median:g} -> {report['after']['bandPx'][1]:g}px "
          f"({report['after']['bandPx'][0]:g}-{report['after']['bandPx'][2]:g}), stroke {report['added']:g}px; "
          f"black line {report['before']['widthPx']} -> {report['after']['widthPx']}px; "
          f"art pixels unchanged {same}/{total}"
          + (f"; canvas cuts the stroke ({', '.join(f'{k} {v}px' for k, v in cut.items())})" if cut else "")
          + "".join(f"; {note}" for note in report["notes"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
