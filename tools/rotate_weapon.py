"""Rotate melee weapon art to the project convention: horizontal, tip right (family rule 7)."""
import argparse
import math
from pathlib import Path

import numpy as np
from PIL import Image

PAD = 4
SHAFT_WIDTH = 6  # pixels either side of the axis that still count as shaft


def headAngle(image):
    """Math-convention angle (degrees, y up) from the handle end to the heavier head end along the principal axis."""
    pixels = np.asarray(image.convert("RGBA")).astype(int)
    alpha = pixels[..., 3] > 128
    ys, xs = np.nonzero(alpha)
    points = np.stack([xs - xs.mean(), -(ys - ys.mean())], axis=1).astype(float)
    # The neutral dark shaft gives the true axis; the coloured head skews a fit over every pixel.
    rgb = pixels[..., :3]
    shaft = alpha & (np.ptp(rgb, axis=2) < 30) & (rgb.mean(axis=2) < 110)
    sy, sx = np.nonzero(shaft)
    shaftPoints = np.stack([sx - xs.mean(), -(sy - ys.mean())], axis=1).astype(float)
    axis = None
    for _ in range(4):
        # Refit on the pixels near the current axis, dropping head outline that skews it.
        kept = shaftPoints if axis is None else shaftPoints[np.abs(shaftPoints @ np.array([-axis[1], axis[0]])
                                                                    - offset) < SHAFT_WIDTH]
        _, vectors = np.linalg.eigh(np.cov(kept.T))
        axis = vectors[:, 1]
        offset = float(np.median(kept @ np.array([-axis[1], axis[0]])))
    along, across = points @ axis, points @ np.array([-axis[1], axis[0]])
    low, high = np.percentile(along, 25), np.percentile(along, 75)
    # The head is the end whose cross-section is wider.
    widthHigh = np.ptp(across[along >= high])
    widthLow = np.ptp(across[along <= low])
    if widthLow > widthHigh:
        axis = -axis
    return math.degrees(math.atan2(axis[1], axis[0]))


def rotate(image, angle):
    """Rotate counter-clockwise about the canvas centre by premultiplied bicubic sampling; crop to a square."""
    image = image.convert("RGBA")
    premultiplied = []
    alpha = image.getchannel("A")
    for channel in image.split()[:3]:
        premultiplied.append(Image.fromarray((np.asarray(channel, float) * np.asarray(alpha, float) / 255).astype("float32"), "F"))
    premultiplied.append(Image.fromarray(np.asarray(alpha, "float32"), "F"))
    turned = [ch.rotate(angle, Image.Resampling.BICUBIC, expand=True) for ch in premultiplied]
    a = np.clip(np.asarray(turned[3]), 0, 255)
    safe = np.where(a > 0, a, 1)
    rgb = [np.clip(np.asarray(ch) * 255 / safe, 0, 255) for ch in turned[:3]]
    result = Image.fromarray(np.stack(rgb + [a], axis=-1).round().astype("uint8"), "RGBA")
    # Keep the original centre as the pivot: crop symmetrically about it, as large as the artwork needs.
    box = result.getchannel("A").point(lambda v: 255 if v > 0 else 0).getbbox()
    cx, cy = result.width / 2, result.height / 2
    half = math.ceil(max(cx - box[0], box[2] - cx, cy - box[1], box[3] - cy)) + PAD
    half += half % 2
    return result.crop((round(cx) - half, round(cy) - half, round(cx) + half, round(cy) + half))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--head-angle", type=float, help="Override the detected handle-to-head angle")
    args = parser.parse_args()
    with Image.open(args.source) as source:
        angle = args.head_angle if args.head_angle is not None else headAngle(source)
        turned = rotate(source, -angle)
    turned.save(args.output)
    print(f"head angle {angle:.1f} degrees; canvas {turned.width}")


if __name__ == "__main__":
    main()
