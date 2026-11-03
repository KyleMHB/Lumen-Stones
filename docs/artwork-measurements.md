# Artwork measurements

Numbers for the [family design rules](rimworld-art-guide.md#family-design-rules). Regenerate with `python tools/artwork_measure.py`, which writes every sprite's figures to [style-measurements.json](../artwork/style-measurements.json). Measured 2026-11-01 from the files in `Textures/`.

## Method

Each sprite is scaled so its canvas fits 128px (box filter, channels separately). Opaque means alpha above 128.

- **Outline px:** rings of pixels counted inward from the edge. Each ring adds how far below the interior's median luminance it sits, and counting stops at the first ring under 15 percent darker. A half-dark ring counts as half.
- **Facets:** regions of the same colour (8 levels per channel) of at least 16px, outside the outline.
- **Facets per kilopixel:** facets divided by opaque pixels, times 1000.
- **Luminance:** 5th, 50th and 95th percentile.
- **Hue:** saturation-weighted circular mean over pixels above 0.2 saturation, taken from each stone's three-chunk stack. Used by the hue check.

Gradient banding can inflate facet counts, so a high count does not prove flat-facet shading. Treat the figures as a floor check, not a quality score.

## Reference figures

| Piece | Role | Outline px | Facets | Per kpx | Luminance p5 / p50 / p95 |
| --- | --- | --- | --- | --- | --- |
| Serenite column | style, mid | 1.2 | 36 | 9.6 | 21 / 140 / 224 |
| Serenite pylon | style, mid | 1.7 | 44 | 9.1 | 12 / 152 / 234 |
| Serenite cluster | style, floor | 2.0 | 48 | 7.7 | 20 / 147 / 231 |
| Serenite lamp | style, floor | 2.0 | 10 | 3.6 | 1 / 58 / 178 |
| Serenite wall lamp | style, floor | 3.0 | 12 | 2.1 | 16 / 115 / 224 |
| Serenite stack of three | style, floor | 2.0 | 14 | 2.0 | 18 / 107 / 226 |
| Verdite column | protected | 2.7 | 29 | 6.6 | 17 / 128 / 229 |
| Sunstone vessel | ceiling | 2.3 | 67 | 9.7 | 12 / 120 / 239 |
| Bloodstone vessel | ceiling | 1.9 | 88 | 13.3 | 0 / 60 / 185 |

Rule 2 names Serenite for outline weight and the Verdite column as protected, but their outlines differ by 1.5px. The column stays as approved and is a recorded exception in [family-checks.json](../artwork/family-checks.json).

## Tier floors

A piece meets its tier when it has at least 75 percent of the facets of its reference.

- **Floor and mid:** Serenite's piece of the same fixture type (column 27, pylon 33, cluster 36, lamp 8, wall lamp 9, stack 10).
- **Ceiling:** the smaller vessel, 67 facets, so 50.

The raw Verdite column has 29 facets, more than any lamp or stack in the repo, including Serenite's. Taking it literally as the floor for lamps and stacks would fail every stone, so the floor uses Serenite's same-type piece. Confirm this reading in review.

## Pieces below their floor

Pylons: Verdite (9), Vitalite (10), Glacite (9). Column: Vitalite (13). Clusters: Verdite (15), Vitalite (24), Aegite (32, borderline). Dark Stone pylon (28). All six signature weapons and the shield and pendant sit below the ceiling figure (12 to 23). The Aegite and Ferrite pylons pass by count; whether they are plain is a human call.
