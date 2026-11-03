# RimWorld object artwork guide

Use this guide when generating or editing object sprites, directional views, material masks, menu icons, draw settings or visual effects for Lumen Stones. It combines the approved artwork with lessons from the project chats reviewed on 2026-10-29. It describes this project's visual target, rather than claiming to be an official Ludeon art specification.

Current user requirements define the object. Approved sprites define its visual language. Historical prompts explain how earlier versions were made; they can contain superseded designs.

## Current furnishing requirements

The [consolidated furnishing specification](../.scratch/layered-artwork-pipeline/spec.md) is the current repair scope. Earlier two-cell pylon placement, pilot-first acceptance and audit-only cluster decisions are historical. Apply these defaults before changing furnishing art or definitions:

- **Placement and artwork:** local pylons occupy 1x1, with tall artwork approximately one wide and two high. Serenite's sanctuary shrine is the explicit exception: a low, non-rotating 2x1 bedroom furnishing that retains `KM_SerenitePylon` and its 45% support category. Record visible alpha bounds, draw scale, padding and base anchor independently of occupied cells. Align feet against the actual placement and recalculate native icons when any framing input changes.
- **Rendering:** resolve inherited drawing modes as well as altitude. Pyrinth uses realtime drawing at the building-on-top altitude, and the current local pylon definitions now declare that same rendering path. Glacite's matching settings still do not prove its northern-neighbour cutoff is fixed. Verify the reported and reversed overlap arrangements in the final human review before choosing any further draw-order intervention.
- **Floor torches:** use fixed wood and a neutral collar, retaining each stone's crystal and effects. Replace selectable torch stuff with fixed wood and account for saved material on existing instances. Preserve the complete user-selected Hollowstone floor lamp. The user approved a smooth Ferrite collar direction for six other torches; their adapted artwork and in-game review are recorded in [ticket 20](../.scratch/artwork-review-2026-11/issues/20-shared-hollowstone-floor-torch-base.md). Keep this policy scoped to floor torches; preserve wall lamps.
- **Protected artwork:** retain the currently successful Verdite column, the selected family identities, the flat Aegite capital and the agreed bed geometry. Record the exact preserved version; a positive review of one object does not approve unrelated masks or views.
- **Material anatomy:** keep crystal/housing joins and openings readable and correctly assigned. A manually traced partition can exclude real housing even when PNG/channel checks pass. Separate unwanted source matte from actual housing and fixed crystal highlights; revise source colour and ownership independently when necessary.
- **Display scale:** judge artwork at normal game zoom. The user handles increased-zoom inspection in the editor; do not require enhanced-zoom reviews. Recover a clean master or repair a boundary when needed for the visible result. Preserve broad facets and restrained contours rather than adding detail.
- **Export:** use one coordinated colour/mask transform from explicit ownership polygons or material layers recorded in one recipe. Declare any source recovery separately. Final export preserves alpha and contours; canvas size alone is not evidence of fidelity.
- **Acceptance:** prepare the complete candidate for one final combined human in-game review. Computer use remains disabled for this effort. Retain passed/failed/pending observations in the existing acceptance checklist and tie decisions to the loaded version. Mechanical checks and CPU models do not approve appearance.

Carry these contracts into the existing registry/checker rather than relying on reminders alone. The checker and staged hook enforce the 1x1/realtime/altitude pylon policy and fixed five-wood floor-torch definitions. The [tooling reference](artwork-tooling.md#current-automation-boundary) lists remaining contract work; these mechanical checks do not validate measured sprite bounds, saved-material conversion or visual results.

## Family design rules

Agreed 2026-11-01 after the artwork review (`.scratch/artwork-review-2026-11/`). They decide what a new or redone piece should be; the sections below decide how to make it. Differences between stones are welcome. Too-similar stones are the failure to avoid.

1. **Motif.** Each stone owns one housing motif, such as Serenite's horns and hoops, Dark Stone's claws and spikes, or Verdite's leaves and raw stone. Every fixture, weapon and item of that stone reuses it. Planned motifs: Sanguinite surgical talons, Dreadstone shackles and spikes, Hollowstone ossuary steps.
2. **Style reference.** Serenite sets outline weight and flat-facet shading. The Verdite column is protected. Measured outline thickness, facets and tonal values for both are in [artwork measurements](artwork-measurements.md).
3. **Visual weight and detail.** Use the Verdite column, Serenite and approved siblings as guides, not minimum detail targets. Lamps, stacks and clusters generally need less visual weight than pylons, columns and signature objects. Keep surfaces mostly flat-shaded; do not add bevels, ridges, extrusions or other modeled surface details unless requested. Use only enough broad, simple shading to show form. Add detail when it is visible at normal game zoom and helps communicate construction, material or identity. Facets show form, lighting or perspective; do not subdivide surfaces for decoration. Preserve distinctive, readable silhouettes and motifs. Preserve the accepted collector-aligned Sunstone batteries and Bloodstone batteries; the Bloodstone charge-indicator crystals are untouched.
4. **Hue.** One hue family per stone, no two stones close in hue, and a darker accent value on pale stones so they read on bright terrain. Sunstone keeps saturated amber, Aegite moves to pale brass or cream with a warm shadow, and Ferrite's core moves to copper-red.
5. **Fixture roles.** A column is straight-sided and architectural. A pylon is a tapering monument on a 1x1 footprint with 1:2 art. A cluster is low and wide. Lamps and wall lamps share the post and cup. Any stone may deviate; Serenite's 2x1 pylon is the first.
6. **Weapons and items.** The crystal is the working edge or core. Structure and moving parts, such as bow limbs, hafts and grips, are neutral grey metal, because crystal is too brittle for them. The stone's motif appears once as an accent. The silhouette comes from the weapon class, not the stone. Signature items exist only where designed; Vitalite, Lumenstone, Dark Stone and Bloodstone have none.
7. **Weapon orientation.** Melee weapons are drawn horizontal with the tip right, and `equippedAngleOffset` is -65 for swung weapons (the [RimWorld wiki](https://rimworldwiki.com/wiki/Modding_Tutorials/Textures) and the installed Pyrinth blade agree). Ranged weapons aim right with the barrel vertically centred. Existing weapons are rotated, not redrawn. Unverified in game; confirm in the final review.
8. **Resource stacks.** Light stones share the Lumenstone one, two and three piece silhouettes, recoloured for each stone. Dark stones share the one, two and three piece layout; their shared silhouette is a separate design decision.
9. **Motes and motion.** Follow [stone animations](../.scratch/stone-animations/spec.md): native effecters, large and small tiers, nothing on wall lamps or beds. Each stone has its own mote shape within a shared size band. Glow is never baked into the sprite, and the crystal must read with the effect off.  Crystal light and pulse follow the same spec: flipbooks on pylons, obelisks and vessels, additive pulse elsewhere, no auras.
10. **Identity check.** If a fixture's identity is uncertain, compare it with relevant siblings; use the optional [silhouette sheets](../.scratch/artwork-review-2026-11/issues/12-silhouette-comparison-sheet.md) when they help. Resolve actual confusion without requiring a full sheet for every fixture.

Measured outline thickness, hue distance and feature size can inform `tools/artwork.py check` ([ticket 14](../.scratch/artwork-review-2026-11/issues/14-measured-family-checks.md)). Silhouette, motif and visual weight remain matters of judgment, not quotas.

## Engine texture constraints

From the [RimWorld wiki texture tutorial](https://rimworldwiki.com/wiki/Modding_Tutorials/Textures) (page marked "Under Review", read 2026-11-02). These are general engine facts, so they sit beneath the project rules above. `tools/artwork.py check` does not enforce any of them yet.

- **Export dimensions:** make final width and height a multiple of 4, ideally a power of two. Source and master images may use other sizes, such as the 1254px Vitalite frame; the rule applies to the exported sprite and mask.
- **Resolution budget:** vanilla uses about 64 pixels per cell, and newer DLC and most mods use about 128. The wiki calls 256 or more excessive, because oversized textures use much more VRAM and the game crashes when VRAM fills. Choose detail for normal game zoom and keep exports within this budget unless the review shows a need.
- **Canvas and draw size:** the wiki matches the canvas to `drawSize` and centres the art, so no draw offset is needed. Our recorded canvas, bounds, draw size and anchor may deviate, as the 1x1 pylon with 1:2 art does. Prefer a centred canvas and record any offset deliberately.
- **Transparent-pixel fringe:** the game compresses textures in memory, and programs that discard colour in fully transparent pixels can leave a white halo. Keep dark colour data under transparent pixels in colour sprites, or add a 1%-opacity black rim outside the contour. Mask bleed is a separate issue, covered under [material-mask encoding](#material-mask-encoding).
- **Outline weight:** the wiki gives about 2px of black outline per 64px of cell resolution. Our rule is to match the approved sibling at displayed size; compare it against the wiki figure when setting a new family's outline.
- **Paths:** all mods and vanilla share one texture namespace, and a duplicate path silently overwrites the earlier one. Our `Things/...` paths are vanilla-style without a mod prefix. Keep filenames distinctive, and check a new path against vanilla and the installed mods before adding it.

## Workflow

### 1. Define the object before generating it

Read the current ticket and select the nearest approved sibling through [ARTWORK.md](../ARTWORK.md#approved-reference-assets). Inspect its color sprite, masks, every direction and consuming XML. Inspect a game screenshot when one is available.

Identify ownership when a reference comes from another mod. Keep any permitted replacement or patch inside this repository's optional integration, with the required attribution; treat the installed upstream mod as a reference rather than an editing destination.

For a new or substantially redesigned object, record a short object contract using the relevant fields below. For a localized edit, a brief note naming the change, protected features and affected views is enough; retain any existing contract.

For simplification, describe each material and surface separately: which shading planes remain to show depth, which surfaces are smooth or rounded, and which crystal facets stay. Name the existing shape, colour and details to preserve. For example, request two broad faces per talon and a continuous round band when those are the intended result. A facet count is a diagnostic, not a substitute for visual judgment.

Start from the [master template](../artwork/master-template.json). The [tooling workflow](artwork-tooling.md#approved-master-for-the-next-artwork-task) explains how a visually accepted master enters the checked registry. Keep the candidate out of production until that acceptance is recorded.

| Property | Record |
| --- | --- |
| Identity | What the object must read as at normal zoom |
| Geometry | Silhouette, parts, exact feature count, relative sizes and spacing |
| Materials | Which parts retain their colors and which accept construction tint |
| Camera | Approved reference, visible top/front/side faces and shallow depth |
| Variants | Required directions and the features shared between them |
| Placement | Footprint, canvas, visible bounds, draw size and anchor |
| Edit scope | Regions that may change and features or pixels that must survive |

For example, the Vitalite inlaid bed has a plain stone slab, a three-crystal headboard and a gold band, with three small crystals on the foot face where visible. Those invariants apply across directions with the appropriate occlusion. A request to clean its edges does not authorize another bed design.

**Done when:** the record is proportionate to the change and identifies what must be preserved across affected views.

### 2. Approve one master design

For a new object, generate a complete representative design first and obtain design feedback before deriving separate material layers from that reference. Preserve the selected source and feedback; concept feedback is distinct from in-game acceptance. Record the [framing contract](#numeric-framing-for-separate-generation) before deriving layers and use the [capture and attempt ledger](artwork-tooling.md#generation-capture-and-attempts) before each call.

Generate one representative view and reduce it to the expected displayed size before producing the full family. Compare it beside the approved sibling, rather than judging the generated image alone.

Use image generation for a new design or an area that needs repainting. For a localized correction, use the editing tools authorized for that task and preserve approved pixels elsewhere. Deterministic mask processing and export operations should preserve the source artwork. Follow the active image-tool instructions for the actual editing mechanism.

Separate reusable features into layers when the source workflow permits it. A bed base, pillow and insets are easier to keep consistent when they have explicit placement and material regions. If the only source is flattened, record what can actually be preserved; a prompt to redraw the whole image does not guarantee pixel preservation.

For a localized edit, compare the result against the approved source outside the allowed region before showing or promoting it. When the contract requires exact preservation, check those pixels and alpha; if they drift, restore the approved region with a declared transform and inspect the join, or keep the edit as an unapproved candidate. Record the comparison and any repair. Visual similarity alone does not establish exact preservation.

After a spatial failure, change the evidence supplied: use a labelled layout reference, isolate the component or derive it from the complete design. Repeating pixel coordinates more emphatically is not a new method. After two attempts repeat the same defect, change the method or reference structure, or stop.

**Done when:** the master meets the contract at game scale, including its silhouette, material identity and required feature count.

### Choose ownership polygons or separate layers

Generate a complete representative design first and compare it beside the approved sibling at the intended display size. Freeze the selected image and record its hash, prompt, references and framing before partitioning it.

For an ownership trial, request neutral fittings and a continuous dark contour where different materials meet. This is a provisional prompt strategy: Dreadstone already had suitable contours before this rule was written. Inspect the actual result before choosing ownership polygons; the prompt does not establish separability.

Record a route for each named join in the object's provenance. Use ownership polygons when visible material ownership is clear, preferably beneath a continuous dark contour. A fitting in front of a crystal can still use polygons when the boundary is clear. Use separate material layers where pixels blend across materials or ownership is ambiguous. Independently generated components must match the frozen design's geometry, camera and lighting.

The Dreadstone cap, collar and plinth demonstrate ownership export on one complete design with clear contours. The original pixels were preserved, the mechanical checks passed and the user approved the CPU previews. Dreadstone has no consuming definition and has not rendered in game. Its integration is deferred to the wider darkstone build; do not create a standalone implementation to validate this artwork workflow.

Vitalite demonstrates a mechanically repeatable separate-layer export. The user liked the new CPU artwork and identified centering and front-lip overlap errors, which were corrected in placement and the bowl partition. Those were authoring errors, not evidence that the generated geometry failed. Two of its four generation calls were lost to capture failure. The corrected candidate's final visual acceptance and in-game review remain pending. This does not establish the cost or reliability of separate generation across objects.

Keep source, ownership or layers, and transform in one export recipe. Export colour and mask together. Fix polygons or layer inputs and re-export. A recipe may use an explicit output-resolution painted mask as its input; the mask editor updates that input when saving a recipe-backed mask. This manual override replaces the polygon-derived mask, so revisit it if the colour source or framing changes. Do not reconstruct invisible surfaces to partition visible pixels.

For separate layers, a coherent generated component may be split deterministically, but verify that the cut follows its actual contour: correct layer order cannot fix pixels assigned to the wrong layer. Named `reviewJoins` previews are available when useful, but are not required. For the existing Vitalite bowl, retain the coherent generated bowl and its corrected deterministic split. Whether independently generated components are worthwhile for another object remains a task-specific decision.

Check material joins and wrapping fixtures at normal display size after changes that affect them. Mechanical checks establish readiness for inspection; they cannot approve anatomy. If a join fails, name the region and defect. Correct misplaced polygons or layer placement first. If the source itself has an ambiguous or mixed join, choose the intervention according to design preservation: before selection, another complete generation may be appropriate; when the selected design must be retained, consider a localized source edit or separate component. There is no established regenerate-first or edit-first rule. Inspect edits against the original to identify changes outside the permitted region; neither editing nor regeneration guarantees preservation. After two attempts repeat a defect, change the method or stop.

### Evidence still needed

Retain the demonstrated controls now: approved sibling references, a frozen selected source, one recipe and shared colour/mask export, explicit ownership or layer roles, reproducibility checks, and named join inspection after changes. Mechanical success and visual acceptance remain separate.

Defer broader method claims until relevant artwork work supplies evidence:

- **Prompt reliability:** use the contour/neutral-fitting request on the next needed single-view object and record actual joins and attempts. One successful result establishes feasibility, not reliability. Do not generate a throwaway object solely to endorse the rule.
- **Directional consistency:** evaluate the next needed directional object against one selected design and its sibling. Neither ownership polygons nor separate layers has been shown here to solve consistency across generated views. Defer a default route for directional generation; retain the per-view contracts and side-by-side review below.
- **Runtime behaviour:** review recipe-built candidates in the normal combined human game review when their feature is ready for integration. Dreadstone specifically waits for the wider darkstone build. CPU previews do not settle shader, enhanced-zoom, placement or native-icon acceptance.
- **Cross-object efficiency:** record calls and corrections during real work. These two pilots do not establish a general cost advantage or justify retiring either route.

No bulk artwork redo, additional generation experiment or gameplay build follows from these open questions alone.

### 3. Derive the other views from that design

Treat directional sprites as views of one physical object. Reuse its parts, proportions and palette. Repaint the visible depth for RimWorld's fixed camera; rotating an entire shaded sprite rotates its lighting and perspective too.

For each direction, compare:

- The same parts and feature count, including partially obscured parts.
- Matching relative sizes and spacing on the object's surface.
- Consistent top, front and side-face depth.
- The same contour weight and large facet structure at displayed size.
- Correct head/foot or wall-facing orientation.

A coordinated generated sheet is a useful reference, but its cells still require individual inspection. The bed sheets in this project changed inset shapes between vertical and horizontal views despite looking coherent as a sheet.

**Done when:** all directions pass the contract side by side, with no new design decisions introduced by a variant.

### 4. Export color and material regions together

Use the established canvas and framing for the object family. Measure visible alpha bounds and preserve the approved aspect ratio. Resize uniformly and use transparent padding to fit the canvas. Apply the same crop, scale and translation to the color sprite and its material-region source.

Preserve smooth antialiasing. Two or three principal shade values do not mean that the PNG must contain only two or three RGB values. Hard palette thresholds, cutout masks and repeated reduction can introduce the jagged edges rejected in the cluster revisions.

Derive masks from declared ownership polygons or explicit material layers. Do not infer ownership from RGB thresholds, warmth rules or palette replacement. Pale crystal highlights, neutral shadows and blended boundary pixels require inspection. Include rear housing visible between crystals as well as the front face. A horizontal cutoff does not describe an irregular bowl or crystal base reliably.

Use the [mask encoding](#material-mask-encoding) below for this renderer. Keep color-sprite transparency separate from mask opacity.

**Done when:** dimensions, placement and filenames agree; intended surfaces are covered; crystals and their contours remain excluded; approved pixels outside the edit scope survive.

### 5. Review at the sizes and paths the game uses

Prepare a contact sheet with all changed views beside approved siblings. Use a recent screenshot to estimate the object's displayed size. A 64/96/128px series is useful for spotting detail loss, but none of those values is a universal RimWorld tile size.

For simplification review, show the current and candidate artwork side by side at the estimated game size, alongside the relevant approved sibling. For a family-wide request, list each affected item as ready, unchanged, rejected or still open. Label unreviewed views and identify any framing or protected-region drift before requesting design feedback.

When the user asks for a stone's contact card, default to the shared-scale layout approved on 2026-11-01: a dark sheet with one labelled panel per object, a consistent one-cell grid, and a gold outline marking each object's draw-size frame. Label the footprint, draw size and artwork status in each panel; include the stone's relevant buildings, resource and equipment where present. Use one common pixels-per-cell scale and preserve each sprite's aspect ratio and canvas framing. Identify any estimated framing or draw size, and distinguish this model from an in-game view. Generate the card when requested; this preference does not require creating cards for every stone in advance.

Run `python tools/artwork.py check` for routine registered file and mask validation. Add `--preview work/artwork-review/current` when a CPU contact sheet would help. The [tooling reference](artwork-tooling.md) lists the checks, model limits and optional upstream dependencies.

At normal displayed size, judge recognition, proportions, feature separation and clutter. The user handles increased-zoom inspection in the editor.

For tintable objects, compare a neutral view, dark steel or another dark material, and a light stone. Review the map sprite and construction-menu icon separately. For moving effects, compare 1x and faster game speeds, both one fixture and a room containing several.

Use the [renderer and icon notes](#renderer-and-menu-icons) for a modeled preview. Label it as a model. A browser animation or CPU composite is not an in-game result.

**Done when:** routine artwork checks pass and the one final combined human in-game review is completed or recorded as pending. Do not launch the game for intermediate checks.

### 6. Deploy the reviewed scope

List the files in the deployment and compare the installed versions with the reviewed baseline. Back them up, copy or patch the intended changes, and verify the installed bytes against the staged candidate.

Use the [staged deployment command](artwork-tooling.md#stage-an-explicit-deployment) for an explicit plan, review diff, baseline checks, backups and installed verification. Prefer selected XML elements when a shared file contains unrelated differences.

Source, staged and installed files can differ when several chats work on the project. If a shared XML file contains an unrelated difference, preserve it while applying the intended field edits. Record the divergence and which candidate was verified. A successful file copy does not establish that the game has reloaded its textures or definitions.

Record the chosen source, edit, export settings, direction, mask encoding and affected definitions in the relevant artwork record. Keep large captures, rejected candidates and deployment backups under ignored `work/`.

Record the image tool and mode actually used. Record a model version only when the tool exposes it; the built-in tool used in these chats did not establish that it was “Imagegen 2.5.”

**Done when:** the deployment scope and hashes are recorded, the reload requirement is clear, and visual acceptance is stated separately from deployment.

## Visual style reference

### Numeric framing for separate generation

Before generating a layer, record a common source canvas, visible bounds for each part, inner rim height/curve and base anchor in source pixels. Put those numbers and reference roles in the request as composition targets. Image generation does not guarantee exact coordinates. Use the exporter for uniform scale and final placement. Whole crystal bases must extend below the rim without moving their visible tips or widening the approved group.

Choose source detail for the intended output rather than enforcing a minimum layer scale. Record the meaningful source bounds, projected visible size and source pixels per output pixel after both placement and final export. Require at least one native source pixel per output pixel; choose a higher task-specific density before generating when close inspection needs it. Any uniform reduction in `(0,1]` is allowed if that detail requirement and final bounds/anchor pass. Enlargement requires regeneration. The former `[0.9,1]` rule was withdrawn after the Dreadstone trial: it was an unsupported proxy for quality. Broad facets, clean contours and visible detail still require human judgment.

For the Vitalite pilot's next generation, use a 1254px common square and the baseline geometry scaled by `1254/512`: full object bounds approximately `(98,132,1188,1036)`, foot anchor y=1036. Derive bowl and crystal sub-bounds from the baseline on that same frame and record them before the call. The overall bounds are a framing target, not permission to redesign the object. The existing retained sources predate this rule and keep their recorded transforms and limitations.

### Silhouette and detail

Make the object recognizable through its outer shape and a few large internal divisions. Keep necessary gaps open after reduction. A rim should read as a rim, a crystal as a solid crystal, and an inset as part of the bed surface.

Use charcoal or black borders on the exterior silhouette and at seams between different materials. Do not draw black borders between areas or pieces of the same material, including facets or overlapping crystals of the same stone. Show form and separation with a few broad light and shadow shapes, spacing or silhouettes instead. A real gap or opening may remain dark as empty space. Match the approved sibling's contour weight at displayed size; a fixed number of source-image pixels is not useful across different export resolutions.

For example, outline a crystal where it meets its metal holder, but shade the crystal's faces without black facet lines. Shade a bent metal talon or a metal band against its metal housing without outlining their same-material surfaces. Separate touching crystals of the same stone through their shapes or shades rather than a black seam.

Concentrate detail where it communicates construction or material. Fine cracks, tiny bevels, grain, many little facets and ornamental fittings usually disappear or create noise at game zoom.

### Materials and shading

Use a small set of principal light, body and shadow values for each material. Preserve the approved reference's antialiasing and modest tonal variation; avoid adding photographic lighting or texture to make a large preview impressive.

Crystal identity comes from angular silhouette breaks and a few broad, uneven planes. Rough crystal can be irregular without becoming pixel noise. Broad warm faces should still read as solid planes; a smooth taper with flame-like red/yellow shading can make Pyrinth read as fire. Ticket 06 records that deferred issue and does not authorize repainting upstream assets.

Give constructed housings enough structure to survive material tint: a distinct rim, shallow front wall and broad shading can establish an open bowl. The approved cluster uses a symmetrical scoop to expose more crystal while retaining a readable container.

### Camera and family consistency

Match the approved sibling's fixed camera and shallow visible depth. Keep facing surfaces consistent across directions. A tall object is not a front-view illustration placed onto a map, and a horizontal view is not automatically a rotated vertical painting.

Share contour weight, lighting, detail density and fitting proportions across a family. Keep meaningful differences: Vitalite's rounded caps and rose crystal distinguish it from Glacite without requiring a different rendering style. A deliberately more crystalline full bed can differ from the cheaper stone bed while sharing the same camera and footprint.

### Glow and motion

Keep environmental glow and animated particles separate from the solid object unless a baked effect is explicitly part of the approved design. Crystals must remain readable when the effect is absent.

Judge motion in the game context. Glacite's downward sparkle concept was approved in a browser preview and later reverted because it resembled condensation at game zoom and speeds above 1x. Tune particle size, density, travel and lifetime against that context rather than the large preview alone.

## Material-mask encoding

These technical notes are verified against the installed RimWorld 1.7/Waylith renderer on 2026-10-29. Recheck the installed shaders and native methods when changing renderer versions.

For the single construction-tint channel used by the repaired bed and cluster masks:

| Region | Red | Green/blue | Alpha |
| --- | --- | --- | --- |
| Full construction-material coverage | 255 | 0 | 255 |
| Crystal face, protected contour or opening | 0 | 0 | 255 |
| Antialiased material boundary | Fractional coverage, 1–254 | 0 | 255 |

Opaque black in the mask means no construction tint. It does not make the color sprite opaque. The color sprite keeps its own genuine transparency.

The installed `Data/Core/Shaders/MaskGate.glsl` documents RGB bleed into fully transparent texels. `CutoutComplex.frag.glsl` independently samples the color and mask, then applies:

```text
gate = clamp(sampledMaskAlpha * 255, 0, 1)
weight = sampledMaskRed * gate
output = sampledColor * (1 - weight + weight * constructionColor)
```

Transparent crystal cutouts can acquire red RGB during texture loading. At a filtered boundary, a small nonzero alpha can make `gate` equal one, producing excessive tint. Store the exclusion as opaque black and the antialiased region coverage in red instead.

For the cluster's legacy mask, coverage was stored in alpha. Its repair used `round(oldRed * clamp(oldMaskAlpha / colorAlpha, 0, 1))` for new red, zero where color alpha was zero, and alpha 255. That conversion is specific to that legacy encoding. Inspect how another mask encodes coverage before converting it; applying coverage twice can weaken the boundary.

Follow the consuming graphic's naming convention. This project uses `Pylon_m.png` for a single graphic and `SlabBed_southm.png` for a directional mask. Match the actual native lookup rather than guessing an underscore pattern.

## Renderer and menu icons

### Diagnose the layer before regenerating artwork

| Symptom | Compare first | Likely place to change |
| --- | --- | --- |
| Broken contour also visible without tint | Raw color sprite beside tinted output | Local source-outline repair |
| Fringe or crystal darkening appears under material tint | Region selection, encoded channels and independently filtered mask | Mask boundary or encoding |
| Crystal is bright on the map but dim throughout its menu icon | Native icon material versus plain icon override | UI definition/rendering path |
| Correct source becomes compressed in game | Export aspect, padding, draw size and footprint | Export or display proportions |
| Features change between rotations | All directions against the approved master | Variant design/placement |
| Trees or wall attachments cover an object unexpectedly | Native layers, mesh depth and reversed object positions | Draw ordering |

A larger canvas alone does not repair a wrong contour, missing material region or incorrect rendering path. Change the layer supported by the comparison.

### Filter before applying tint in a renderer model

A full-resolution CPU composite followed by downsampling can hide the failure seen in game. For a renderer model, sample the color and mask independently at the target size, then apply the installed shader equations. Account for the loader behavior being investigated. State any modeled bleed radius or filter approximation; it is not proof of the exact GPU output.

### Preserve the UI material

In this installed version, an explicit `uiIconPath` takes the plain-texture branch of `BuildableDef.PostLoad`, bypassing `ResolveIcon` and its mask-aware UI material. That caused steel to dim whole crystals in the pylon and bed menu icons.

Prefer the native graphic-derived icon for a tintable object when its framing can be retained. If a dedicated icon is necessary, verify how its matching mask and UI material are supplied by the target runtime. A mask file beside a custom icon is not proof that the menu uses it. Fully crystalline, untinted objects can legitimately use a plain icon.

### Calculate scale from the actual native function

Without an explicit icon path, the installed `GenUI.IconDrawScale` uses:

```text
effectiveScale = uiIconScale * min(drawSize.x / footprint.x,
                                  drawSize.y / footprint.z)
```

`Command.DrawIcon` applies a further factor of `0.85` for build commands. The drawing routine also fits the texture to the icon rectangle using its graphic proportions. Other icon call sites need their own path checked.

| Object | Draw size | Footprint | Minimum ratio | Example uiIconScale | Effective scale |
| --- | --- | --- | --- | --- | --- |
| Vitalite pylon | 1.45 × 2.16 | 1 × 1 | 1.45 | 0.62069 | Approximately 0.9 |
| Inlaid bed | 2 × 2 | 1 × 2 | 1 | 1 | 1 |

Using the larger ratio made these icons too small. Compare visible alpha bounds after the full fitting calculation, including padding and offsets. The directional pylons use the native North default; the beds explicitly use South. Check `defaultPlacingRot` rather than assuming which mask supplies an icon.

These are worked examples from the 2026-10-29 correction. The consuming XML owns the current settings.

### Treat draw ordering as renderer behavior

Start with comparable vanilla definitions and inspect inherited layers, footprints, draw offsets and native mesh behavior. A uniform altitude layer is not a complete screen-position sorting algorithm. Tree meshes and tall furniture can still overlap unexpectedly.

Use the reported arrangement and its reversed foreground/background positions to judge a change. Raising every tall object can solve one overlap while making it cover a tree in front. Record that priority tradeoff when it is the chosen behavior. Keep world draw ordering separate from icon scale and from painted perspective.

## Prompt scaffold

Supply the actual reference images and fill only the fields needed for the object:

> Production RimWorld object sprite matching reference A's fixed camera, charcoal contour weight, broad material shading and detail density at game zoom. Reference B controls [shape]; reference C controls [palette]. Draw [object and exact parts/counts]. Preserve [silhouette, relative sizes, spacing and material boundaries]. Change only [requested region/properties]. Use [principal material colors] with a few broad readable planes and smooth antialiasing. Use dark borders only on the exterior and between different materials; shade same-material surfaces without black seams or facet lines. [Direction] view of the same approved object; repaint facing depth for the fixed camera. Genuine transparent background, fully visible object, [established canvas and framing].

For a localized edit, add the protected regions and permitted newly exposed surfaces. For an existing design, describe rough crystal geometry rather than repeatedly asking for “more detail” or “more crystalline.” Keep the accepted prompt and selected result in the artwork record.

## Acceptance record

Keep these states distinct in the ticket or delivery note:

| State | Evidence |
| --- | --- |
| Design approved | Contract and chosen master/reference |
| Export inspected | Direction sheet, game-size comparison, masks and mechanical checks |
| Deployed | Exact file list, staged candidate and installed hashes, backup |
| In-game confirmed | Reloaded screenshot or observation for the reported case |

A user saying the result is “much better” confirms improvement in the shown case. It does not verify every rotation and material. If game access is unavailable or UI automation is declined, finish the authorized export/deployment work and leave the remaining visual check explicit. Honor the active request's limits on testing, saves and computer use.

## Sources

- [Project retrospective](retrospectives/2026-10-29-project-retrospective.md), including the twelve-chat source index.
- [Artwork reference map and provenance](../ARTWORK.md).
- [Vitalite artwork history](../VITALITE-ART-PROMPTS.md) and [column artwork history](../COLUMN-ART-PROMPTS.txt).
- Installed primary renderer sources: `Data/Core/Shaders/MaskGate.glsl`, `Map/CutoutComplex.frag.glsl`, `Map/CutoutComplexUI.frag.glsl`; native `BuildableDef.PostLoad/ResolveIcon`, `GenUI.IconDrawScale`, `Designator_Build` and `Command.DrawIcon` in `Assembly-CSharp.dll`.
