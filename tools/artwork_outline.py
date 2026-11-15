"""Measure a sprite's outline against the house-style outline rule (docs/rimworld-art-guide.md#outline-rule).

Works in the sprite's own pixels and converts widths to vanilla's 64 pixels per cell, so no resampling
blends the line with the alpha edge. Width is perpendicular to the edge (distance from the alpha edge),
so slanted edges such as crystal tips are not over-counted. The rule, its numbers and their vanilla
source live in the guide; this module applies them.

Report only: the figures go to the user beside a candidate. Nothing here changes pixels, and no check or
deployment blocks on them; approved artwork ships as approved.
"""
import argparse
import json
from pathlib import Path
import sys

import numpy as np
from PIL import Image

VANILLA_PPC = 64
OPAQUE = 0.5          # alpha above this is part of the silhouette
BLACK = 25 / 255      # outline pixel: every channel at or below 25 of 255
FILL_DARK = 0.35      # value guide: fill pixels whose largest channel is below this count as dark
INTERIOR_MAX = 8      # interior dark runs wider than this (64 px per cell) are openings, not lines
# Exterior width at 64 px per cell: (low, high, aim). The one source of the outline rule's aims; the batch and
# the layer read them from here. A registry object's `outlineRole` picks its row.
RANGES = {
    "building": (1.8, 2.5, 2.0),   # Lumen aim 2.0px (user, 2026-11-14), held by the outline layer
    "resource": (2.0, 3.0, 2.0),
    "item": (1.0, 2.0, 1.5),
    # Vanilla measures 1.0px on knives, gladius, wall torch and torch lamp (2026-11-15), so thin pieces aim lower.
    "weapon": (1.0, 2.0, 1.0),
    "wall-lamp": (1.0, 2.0, 1.5),
}
EVENNESS = 1.0        # spread of local width, 10th to 90th percentile, at 64 px per cell
COVERAGE = 98.0       # percent of edge scans whose first opaque pixel is black

FOUR = ((1, 0), (-1, 0), (0, 1), (0, -1))
EIGHT = FOUR + ((1, 1), (1, -1), (-1, 1), (-1, -1))


def load(path):
    path = Path(path)
    if path.suffix.lower() == ".psd":
        from psd_tools import PSDImage   # optional: only vanilla sources are PSD
        return PSDImage.open(path).composite().convert("RGBA")
    with Image.open(path) as image:
        return image.convert("RGBA")


def shift(mask, dy, dx):
    """The mask as seen from each pixel's (dy, dx) neighbour; outside the canvas counts as empty."""
    padded = np.pad(mask, 1)
    height, width = mask.shape
    return padded[1 + dy:1 + dy + height, 1 + dx:1 + dx + width]


def edgeDistance(opaque):
    """Approximate distance in pixels from each opaque pixel to the nearest transparent one (1 = edge).

    Alternating 4- and 8-neighbour erosion gives an octagonal distance within about 8 percent of
    Euclidean.
    """
    depth = np.zeros(opaque.shape, float)
    current, step = opaque.copy(), 0
    while current.any():
        step += 1
        depth[current] = step
        following = current.copy()
        for dy, dx in (FOUR if step % 2 else EIGHT):
            following &= shift(current, dy, dx)
        current = following
    return depth


def coverageShare(opaque, black):
    """Percent of row and column scans whose first opaque pixel is black."""
    hits = []
    for op, bk in ((opaque, black), (opaque.T, black.T)):
        for row_op, row_bk in zip(op, bk):
            index = np.flatnonzero(row_op)
            if len(index):
                hits += [row_bk[index[0]], row_bk[index[-1]]]
    return float(np.mean(hits) * 100)


def interiorRuns(black, fill):
    """Lengths of black runs with fill on both sides, along rows and columns."""
    runs = []
    for bk, fl in ((black, fill), (black.T, fill.T)):
        for row_bk, row_fl in zip(bk, fl):
            count, started = 0, False
            for dark, filled in zip(row_bk, row_fl):
                if dark and started:
                    count += 1
                elif filled:
                    if started and count:
                        runs.append(count)
                    started, count = True, 0
                else:
                    started, count = False, 0
    return runs



def parseSize(text):
    """'1.1667,1.4583' -> (1.1667, 1.4583); a single number is a square draw size."""
    parts = [float(part) for part in str(text).strip("() ").split(",")]
    return (parts[0], parts[-1])


def grid(image, draw_size=None, ppc=None):
    """The sprite on a grid with equal pixels per cell on both axes, as RimWorld stretches it in game.

    RimWorld maps the canvas onto a quad of draw size W x H, so a canvas whose aspect differs from W:H has
    different pixels per cell across and down. The lower axis is enlarged with nearest-neighbour sampling,
    which adds no blended pixels, so pure black stays pure black. Returns the RGBA array (0-255), the
    common pixels per cell, the source row and column for each grid pixel, and the per-axis pixels per cell.
    """
    source = np.asarray(image)
    if ppc is not None:
        across = down = float(ppc)
    else:
        if draw_size is None:
            raise ValueError("Give the draw size (W,H in cells) or ppc")
        width, height = draw_size if isinstance(draw_size, tuple) else (draw_size, draw_size)
        across, down = image.width / float(width), image.height / float(height)
    common = max(across, down)
    rows = np.minimum((np.arange(round(image.height * common / down)) * down / common).astype(int), image.height - 1)
    cols = np.minimum((np.arange(round(image.width * common / across)) * across / common).astype(int), image.width - 1)
    return source[rows][:, cols], common, rows, cols, (across, down)


def measure(path, draw_size=None, ppc=None, kind="building"):
    """Measure one sprite. Give its draw size in cells, (W, H) or a number for a square, or ppc."""
    image = load(path)
    pixels, ppc, _, _, axes = grid(image, draw_size, ppc)
    rgba = pixels.astype(float) / 255
    scale = VANILLA_PPC / ppc
    opaque = rgba[..., 3] > OPAQUE
    if opaque.sum() < 50:
        raise ValueError(f"{path}: fewer than 50 opaque pixels")
    largest = rgba[..., :3].max(axis=2)
    black = (largest <= BLACK) & opaque
    fill = opaque & ~black
    depth = edgeDistance(opaque)
    edge = black & (depth == 1)
    # A silhouette cut by the canvas has no outline along the cut; report that side as clipped and leave
    # it out of the width figures, so a framing problem does not read as an uneven line.
    clipped = [side for side, line in (("top", opaque[0]), ("bottom", opaque[-1]),
                                       ("left", opaque[:, 0]), ("right", opaque[:, -1])) if line.any()]
    edge[[0, -1], :] = False
    edge[:, [0, -1]] = False
    # Local width at each black edge pixel: its distance to the nearest fill pixel. A material line
    # meeting the outline does not inflate it, because fill lies on either side of the line.
    # Pad with non-fill: beyond the canvas is empty, not fill, so a line near the edge is not cut short.
    pad = 64
    toFill = edgeDistance(np.pad(~fill, pad, constant_values=True))[pad:-pad, pad:-pad]
    widths = toFill[edge] * scale
    width = float(np.median(widths)) if widths.size else 0.0
    spread = float(np.percentile(widths, 90) - np.percentile(widths, 10)) if widths.size else 0.0
    beyond = depth > width / scale + 1        # past the exterior outline
    lines = [run * scale for run in interiorRuns(black & beyond, fill) if run * scale <= INTERIOR_MAX]
    coverage = coverageShare(opaque, black)
    low, high, aim = RANGES[kind]
    checks = {"width": low <= width <= high, "coverage": coverage >= COVERAGE, "evenness": spread <= EVENNESS}
    return {
        "path": str(path), "kind": kind, "ppc": round(ppc, 2),
        "widthPx": round(width, 2), "evennessPx": round(spread, 2), "coveragePct": round(coverage, 1),
        "clipped": clipped,
        "interiorLinePx": round(float(np.median(lines)), 2) if lines else None,
        # The fill share leaves out every black pixel; the all-pixels share is reported beside it so a
        # near-black body still shows.
        "fillDarkPct": round(float(((largest < FILL_DARK) & fill).sum() / max(1, fill.sum()) * 100), 1),
        "allDarkPct": round(float(((largest < FILL_DARK) & opaque).sum() / opaque.sum() * 100), 1),
        "target": {"low": low, "high": high, "aim": aim,
                   "sourcePxDown": round(aim * axes[1] / VANILLA_PPC, 1),
                   "sourcePxAcross": round(aim * axes[0] / VANILLA_PPC, 1)},
        "checks": checks, "passed": all(checks.values()),
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("paths", nargs="+", type=Path)
    size = parser.add_mutually_exclusive_group(required=True)
    size.add_argument("--draw-size", type=parseSize, metavar="W,H",
                      help="graphicData drawSize in cells; a single number for a square")
    size.add_argument("--draw-height", type=float, help="Draw height in cells, for a square draw size")
    size.add_argument("--ppc", type=float, help="Source pixels per cell, if known directly")
    parser.add_argument("--kind", choices=sorted(RANGES), default="building")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args(argv)
    draw = args.draw_size or args.draw_height
    failed = False
    for path in args.paths:
        result = measure(path, draw, args.ppc, args.kind)
        failed |= not result["passed"]
        if args.json:
            print(json.dumps(result))
            continue
        verdict = "PASS" if result["passed"] else "FAIL " + ",".join(k for k, ok in result["checks"].items() if not ok)
        target = result["target"]
        if target["sourcePxDown"] == target["sourcePxAcross"]:
            source = f"{target['sourcePxDown']}"
        else:
            source = f"{target['sourcePxDown']} top and bottom, {target['sourcePxAcross']} at the sides"
        print(f"{verdict}: {path.name} width {result['widthPx']}px, evenness {result['evennessPx']}px, "
              f"coverage {result['coveragePct']}%, interior lines {result['interiorLinePx']}px, "
              f"fill dark {result['fillDarkPct']}% (all {result['allDarkPct']}%); "
              f"aim {target['aim']}px = {source} source px"
              + (f"; clipped by the canvas at the {', '.join(result['clipped'])} (left out of the width)"
                 if result["clipped"] else ""))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
