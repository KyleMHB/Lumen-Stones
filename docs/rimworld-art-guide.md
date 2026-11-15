# Lumen Stones artwork requirements

The reusable [RimWorld artwork guide](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#read-route) now lives in GlobalTemplates. Read only its relevant sections, then apply the Lumen-specific requirements below. This file keeps its original path and headings so existing tickets and tooling references still resolve.

## Current furnishing requirements

The [consolidated furnishing specification](../.scratch/layered-artwork-pipeline/spec.md) sets the repair scope. Apply these rules before changing furnishing art or definitions:

- **Placement and artwork:** pylons occupy 1x1, with tall artwork about one wide and two high. Serenite's sanctuary shrine is the exception: a low, non-rotating 2x1 bedroom furnishing that keeps `KM_SerenitePylon` and its 45% support category. Record visible alpha bounds, draw scale, padding and base anchor independently of occupied cells. Align feet against the actual placement and recalculate native icons when any framing input changes.
- **Rendering:** resolve inherited drawing modes as well as altitude. Pylons draw in realtime at the building-on-top altitude. Matching settings do not prove a draw-order problem is fixed; verify overlap arrangements in the final human review before choosing a draw-order intervention.
- **Floor torches:** on 2026-11-11 the user requested replacing every floor torch's wooden post and collar with a small cluster of its stone's crystals. This supersedes the earlier fixed-wood art direction; the new designs and implementation are pending. Preserve each stone's identity and effects, and account for saved material on existing instances when definitions change. Wall lamps keep their own cups.
- **Protected artwork:** keep the designs of the Verdite column and the complete user-selected Hollowstone floor lamp, the selected family identities, the flat Aegite capital and the agreed bed geometry. Record the exact preserved version; a positive review of one object does not approve unrelated masks or views.
- **Approval is final** (user ruling, 2026-11-12). Once the user approves a piece, that exact image ships. No rule, measurement, review or release check re-opens it: do not regenerate, retouch, re-export differently or replace it unless the user asks for a redo of that piece. If a later check flags approved art, report the finding and leave the art alone. The [outline rule](#outline-rule) guides generation before approval; it is not a reason to change approved or protected art. After approval only the mask (through the user's mask review) and computed derived files may be added; anything with new pixels, or a change of framing, source repair or recolour, is a new candidate. [ADR 0001](adr/0001-artwork-approval-is-the-release-contract.md) records the decision.
- **Material anatomy:** keep crystal/housing joins and openings readable and correctly assigned. A hand-traced partition can exclude real housing even when PNG and channel checks pass. Separate unwanted source matte from real housing and fixed crystal highlights; revise source colour and ownership independently when needed.
- **Display scale:** judge artwork at normal game zoom. The user handles close-zoom inspection in the editor; do not require enhanced-zoom reviews. Recover a clean master or repair a boundary when the visible result needs it. Keep broad facets rather than adding detail; outlines follow the [outline rule](#outline-rule).
- **Export:** one coordinated colour/mask transform from explicit ownership polygons or material layers, recorded in one recipe. Declare any source recovery separately. Final export preserves alpha; canvas size alone is not evidence of fidelity.
- **Acceptance:** prepare the complete candidate for one final combined human in-game review. Keep passed, failed and pending observations in the acceptance checklist and tie decisions to the loaded version. Mechanical checks and CPU models do not approve appearance.

Carry these contracts into the registry and checker rather than relying on reminders. The checker and staged hook enforce the 1x1, realtime and altitude pylon policy and fixed five-wood floor torches. The [tooling reference](artwork-tooling.md#current-automation-boundary) lists what the checks do not cover.


## House style

The artwork should be distinctive and beautiful, and sit comfortably beside vanilla. Status and pieces awaiting redo are in [ARTWORK.md](../ARTWORK.md#current-artwork-workflow); the evidence behind each number is in [artwork measurements](artwork-measurements.md#outline-rule-figures).

**The rules are this section, not any one stone.** Glacite and Vitalite are examples of crystal, colour and housing done well, not a standard to copy; when an example and a rule disagree, the rule wins. Never use or name another mod's artwork as a reference: this mod's look is its own.

- **Crystal:** one hue in about three values: a pale highlight plane, a body and a shadow. Broad facets, few enough to count at game zoom. Facets are the crystal's character.
- **Value:** the body is light to mid, so the black outline and facet edges stay readable. As a guide, no more than about 30 percent of the fill (everything but black lines) is darker than 0.35. A dark stone carries its darkness in hue and one shadow plane, not a near-black body.
- **Colour:** saturated is fine; there is no saturation cap.
- **Housing:** neutral light grey, a few rounded or blocky parts, one coherent motif (family rule 1).
- **Shapes:** a few large ones. Skulls, studs, horns and spikes that vanish at game zoom come out when a piece is redone. A crystal fixture has about 5–8 crystals, each wide enough to show its faces at game zoom; crystals only 4–8 pixels wide at 64 pixels per cell read as speckle. Housing stays slim so the crystal dominates.
- **No baked glow** or painted halo, including on command and ritual icons.
- **Outline:** the outline rule below, taken from vanilla and strict.

### Outline rule

Widths are at 64 pixels per cell, vanilla's scale. It applies the shared guide's [outline defaults](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#outline-defaults) with Lumen's aims: 2.0px buildings and stacks, 1.5px wall lamps and items, 1.0px weapons (user, 2026-11-14: "2px", and items 1.5px; 2026-11-15, after the vanilla comparison in rule 8: "thats good"). Generation asks for it, the [outline layer](#outline-layer) brings each new candidate's exterior to the aim, and the figures are reported beside the candidate so the user sees them when choosing. It is not a gate: a piece the user approves ships as drawn whatever it measures ([approval is final](#current-furnishing-requirements)), and no tool blocks on it.

1. **Black:** every channel 25 of 255 or below. No hue, no dark crystal colour, no grey. A darker shading band inside the line is fill, not outline.
2. **Unbroken** around the whole silhouette, except edges cut by the canvas or hidden behind a wall.
3. **Inside** the silhouette: its outer edge is the alpha edge, so it never changes silhouette, framing or mask extent. The one exception is the [outline layer](#outline-layer), which adds the line outside to bring it to width.
4. **Width:** buildings and resource stacks aim 2.0px; 1.8–2.5px passes for buildings, 2.0–3.0px for stacks. The [outline layer](#outline-layer) tops a thin line up to the aim with an outside stroke; a wider line stays as drawn.
5. **Even:** no tapering or swelling; local width varies by 1.0px or less.
6. **Material boundaries** (crystal and housing, wood and collar): a black line of 1.0–2.0px.
7. **No lines between facets** of the same material; facet edges are value changes only.
8. **Weapons, wall lamps and items.** Weapons aim 1.0px and wall lamps 1.5px; 1.0–2.0px passes for both. Items (apparel, equipment, carried items) aim 1.5px. Width follows how thin a piece is, not its size: vanilla keeps 2.0px on small chunky sprites (wall lamp, gold, jade, rock chunks, component) and uses 1.0px on thin ones (wall torch, torch lamp, knife, gladius), measured 2026-11-15. Floor decals drawn with a light line, such as the Lumen circle, take no outline layer (2026-11-14: a black layer made the circle heavy and busy).
9. **Thin parts** under 8px wide, such as a crystal shard, lamp post or haft, use about 1.5px so the contour never fills the part. A small spiky piece held to the full building width reads heavy at game scale (the regenerated Dark Stone cluster, 2026-11-12, was 35 percent black edge band against 17 percent for the previous one). The measuring tool does not yet separate thin parts, so judge a heavy-looking result on the `game-scale.png`, not the number.
10. **Nothing else at the edge:** no second keyline, light rim, glow or drop shadow.

#### Measuring an outline

```
python tools/artwork.py outline <sprite.png> --draw-size <W,H> [--kind building|resource|weapon]
```

Draw size is the definition's `graphicData/drawSize` (stacks and most items `1,1`). Use the interpreter in `git config lumen.artworkPython`. The tool checks rules 1, 2, 4, 5 and 8, measures the sprite as the game stretches it, and prints the target width in source pixels; a piece passes only when width, coverage and evenness all pass. A deliberate cut under rule 2 can fail coverage, so read a coverage failure against the sprite. It also reports material-line width and dark shares; judge a dark stone on the all-pixels share too. How it measures is in [artwork tooling](artwork-tooling.md).

#### Outline layer

The user wants one line weight across a set ("I don't want two pillars to have wildly different outlines", 2026-11-14). `artwork.py outline-layer` is how outline width is fixed ([ADR 0002](adr/0002-outline-layer-fixes-outline-width.md)). It adds black **outside** the art and never changes a pixel of it. Use it on every new candidate as part of the [light check](#the-three-steps), and on an approved piece only when the user names that piece for an outline redo:

```
python tools/artwork.py outline-layer <recipe> --draw-size <W,H> --kind <role> --output work/<piece>/outline
```

- The recipe is the candidate's recipe under `work/`, or for an approved piece its `artwork/objects.json` recipe, which must re-export to the approved texture (`baseline/` holds that export to compare). The stroke is planned on that export at four times its resolution and put under it.
- `--kind` picks the role and its aim from the [outline rule](#outline-rule) (`--target` overrides it); a registry object's `outlineRole` sets its role in a batch. Use one aim across a set.
- **What it does:** an outside stroke on the drawn silhouette, as wide as the aim less the visible line's median. The art, its contour and its line variation stay as drawn; a spike rounds only by the stroke's width; the new edge is antialiased; crop cuts stay open. A line already within 0.25px of the aim, or wider, is left as drawn: nothing is trimmed. `outline-layer --help` and the module docstring hold the details.
- **A layer is a small top-up, not a redo.** With `--max-add <px>` (the batch uses 0.75) a piece is left unstroked when its visible line is further than that under the aim, or when half or more of its drawn edge is not black: an outside line then fattens the silhouette or wraps a coloured keyline in a double line. Those pieces need the line drawn in, a redo the user names.
- **Read the report:** `outline-layer.json` gives `bandPx` (the visible line, black plus keyline, 5th/50th/95th percentile, crop cuts left out), the black line's width and evenness, and `added` (the stroke's width), before and after.
- **The canvas can cut the stroke;** the tool reports how far. Making room is a reframe of the recipe, a new export at a smaller scale that a painted mask no longer fits, so ask the user first.
- **No recipe** (stacks, implants, a `paintedColor` recipe): `outline-layer --png <Textures/...png>` strokes the deployed PNG as it is.
- **Every deployed piece at once**, only when the user names a full pass: `python tools/artwork.py outline-layer-deployed --output work/<batch>` writes game-scale sheets, `REPORT.md` and a `deploy-plan.json`. Test it in game with `tools/deploy.py stage`, `deploy` and `restore` on that plan, as the report shows; its entries come `from` the batch, so `Textures/` is untouched. The two earlier full passes and what the user rejected are in [ADR 0002](adr/0002-outline-layer-fixes-outline-width.md).
- **Review:** the user edits `outline-mask.png` in the mask editor beside `outline-preview.png`; rerun with `--from-mask`. Compare a set with `artwork.py outline-lineup`. A layered approved piece is a new candidate until the user approves it again ([the three steps](#the-three-steps)).
- Inside lines stay as drawn: the layer cannot reach them. If they come back too thin or missing, regenerate the design.

**No blanket filters.** Never correct artwork across the mod with a deterministic pass (saturation cap, facet blur, automatic outline thickening). **No outline passes either.** Never thicken inward, redraw or blacken an outline after generation, on the export or on the full-size source; the user ruled this out on 2026-11-11 after a pass chewed the Glacite greatsword's crystal. The only post-generation line is the [outline layer](#outline-layer): on new candidates as part of the light check, and on approved art only for pieces the user names. Never run it across approved art unasked, and never make it a gate.

These rendering rules override the family rules below; the family rules decide design.

## Generating a stone family

Follow this for a new stone and for redoing an existing one. When a step fails in practice, correct this section.

### The three steps

Every piece goes through three steps and nothing more unless its brief asks ([ADR 0001](adr/0001-artwork-approval-is-the-release-contract.md)). Keep each brief short: stone, piece, colours, what to retain, call budget.

1. **Generate.** Write the request from the [template](#make-each-piece) at the [common scale](#common-scale), with the stone's own piece, one [role example](#role-examples) and a scale guide attached. Save the source image and a recipe (canvas, crop, `candidateChecks.drawSize`) under `work/`.
2. **Light check.** Run `artwork.py prepare-candidate RECIPE --output work/PIECE/review` ([candidate validation](artwork-tooling.md#candidate-validation)). It checks canvas, framing and draw size. Then run the [outline layer](#outline-layer) on the recipe at the role's aim. The layered export in `work/PIECE/outline/export/` is the candidate. Look at it at normal game zoom, beside the set with `outline-lineup`, and show it to the user with the outline (before and after) and dark-share figures as numbers. Open the tinted and join views only for a piece with a material mask.
3. **Approve and integrate.** When the user approves, in the same turn: copy the layered export into `Textures/`, copy `outline-mask.png` and `outline-layer.json` into an `outline-<date>/` folder beside the piece's source under `artwork/design-sources/` so the line can be rebuilt, with the layered export as `layered.png`, its mask as `painted-mask.png` and a 1:1 `production.json` over them (crop and target box the whole canvas) that the registry's `recipes` points to, so `artwork.py check` verifies it, update the object's `approvedMaster` in `artwork/objects.json` (path, SHA-256, the user's words, date), add one line to `ARTWORK.md`, and run `python tools/artwork.py check`. Masks and computed views follow; see [record the current selection](artwork-tooling.md#record-the-current-selection).

Optional, only when a brief asks: the [attempt ledger](artwork-tooling.md#generation-capture-and-attempts), [protected-region checks](artwork-tooling.md#protected-region-checks), silhouette sheets and extra comparison sheets.

### Common scale

Every building piece in a family is generated at one world scale, so a pylon comes out more than twice as tall as a floor lamp rather than the same size. Rewritten 2026-11-13: before this, each role had its own canvas and the model filled every frame, so final pixels per cell ranged from about 233 (pylon) to 512 (wall lamp, cluster), and small pieces carried a pylon's worth of detail.

- **Generation:** 512 source pixels per cell. A 1024×1024 frame is 2×2 cells; a 1024×1536 frame is 2 wide by 3 tall. Use the square frame unless the piece is taller than 2 cells. The object is drawn at its real size, centred, standing on the frame's ground line. Empty frame around a small piece is correct.
- **Scale guide:** attach a plain image the size of the frame showing the 512px cell grid, the ground line and the target box from the table below. Save it under `work/PIECE/`. The request says the grid is a guide only and must not be drawn.
- **Export:** final canvas = draw size × 256, rounded to an even number, so the export is one uniform 0.5 reduction. The wall lamp is the exception: it keeps the shared cup's 512 canvas at scale 1.
- **Outline:** 2.0px at 64 pixels per cell is 16 source pixels at 512 per cell, for every building role. Material lines are 8–16 source pixels, thin parts about 12. After the 0.5 export the final outline is 8px. A wall lamp exports at scale 1 and aims 1.5px, so 12 source pixels on its 512 canvas.

If the object comes back filling the frame, the attempt failed on scale. Don't fix it by rescaling the export, which changes the detail per cell; reword the request around the target box and generate again.

### Role examples

Give each attached image one job. For a redo, the stone's existing piece is the **edit target and design reference**: it supplies identity, silhouette and the features named for preservation. Its own family supplies motif references. A same-role example below is a **rendering reference only**: borrow camera, broad shading, contour treatment and readable material joins, but not its size in the frame. Borrow its housing geometry only when the brief explicitly selects that geometry. For a new object, define its own geometry before using a role example. The examples were drawn at the old per-role canvases, so their outline widths in pixels differ from the figures here.

| Role | Rendering example | Second rendering example | Draw size | Frame and target box at 512px/cell | Final canvas | Final outline |
| --- | --- | --- | --- | --- | --- | --- |
| Pylon | [Glacite pylon, north](../Textures/Things/Building/Misc/GlacitePylon/GlacitePylon_north.png) | [Vitalite pylon](../Textures/Things/Building/Vitalite/Pylon.png) | (2.2, 2.2), 1×1 footprint | 1024×1536; about 512 wide × 1024 tall | 564×564 | 8–12px, aim for 10 |
| Column | [Glacite column](../Textures/Lumenstone/GlaciteColumn.png) | [Vitalite column](../Textures/Lumenstone/VitaliteColumn.png) | (1.1667, 1.4583) | 1024×1024; 597 × 747 | 298×374 | 8–12px, aim for 10 |
| Cluster | [Vitalite cluster](../Textures/Things/Building/Vitalite/Cluster.png) | [Glacite cooler](../Textures/Things/Building/Misc/GlaciteCooler.png) | (1, 1) | 1024×1024; within 512 × 512 | 256×256 | 8–12px, aim for 10 |
| Floor lamp | [Glacite lamp](../Textures/Things/Building/Misc/GlaciteLamp.png) | [Vitalite lamp](../Textures/Things/Building/Vitalite/Lamp.png) | (1, 1) | 1024×1024; within 512 × 512 | 256×256 | 8–12px, aim for 10; 6 on thin parts |
| Wall lamp | [Vitalite wall lamp](../Textures/Things/Building/Vitalite/WallLamp.png) | [Glacite wall torch, north](../Textures/Things/Building/Misc/WallGlaciteTorch/WallGlaciteTorch_north.png) | (1, 1) | 1024×1024; crystal sized to the cup's cell | 512×512 (shared cup) | 8–16px, aim for 12 |

Pylon art is about one cell wide and two high inside its 2.2 draw size; the box is the art, not the draw size. For any other building, take the box from its draw size and visible art at 512 per cell.

Items are not on the building scale; they follow vanilla's own item sizes:

| Role | Rendering example | Second rendering example | Canvas and draw size | Outline on that canvas |
| --- | --- | --- | --- | --- |
| Resource stack | [Glacite stack of three](../Textures/Things/Item/Resource/Glacite/Glacite_c.png) | [Lumenstone stack](../Textures/Things/Item/Resource/Lumenstone/Lumenstone_c.png) | 128×128, three files `_a`, `_b`, `_c` | 4–6px, aim for 4 |
| Melee weapon | [Glacite blade](../Textures/Things/Item/Equipment/WeaponMelee/GlaciteBlade.png) | — | 256×256, draw 1.2 | 4–6px, aim for 5 |

For another canvas or draw size, the outline tool prints the target in source pixels. Vanilla sprites for the comparison row are in `E:\AI\rimworld-lora\extracted` (PSDs need `psd_tools`).

Always state the outline width in source pixels in the request: 16 for buildings at the common scale. For an item, source outline = final outline ÷ export scale. Measure after export and report the result.

### Decide the stone first

Agree on unresolved design choices before generation; reuse choices already settled by the user:

- **Hue:** one main hue family and any identity-bearing core/accent. Use a 20-degree stack-hue gap as a diagnostic, not proof of distinctness: an accent can dominate the measured mean. Record agreed exceptions; compare the actual pieces instead of increasing accent area to pass the number. Family findings are diagnostic in the current checker.
- **Values:** a pale highlight, a light-to-mid body and one shadow, given as three colours. Keep pixels darker than 0.35 to about 30 percent of the fill or less.
- **Crystal shape:** what makes this stone's crystal different in silhouette, such as slender, blocky, split or twisted.
- **Motif:** one coherent housing motif, expressed as an accent or construction principle (family rule 1).

For a redo, reuse settled decisions and list **retain / replace / borrow** before generation. Retain the stone's recognizable crystal geometry and motif; name the old parts being replaced and the new housing construction. Borrow rendering traits from the role example without inheriting its silhouette or supports. A motif must read in the overall construction at game scale; tiny rivets alone cannot distinguish otherwise identical fixtures. A neutral-silhouette comparison can expose copied geometry, but palette and internal features also contribute to identity.

For repeated structural parts or a recurring perspective defect, establish a top-down plan and a simple perspective blockout before further polishing. Account for the foundation, each assembly, crystal contact and occlusion under one camera. Reuse one component for matching parts where practical; inspect the construction against the brief before using it as generation evidence. Simple objects need no blockout.

### Make each piece

1. **Stacks:** recolour the stone's shared one-, two- and three-piece silhouettes with its three values, in the Lumenstone family's chunk style (family rule 8), with the outline rule's black line inside the existing silhouette. This applies to new and redone stacks; approved stacks keep their line as drawn. No generation. Follow the recipe pattern in `artwork/design-sources/lightstone-resources/recolor.json`.
2. **Floor lamps:** design small freestanding crystal clusters for each stone under the 2026-11-11 direction above; their exact shapes remain to be selected. **Wall lamps:** generate the crystal only, then composite it onto the shared cup, so cup pixels stay identical across stones.
3. **Pylon, column and cluster:** for a redo, attach the stone's piece first as the edit target, its own motif reference when needed, and a role example as a rendering reference. State each image's role in the request. For a new piece, attach the agreed design or layout and a rendering example. Specify the housing geometry explicitly; recolouring another stone's fixture and adding a small accent does not establish a new identity.
4. **Weapons and items:** edit from the weapon example for shading; the silhouette comes from the weapon class (family rules 6 and 7).

Request wording for step 3, filled in per piece:

Use the same wording for every fixture edit, including base, indicator and colour corrections. Take the frame, target box and outline from the [common scale](#common-scale).

> Edit image 1, the [stone/object] design. Retain [identity-bearing silhouette and features]. Replace [named parts] with [explicit construction and motif]. Image 2 supplies [this stone's motif, if attached]. Image [N] supplies rendering only: camera, broad flat facets, neutral housing shading and contour treatment, not size. Image [M] is a scale guide only; do not draw its grid or box. The output is [1024×1024 / 1024×1536] at 512 pixels per game cell. The object is [W×H cells], so draw it about [box] pixels, centred and standing on the ground line, and leave the rest of the frame empty. Size its details for that scale. Keep the design and housing geometry specified here. Use [highlight, body and shadow colours] with [agreed core/accent]. Use a pure black exterior outline 16 source pixels wide, with inter-material lines about half as wide. Keep facets broad and separate them through value changes. No glow, ground shadow, extra ornament or text. Transparent background.

Stay within the brief's call budget. If two attempts fail the same way, stop and change the request or ask the user rather than retrying. Stop when a candidate is good enough to show. Derive masks and additional views after approval.

When the user chooses an image, say whether you are recording it as selected (refining continues) or approved (final), and record it during the same turn using the [current selection procedure](artwork-tooling.md#record-the-current-selection). Preserve earlier decisions as history and name any recipe work still pending.

For a colour-only concept, use a fresh recipe under `work/` with a single `constructionTint: false` source layer and `candidateChecks.materialOwnership: "unresolved"`. Follow [candidate validation](artwork-tooling.md#candidate-validation) for framing and comparison fields. Reuse only the intended canvas/placement settings from an earlier recipe, not its ownership polygons or painted overrides. Record the actual source size, crop repair, uniform scale, padding and anchor. The generated zero mask is a placeholder, not a material partition or tint preview. Material ownership and tint review remain unavailable until a new partition is authored for the selected design.

**Outline.** Every design call still asks for the outline: a pure black exterior line at the calculated source width and inter-material lines about half as wide, as the template says. The model draws its own line, often too thin once the export reduces it, so the width is fixed afterwards with an [outline layer](#outline-layer) on the export, not by another call. Never queue an outline-only edit or ask Codex to thicken, redraw or match outlines (user, 2026-11-14: "we no longer ask codex to do outline adjustments"). The one exception is approved art the layer cannot fix, a line too far under the aim or a coloured edge, when the user allows outline edits for those named pieces (2026-11-15: "Allow outline edits", for the 13 pieces in `work/stack-masters-migration` and `work/outline-redo-migration`). Their queue starts with an `outline-edits:` line so Codex makes the calls, and each result is a new candidate. If a return has no exterior line at all, or broken patches of one, that is a design failure: regenerate it. Measure the alpha-8 crop before export; an object drawn larger than its box forces a stronger reduction, which thins every line, so say "stay inside the box" in each request.

Never run an outline pass either: no thickening inward, of the export or of the full-size source, and no localized outline edits. On the Glacite greatsword (2026-11-11), passes on the export chewed the crystal into black spikes and stair-steps.

**Masks.** A regenerated fixture needs a new mask. For a color-only pilot, wait for the user's candidate selection before creating the mask or queuing its review; otherwise add the exact artwork to the mask review queue before mask handoff. Black stays black under any tint, so outline and material-line pixels may sit on either side of a mask boundary; put the boundary where it is easiest to paint, normally with the line in the housing.

### Check before showing the user

- Look at `game-scale.png` at normal game zoom. Check identity: the candidate should read as its own stone, not the rendering reference recoloured.
- Report the [outline](#measuring-an-outline) and fill dark share (guide: about 30 percent or less) as numbers beside the image, before and after the outline layer. Fix outline width with the layer, never with an outline pass or another call.
- Build a wider sheet with role examples and vanilla only for a new stone or when identity is in doubt.

Mechanical passes do not approve the look. The user approves on the sheet and does one final in-game review at the end.

## Family design rules

They decide what a new or redone piece should be; the sections above decide how it is rendered and made. Differences between stones are welcome in design. Too-similar stones are the failure to avoid, and so is rendering that drifts from the [house style](#house-style).

1. **Motif.** Each stone owns one coherent motif, such as Serenite's horns and hoops, Verdite's leaves and raw stone, or Dreadstone's architectural restraint. It may be one accent or a construction principle expressed by several functional parts. Adapt it to each object's role rather than adding fittings everywhere. Read the current decision from [ARTWORK.md](../ARTWORK.md) and the selected sprites; repeated decorative spikes, studs and crowns do not establish identity.
2. **Style.** The [house style](#house-style) rules set rendering and the [outline rule](#outline-rule) sets outlines. No stone is the standard. The Verdite column's design, outline included, is protected.
3. **Visual weight and detail.** Use the [role examples](#role-examples) as guides, not minimum detail targets. Lamps, stacks and clusters generally need less visual weight than pylons, columns and signature objects. Keep surfaces mostly flat-shaded; do not add bevels, ridges, extrusions or other modeled surface details unless requested. Use only enough broad, simple shading to show form. Add detail when it is visible at normal game zoom and helps communicate construction, material or identity. Facets show form, lighting or perspective; do not subdivide surfaces for decoration. Preserve distinctive, readable silhouettes and motifs. Preserve the accepted collector-aligned Sunstone batteries and Bloodstone batteries; the Bloodstone charge-indicator crystals are untouched.
4. **Hue.** One main hue family per stone, with agreed identity-bearing cores and accents. Aim for distinct palettes; record accepted close-hue choices and judge them with silhouette and motif rather than the stack-hue mean alone. Pale stones need a darker accent value to read on bright terrain. Sunstone is saturated amber, Aegite pale brass or cream with a warm shadow, and Ferrite slate with a copper-red core.
5. **Fixture roles.** A column is straight-sided and architectural. A pylon is a tapering monument on a 1x1 footprint with 1:2 art. A cluster is low and wide. Lamps and wall lamps share the post and cup. Any stone may deviate, as Serenite's 2x1 shrine does.
6. **Weapons and items.** The crystal is the working edge or core. Structure and moving parts, such as bow limbs, hafts and grips, are neutral grey metal, because crystal is too brittle for them. Adapt the stone's motif under rule 1. The silhouette comes from the weapon class, not the stone. Signature items exist only where designed.
7. **Weapon orientation.** Follow the shared guide's [weapon orientation](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#weapon-orientation). Existing weapons are rotated with `tools/rotate_weapon.py`, not redrawn.
8. **Resource stacks.** Every stone's chunks are drawn in one style: the Lumenstone family's (Lumenstone and the light stones made from it). Those stones share the Lumenstone one-, two- and three-piece silhouettes, recoloured for each stone. The Dark Stone family tree (Dark Stone and the stones made from it) shares its own one-, two- and three-piece layout and may keep its own silhouettes, but its chunks match the Lumenstone family's chunks in facet size and value range. Every new or redone chunk, in both families, has the [outline rule](#outline-rule)'s black line; the Lumenstone chunks set the facets and values, and the rule sets the outline. Approved chunks keep their line as drawn. A dark-family chunk that looks like it comes from a different art set is wrong.
9. **Motes and motion.** Follow [stone animations](../.scratch/stone-animations/spec.md): native effecters, large and small tiers, nothing on wall lamps or beds. Each stone has its own mote shape within a shared size band. Glow is never baked into the sprite, and the crystal must read with the effect off. Crystal light and pulse follow the same spec: flipbooks on pylons, obelisks and vessels, additive pulse elsewhere, no auras.
10. **Identity check.** For a new or redone fixture, check its agreed distinctive silhouette, internal features and motif against its own family and any rendering reference. Small ornament and colour changes alone do not distinguish copied housing geometry. Use the optional [silhouette sheets](../.scratch/artwork-review-2026-11/issues/12-silhouette-comparison-sheet.md) when confusion remains; a full-mod sheet is not required for every fixture.

Measured outline thickness, hue distance and feature size can inform `tools/artwork.py check` ([ticket 14](../.scratch/artwork-review-2026-11/issues/14-measured-family-checks.md)). Silhouette, motif and visual weight remain matters of judgment, not quotas.

## Lumen workflow and evidence notes

Start from the [master template](../artwork/master-template.json). The [approval procedure](artwork-tooling.md#record-the-current-selection) explains how an approved master enters the checked registry. Keep a candidate out of production until that acceptance is recorded.

When the user asks for a stone's contact card, use the shared-scale layout: a dark sheet with one labelled panel per object, a consistent one-cell grid, and a gold outline marking each object's draw-size frame. Label footprint, draw size and artwork status in each panel, and include the stone's buildings, resource and equipment where present. Use one pixels-per-cell scale for every panel and keep each sprite's aspect ratio and canvas framing. Mark any estimated framing or draw size, and do not present the card as an in-game view. Make cards only when asked.

Run `python tools/artwork.py check` for registered file and mask validation; add `--preview work/artwork-review/current` for a CPU contact sheet. The [tooling reference](artwork-tooling.md) lists the checks, model limits and optional dependencies.

Use the [staged deployment command](artwork-tooling.md#stage-an-explicit-deployment) for an explicit plan, review diff, baseline checks, backups and installed verification. Prefer selected XML elements when a shared file contains unrelated differences.

For separately generated parts, record a common source square and the baseline bounds scaled to it before the call; the bounds are a framing target, not permission to redesign. Retained sources that predate this keep their recorded transforms. Pilot history, including the Vitalite and Dreadstone examples behind these notes, is in [ARTWORK.md](../ARTWORK.md#guide-history-moved-2026-11-10).

## Project renderer examples

Worked examples from the 2026-10-29 correction, not overrides of current consuming XML:

| Object | Draw size | Footprint | Minimum ratio | Example uiIconScale | Effective scale |
| --- | --- | --- | --- | --- | --- |
| Vitalite pylon | 1.45 × 2.16 | 1 × 1 | 1.45 | 0.62069 | Approximately 0.9 |
| Inlaid bed | 2 × 2 | 1 × 2 | 1 | 1 | 1 |

Directional pylons used the native North default; beds explicitly used South. This project used `Pylon_m.png` for a single graphic and `SlabBed_southm.png` for a directional mask. Check current XML and native lookup before reusing either naming pattern.

The Vitalite pilot's common source frame and the existing Dreadstone/Vitalite transforms remain project choices. Require at least one native source pixel per output pixel after placement and final export; choose a higher task-specific density before generation when needed. The former minimum layer scale of 0.9 remains withdrawn.

Vitalite's rounded caps and rose crystal distinguish it from Glacite. Glacite's downward sparkle was reverted after resembling condensation in-game at zoom and speeds above 1x.

## Sources

- [Project retrospective](retrospectives/2026-10-29-project-retrospective.md), including the twelve-chat source index.
- [Artwork reference map and provenance](../ARTWORK.md).
- [Vitalite artwork history](../VITALITE-ART-PROMPTS.md) and [column artwork history](../COLUMN-ART-PROMPTS.txt).
- Installed primary renderer sources: `Data/Core/Shaders/MaskGate.glsl`, `Map/CutoutComplex.frag.glsl`, `Map/CutoutComplexUI.frag.glsl`; native `BuildableDef.PostLoad/ResolveIcon`, `GenUI.IconDrawScale`, `Designator_Build` and `Command.DrawIcon` in `Assembly-CSharp.dll`.

## Moved general guidance

These headings preserve links from existing tickets. Follow the shared section rather than reading every topic.

## Engine texture constraints

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#engine-texture-constraints).

## Workflow

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#workflow).

### 1. Define the object before generating it

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#1-define-the-object-before-generating-it).

### 2. Approve one master design

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#2-approve-one-master-design).

### Choose ownership polygons or separate layers

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#choose-ownership-polygons-or-separate-layers).

### Evidence still needed

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#evidence-still-needed).

### 3. Derive the other views from that design

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#3-derive-the-other-views-from-that-design).

### 4. Export color and material regions together

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#4-export-color-and-material-regions-together).

### 5. Review at the sizes and paths the game uses

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#5-review-at-the-sizes-and-paths-the-game-uses).

### 6. Deploy the reviewed scope

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#6-deploy-the-reviewed-scope).

## Visual style reference

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#visual-style-reference).

### Numeric framing for separate generation

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#numeric-framing-for-separate-generation).

### Silhouette and detail

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#silhouette-and-detail).

### Materials and shading

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#materials-and-shading).

### Camera and family consistency

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#camera-and-family-consistency).

### Glow and motion

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#glow-and-motion).

## Material-mask encoding

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#material-mask-encoding).

## Renderer and menu icons

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#renderer-and-menu-icons).

### Diagnose the layer before regenerating artwork

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#diagnose-the-layer-before-regenerating-artwork).

### Filter before applying tint in a renderer model

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#filter-before-applying-tint-in-a-renderer-model).

### Preserve the UI material

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#preserve-the-ui-material).

### Calculate scale from the actual native function

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#calculate-scale-from-the-actual-native-function).

### Treat draw ordering as renderer behavior

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#treat-draw-ordering-as-renderer-behavior).

## Prompt scaffold

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#prompt-scaffold).

## Acceptance record

See [shared guidance](E:/Coding/Templates/Steam/rimworld/rimworld-art-guide.md#acceptance-record).
