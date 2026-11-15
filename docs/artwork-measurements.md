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

Historical comparison: the former rule named Serenite for outline weight and protected the Verdite column. Current house style uses the outline rule for both; the old comparison and exceptions in [family-checks.json](../artwork/family-checks.json) are diagnostic history, not redraw targets.

## Vanilla comparison

Measured 2026-11-10 with a different method from the table above: each sprite is resampled to 64 pixels per cell from its canvas and draw size, then measured as described in the shared guide's [measured vanilla rendering](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#measured-vanilla-rendering). The script is `work/style-normalisation-prototype/measure.py` (ignored); it needs `psd_tools` for vanilla PSDs. Its outline column is superseded by [outline rule figures](#outline-rule-figures). Pixels per cell were taken as canvas height over draw height: columns 410, floor lamps 256, clusters 512, resource stacks 128.

| Set | Exterior outline, px | Outline coverage | Saturation p90 | Interior detail | Hard interior edges |
| --- | --- | --- | --- | --- | --- |
| Vanilla buildings | 2–3 | 100% | 0–0.42 | 2.3–2.9 | 5–8% |
| Vanilla resources | 3 | 100% | 0.46 | 4.4 | 9% |
| Lumen columns (12) | 1 | 82% | 0.91 | 11.6 | 34% |
| Lumen floor lamps (12) | 1 | 93% | 0.82 | 8.4 | 23% |
| Lumen clusters (12) | 1 | 89% | 0.80 | 11.5 | 37% |
| Lumen resource stacks (14) | 1 | 57% | 0.79 | 12.6 | 30% |

Values are medians. Outliers: the Serenite column and the light-stone stacks have no dark contour (0px); the Dreadstone stack has 6px. The Hollowstone column and cluster and the Verdite cluster have the most interior detail (19 to 21). Six stones reach 1.00 saturation.

Only the outline figures are a target: the [outline rule](rimworld-art-guide.md#outline-rule) is taken from vanilla. Vanilla's saturation and interior-detail figures are a reference, not a target; the user rejected a pass that pushed every stone toward them on 2026-11-10.

## House style figures

Measured 2026-11-10 with the same method. Each row is the median over that stone's pylon, cluster or cooler, floor lamp, wall lamp, three-piece stack and column or blade. Dark area is the share of opaque pixels whose largest channel is below 0.35. The value guide now counts the fill only, excluding the black outline and lines; those shares are under [outline rule figures](#outline-rule-figures).

| Stone | Exterior outline, px | Saturation p90 | Interior detail | Hard interior edges | Dark area |
| --- | --- | --- | --- | --- | --- |
| Glacite (7) | 1 | 1.00 | 10.9 | 33% | 21% |
| Vitalite (6) | 1 | 0.74 | 7.9 | 22% | 17% |
| Dreadstone (6) | 2 | 1.00 | 9.3 | 27% | 48% |
| Dark Stone (6) | 1.5 | 0.86 | 13.0 | 42% | 39% |

- The user named Glacite and Vitalite as examples of what was done well, not as a standard. Their figures here illustrate the house style's facets, colour and value; they are not thresholds.
- No stone's outlines are taken as correct, these two included. The outline column here uses the older method; measured against the full rule, only the Glacite pylon passes ([outline rule figures](#outline-rule-figures)). Approved pieces are not corrected for this; the rule guides new pieces and requested redos ([ADR 0001](adr/0001-artwork-approval-is-the-release-contract.md)).
- Dark area separates the stones the user likes from the ones they flagged better than any other figure here. Dreadstone's stack is 58 percent dark with a 6px contour; Glacite's and Vitalite's stacks are 3 and 6 percent dark with no dark contour.
- Six or seven sprites per stone is a small sample, and one direction per object.

## Outline rule figures

Measured 2026-11-10 with `python tools/artwork.py outline` (`tools/artwork_outline.py`), the tool the [outline rule](rimworld-art-guide.md#measuring-an-outline) names. It supersedes the outline column of the two tables above, which counted every pixel below 0.3 as outline and resampled first; resampling blends the line into the alpha edge and thinned weapons in particular. Here, outline pixels are black (every channel 25 of 255 or below), width is perpendicular from the alpha edge, and figures are at 64 pixels per cell. Each sprite is first put on the grid the game draws it on, from its full draw size, so a stretched canvas is measured as it appears.

Vanilla core art from `E:\AI\rimworld-lora\extracted`. Draw sizes come from the installed game's `Data/Core/Defs`: buildings and stacks are 64 pixels per cell, weapons 128. Up to three files per definition; medians with quartiles in brackets.

| Group | Sprites | Exterior width | Evenness spread | Evenness ≤ 1.0px | Coverage ≥ 98% | Material lines | Fill dark share |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Buildings | 462 | 2.0 (1.3–3.0) | 1.0 (0–1.0) | 75% | 84% | 2.0 (1.0–2.7) | 25% (6–50) |
| Resource stacks | 103 | 2.0 (2.0–2.0) | 0 (0–0.9) | 98% | 92% | 1.5 (1.0–2.0) | 15% (9–30) |
| Ranged weapons | 30 | 1.0 (1.0–1.5) | 0.5 | 100% | 100% | 1.5 (1.0–2.0) | 27% |
| Melee weapons | 8 | 1.0 (1.0–1.5) | 0.5 | 88% | 100% | 1.0 (0.8–1.0) | 8% |

- The silhouette edge pixel is pure black in every group. Counting pixels below 0.3 instead gives about 3px on buildings and stacks: vanilla often has a dark grey band inside the black line.
- Coverage is 100 percent at the median in every group. The vanilla sprites below 98 percent were not inspected one by one.
- Material lines are black runs with fill on both sides, up to 8px; dark openings that narrow are included, so the figure is a guide to line weight, not a pure count of material boundaries.
- Fill dark share counts pixels below 0.35 outside every black pixel, so vanilla's dark grey shading band is included.

Lumen pieces, the same roles as [house style figures](#house-style-figures) plus the other dark-family stones and Lumenstone, one direction each. Draw size is from the consuming definition. Widths, spread and lines in px at 64 pixels per cell; dark shares in percent, of the fill and of all opaque pixels.

| Piece | Draw size | Width | Spread | Coverage % | Material lines | Fill dark | All dark | Rule |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Glacite GlacitePylon_north | 2.7×2.7 | 2.03 | 0.34 | 100.0 | 2.03 | 2.0 | 20.9 | pass |
| Glacite GlaciteCooler | 1.0×1.0 | 1.75 | 0.12 | 100.0 | 1.12 | 0.7 | 21.6 | fail |
| Glacite GlaciteLamp | 1.0×1.0 | 0.75 | 0.25 | 100.0 | 0.5 | 7.8 | 25.9 | fail |
| Glacite WallGlaciteTorch_north | 1.0×1.0 | 0.75 | 0.12 | 100.0 | 0.5 | 1.9 | 20.3 | fail |
| Glacite Glacite_c | 1.0×1.0 | 0.0 | 0.0 | 0.0 | — | 3.2 | 3.2 | fail |
| Glacite GlaciteColumn | 1.0×1.25 | 0.75 | 0.38 | 99.4 | 0.38 | 1.7 | 15.2 | fail |
| Glacite GlaciteBlade (greatsword 512×512, 2026-11-11) | 1.6×1.6 | 0.6 | 0.2 | 100.0 | 0.4 | 5.5 | 22.6 | fail (user exception) |
| Vitalite Pylon | 1.08×2.16 | 2.43 | 1.35 | 100.0 | 1.08 | 1.1 | 19.3 | fail |
| Vitalite Cluster | 1.0×1.12 | 0.38 | 0.25 | 100.0 | 0.5 | 2.0 | 11.3 | fail |
| Vitalite Lamp | 1.0×1.0 | 1.25 | 0.75 | 53.1 | 0.5 | 31.6 | 41.2 | fail |
| Vitalite WallLamp | 1.0×1.0 | 1.38 | 1.25 | 67.7 | 1.0 | 7.3 | 22.2 | fail |
| Vitalite Vitalite_c | 1.0×1.0 | 0.0 | 0.0 | 0.0 | — | 5.6 | 5.6 | fail |
| Vitalite VitaliteColumn | 1.0×1.25 | 0.75 | 0.25 | 100.0 | 0.5 | 2.7 | 15.2 | fail |
| Dreadstone Pylon | 2.2×2.2 | 1.65 | 0.55 | 100.0 | 1.1 | 33.4 | 45.2 | fail |
| Dreadstone Cluster | 1.0×1.0 | 1.0 | 0.5 | 100.0 | 0.62 | 24.5 | 35.9 | fail |
| Dreadstone Lamp | 1.0×1.0 | 0.75 | 0.32 | 100.0 | 0.5 | 45.2 | 58.0 | fail |
| Dreadstone WallLamp | 1.0×1.0 | 1.12 | 0.12 | 100.0 | 1.0 | 34.3 | 50.9 | fail |
| Dreadstone Dreadstone_c | 1.0×1.0 | 1.0 | 0.0 | 100.0 | 1.0 | 48.8 | 57.8 | fail |
| Dreadstone DreadstoneColumn | 1.1667×1.4583 | 0.73 | 0.73 | 79.4 | 0.73 | 35.1 | 42.6 | fail |
| DarkStone Pylon | 2.2×2.2 | 1.1 | 0.28 | 100.0 | 0.83 | 27.7 | 41.2 | fail |
| DarkStone Cluster | 1.0×1.0 | 1.0 | 0.25 | 100.0 | 0.62 | 10.5 | 31.4 | fail |
| DarkStone Lamp | 1.0×1.0 | 0.75 | 0.5 | 94.3 | 0.25 | 19.8 | 30.5 | fail |
| DarkStone WallLamp | 1.0×1.0 | 1.62 | 0.38 | 100.0 | 1.12 | 22.7 | 42.0 | fail |
| DarkStone DarkStone_c | 1.0×1.0 | 1.0 | 0.45 | 100.0 | 1.0 | 42.0 | 50.0 | fail |
| DarkStone DarkStoneColumn | 1.1667×1.4583 | 0.73 | 0.44 | 100.0 | 0.73 | 24.8 | 37.0 | fail |
| Lumenstone Pylon | 2.2×2.2 | 0.83 | 0.28 | 100.0 | 0.83 | 4.1 | 16.8 | fail |
| Lumenstone Cluster | 1.0×1.0 | 0.75 | 0.12 | 99.6 | 0.62 | 8.6 | 23.3 | fail |
| Lumenstone Lamp | 1.0×1.0 | 0.0 | 0.0 | 0.0 | 0.25 | 18.9 | 20.5 | fail |
| Lumenstone WallLamp | 1.0×1.0 | 0.62 | 0.64 | 75.4 | 0.62 | 13.3 | 27.4 | fail |
| Lumenstone Lumenstone_c | 1.0×1.0 | 0.0 | 0.0 | 0.0 | — | 10.2 | 10.2 | fail |
| Lumenstone LumenstoneColumn | 1.1667×1.4583 | 0.73 | 0.29 | 100.0 | 0.58 | 2.8 | 18.0 | fail |
| Bloodstone Bloodstone_c | 1.0×1.0 | 1.0 | 0.5 | 100.0 | 0.5 | 11.9 | 21.7 | fail |
| Sanguinite Pylon | 2.2×2.2 | 1.38 | 0.83 | 98.4 | 1.1 | 7.8 | 26.8 | fail |
| Sanguinite Cluster | 1.0×1.0 | 0.75 | 0.0 | 100.0 | 0.75 | 6.8 | 19.4 | fail |
| Sanguinite Lamp | 1.0×1.0 | 0.75 | 0.25 | 100.0 | 0.5 | 5.5 | 22.9 | fail |
| Sanguinite WallLamp | 1.0×1.0 | 0.62 | 0.5 | 91.4 | 0.62 | 3.0 | 17.4 | fail |
| Sanguinite Sanguinite_c | 1.0×1.0 | 1.0 | 0.5 | 100.0 | 0.5 | 39.7 | 47.5 | fail |
| Sanguinite SanguiniteColumn | 1.1667×1.4583 | 0.73 | 0.29 | 100.0 | 0.73 | 8.7 | 21.5 | fail |
| Hollowstone Pylon | 2.2×2.2 | 1.1 | 0.28 | 97.4 | 1.1 | 12.1 | 36.1 | fail |
| Hollowstone Cluster | 1.0×1.0 | 0.62 | 0.12 | 100.0 | 0.62 | 9.0 | 27.0 | fail |
| Hollowstone Lamp | 1.0×1.0 | 0.75 | 0.25 | 98.5 | 0.5 | 22.2 | 40.2 | fail |
| Hollowstone WallLamp | 1.0×1.0 | 1.5 | 0.25 | 100.0 | 0.75 | 3.9 | 36.2 | fail |
| Hollowstone Hollowstone_c | 1.0×1.0 | 0.5 | 0.0 | 98.1 | 0.5 | 15.5 | 26.3 | fail |
| Hollowstone HollowstoneColumn | 1.1667×1.4583 | 0.58 | 0.29 | 98.3 | 0.58 | 11.7 | 37.5 | fail |

The Glacite blade row was re-measured on 2026-11-11 after the greatsword replaced the one-handed sprite. Its [recipe](../artwork/design-sources/glacite-greatsword-selected-2026-11-11/master.json) is the approved image shrunk to 488×95 on a 512×512 canvas with no outline pass, so it keeps the approved 0.6px outline. Outline passes were tried and shown to the user: on the export (1.2px crystal, 0.8px hilt, which passed but chewed the crystal into hard steps) and on the full-size image before shrinking (1.0px, smooth, passed). The user chose the approved outline as drawn, a recorded exception to the weapon minimum of 1.0px. Draw 1.5 was rejected earlier: its non-integer grid (170.67 pixels per cell on a 256 canvas) left comb artefacts after a pass.

Of the 2026-11-10 measurements, only the Glacite pylon passed. The other widths have a median of 0.75px against vanilla's 2. Spread is mostly within the rule: the lines are thin rather than uneven. The light-stone stacks and the Lumenstone floor lamp have no black outline; Vitalite's floor and wall lamps and the Lumenstone wall lamp have gaps. The dark-family stacks have black lines of 0.5–1px, and the Dreadstone fill is the darkest measured (stack 49 percent, lamp 45).

## Requested versus drawn outline width

Sunstone migration, 2026-11-13. All frames were 1254 square. Drawn width is the measured export width converted back to source pixels through each recipe's reduction.

| Piece | Call | Requested (source px) | Drawn (source px) | Export at 64px/cell |
| --- | --- | --- | --- | --- |
| Array | design | 16 | ≈12 | 1.56px |
| Array | outline-only edit | 21 | ≈20 | 2.5px |
| Collector | design | 33 | ≈15 | 1.12px |
| Collector | outline-only edit | 28 | ≈20 | 1.50px |
| Vessel | design | 33 | ≈17 | 1.31px |
| Obelisk | design | 21 | ≈12 | 1.56px |
| Array (simplified) | design | 21 | ≈23 | 2.5px |
| Collector (simplified) | outline-only edit | 38 | ≈32 | 1.88px |
| Vessel (simplified) | outline-only edit | 34 | ≈32 | 2.44px |

Design calls mostly drew the model's own 12–17px line whatever was requested; the simplified array is the exception. Outline-only edits against a visible bar drew 70–100 percent of the request.

## Tier floors

Retired from the active generation/review procedure on 2026-11-10. These historical floors ask for a minimum facet count measured against Serenite, conflicting with the house style's broad, simple facets. Keep the figures as history; do not add facets or reject candidates to meet them. The Glacite and Vitalite pylons fall below them although the user named both as examples of what was done well.

A piece meets its tier when it has at least 75 percent of the facets of its reference.

- **Floor and mid:** Serenite's piece of the same fixture type (column 27, pylon 33, cluster 36, lamp 8, wall lamp 9, stack 10).
- **Ceiling:** the smaller vessel, 67 facets, so 50.

The raw Verdite column has 29 facets, more than any lamp or stack in the repo, including Serenite's. The former floor therefore used Serenite's same-type piece; this does not establish a current minimum.

## Pieces below their floor

Pylons: Verdite (9), Vitalite (10), Glacite (9). Column: Vitalite (13). Clusters: Verdite (15), Vitalite (24), Aegite (32, borderline). Dark Stone pylon (28). All six signature weapons and the shield and pendant sit below the ceiling figure (12 to 23). The Aegite and Ferrite pylons pass by count; whether they are plain is a human call.
