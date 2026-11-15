# Artwork checks and deployment

Use [the art guide](rimworld-art-guide.md) for design and visual acceptance. These commands enforce the mechanical parts of that workflow. They edit no sprite artwork or game settings while checking.

## Current automation boundary

The [current furnishing requirements](rimworld-art-guide.md#current-furnishing-requirements) and [consolidated specification](../.scratch/layered-artwork-pipeline/spec.md) supersede earlier pylon placement and torch-material assumptions. The checker validates declared assets/encoding, historical master hashes, consuming XML and native icon calculations; it also enforces the six registered pylons' 1x1 footprint, realtime drawing and BuildingOnTop altitude, plus all six floor torches' fixed five-wood construction. These checks resolve inherited definition fields and run against the Git index in the pre-commit hook. Deployment verifies source, baseline and candidate bytes with backups. None of these checks establishes correct material anatomy, close-zoom fidelity or observed draw ordering.

Serenite's low sanctuary shrine is a recorded 2x1 exception in `pylonFootprintExceptions`; realtime drawing and BuildingOnTop altitude still apply. The other registered pylons retain their one-cell policy.

Remaining implementation work is to extend the same checker/registry/export path with declared material roles, effective visible-detail targets, meaningful alpha bounds, base anchors, protected source-region contributions and framing dependencies. Check the exact candidate and its consuming definitions; passing against unchanged historical production assets cannot approve separate exports under work/. Reconcile retired torch masks and unused UI paths against actual consumers. The current contracts do not record measured visible bounds or prove saved-material conversion for already-built torches.

Keep automatic enforcement and human acceptance distinct. Guardrails can reject a mismatched transform or a violated declared material region, but cannot infer correct ownership from grey/bright pixels or approve a bad traced boundary. The final human game review remains necessary.

## Python setup

### Artwork preflight

Before starting an artwork task:

1. Inspect `git branch --show-current` and `git status --short`. Record the task's permitted paths and baseline commit. Coordinate exclusive editing of those paths with other active chats; recheck the branch and index before committing. If another chat needs the same files or index, serialize the work or use an authorized separate worktree.
2. Resolve the interpreter before running image commands: `$artPython = git config --get lumen.artworkPython`, then `& $artPython -c "from PIL import Image; print(Image.__version__)"`. If configuration is absent, select a Python with Pillow and run the hook installer below. Use that interpreter consistently for checks, exports and tests.
3. Open the specific task or artwork ticket first, then the current furnishing requirements and the selected object's provenance. Search within those files using section names or narrow patterns; follow additional links only for the active operation. Bound each read so required sections are returned intact, and retrieve any omitted text before proceeding. Avoid repository-wide content searches to answer a question the ticket already locates.
4. Follow the art guide's [three steps](rimworld-art-guide.md#the-three-steps). Capture and attempt recording is optional, used only when a brief asks.

Use Python 3.12 or later with the development-only image dependency:

```powershell
python -m pip install -r tools/requirements-artwork.txt
```

Codex's bundled Python already has the required Pillow version on this workstation. Use its full executable path in place of `python` if the shell's default Python differs. The hook installer records the chosen executable in local Git configuration; no personal runtime path is committed.

## Shared mask tooling

Editor, gallery, review queue operations and recipe export now use `E:/Coding/Rimworld/RimWorldMaskEditor`. Existing `tools/mask-editor.py`, `mask_gallery.py`, `mask_review.py` and `export-layered-artwork.py` are compatibility entry points. Artwork, recipes, review history and Lumen-only historical gallery rules remain in this repository; the latter are in `artwork/mask-editor.json`. For another mod use the shared editor's `--project` option and its [mask guide](E:/Coding/Templates/Steam/rimworld/mask-editor-guide.md).

## Paint an existing mask

Open **Artwork gallery** in the editor to browse labelled mod textures and design sources. Items marked **FOR MASK REVIEW** appear first, with recently modified artwork first within each group; use the **For review** or **Reviewed** filters to find them. Confirmed superseded artwork is hidden from the gallery but retained on disk. Search by name or path, or filter between In mod and Designs. A recorded approved image is labelled only when it matches the registry hash; that label does not approve its mask. Select a texture to open its mask when one exists. Painting and selection are paused for one second after opening an item. A design without a matching current export shows its first source layer in the gallery; selecting it exports its colour and mask to a fresh directory under `work/` before opening them.

### Mask review queue

When handing a mask to the user for review or manual finishing, mark the exact colour PNG or recipe before handoff. Use a recipe for recipe-built artwork so the gallery opens a reproducible export; use the colour PNG for an existing non-recipe texture. Add a short note about what needs attention:

```powershell
& $artPython tools/mask_review.py add artwork/design-sources/OBJECT/master.json --note 'Inspect the housing edge'
& $artPython tools/mask_review.py add Textures/Things/Building/Misc/GlaciteCooler.png --note 'Inspect the housing edge'
& $artPython tools/mask_review.py list
```

For a bulk review, `mask_review.py sheet [--current] [--output work/NAME.png]` renders every pending mask as colour, mask, dark tint and light tint rows (the editor's tint values). When the user approves from the sheet, record it in their words with `mask_review.py approve-all --words '<their words>' [--except PATH ...]`; it saves each review against the mask as installed (changed pixels 0), exactly as an editor save would.

For a colour PNG with a nonstandard mask name, add `--mask path/to/mask.png`. Review items may be under `work/`; candidate exports there are not production textures. The gallery also offers **Mark for review** and **Clear request** buttons; clearing a new request preserves any earlier saved reviews. Saving a mask with either Save or Ctrl+S records it as **MASK REVIEWED**. Each save records its time, image and mask paths, previous and saved mask hashes, changed-pixel count when available, and whether a recipe was updated. Before-and-after mask snapshots are kept under ignored `work/mask-review-history/`. Records persist in `artwork/mask-reviews.json`; use `list` to inspect them before continuing the user's work. A saved review is not design or in-game approval. `clear PATH` removes the complete record, so preserve reviewed records unless removal is requested.

The gallery labels queued artwork as **current approved**, **current; approval hash differs**, or **candidate or historical** using the registry and approved master hash. **Current review** filters pending current approved artwork; **For review** still includes candidates. Unregistered images and work copies are not assumed to be superseded: compare their source and review records before clearing a request. Use `python tools/mask_review.py list --current` for the same filter. These labels do not establish mask or in-game approval.

Before promoting a saved mask, reconcile reviews by object, source and mask hashes. Preserve every saved snapshot; if copies have divergent edits, resolve them deliberately instead of merging or overwriting them silently. For a reviewed current recipe, preview and then apply its mask to every registered direction:

```powershell
& $artPython tools/mask_review.py promote artwork/design-sources/OBJECT/master.json
& $artPython tools/mask_review.py promote artwork/design-sources/OBJECT/master.json --apply
```

The plan reports each runtime path and its before/after SHA-256. It requires the current approved master, the latest saved mask snapshot, an unchanged recipe mask and a saved colour hash matching both the fresh export and production colour. It refuses unrelated runtime-mask changes. Older reviews without a colour hash need a fresh save before this command can verify them. After application the command verifies written hashes; run the artwork check before deployment. Directional recipes are promoted separately. This updates project textures, not an installed mod.

### Editor controls

Run `& (git config --get lumen.artworkPython) tools/mask-editor.py` in PowerShell. Open the colour image; its matching mask opens automatically when present, or a blank mask is created when none exists. Paint adds construction tint and Erase removes it; the red overlay shows coverage. Hold Shift while brushing to temporarily switch between paint and erase. Choose a round or square brush and set its size in image pixels; Ctrl+mouse wheel adjusts its size. Zoom with the mouse wheel and drag with the middle mouse button to pan. Use Ctrl+Z to undo and Ctrl+Shift+Z to redo, up to 100 changes. Ctrl+S saves to the opened mask; a new mask prompts for a PNG filename, suggesting the matching name next to the image.

Choose **Outline brush** to paint black directly onto the colour artwork, or **Outline line** to drag a straight black line. Brush size, shape, opacity, hardness and the current selection also apply to outlines. Save writes colour edits to the opened image as well as the mask. For recipe-backed artwork it stores an output-resolution `paintedColor` input, so later exports retain the edits. This replaces the generated colour output; revisit or remove it if source layers or framing change. Work-directory candidates still need promotion to the production textures after review.

Outline strokes use smoothed edges; lower hardness produces a softer falloff. While dragging a straight line, the dotted guide shows its path; the finished stroke appears on release.

To open an exact candidate for a handoff, pass the image and mask as separate quoted arguments using PowerShell's call operator. Run from the repository root; leave the editor process running for the user:

```powershell
& (git config --get lumen.artworkPython) 'tools/mask-editor.py' 'work/path with spaces/image.png' 'work/path with spaces/image_m.png'
```

For a recipe-backed candidate, open the queued recipe through **Artwork gallery** instead, so saving updates its recipe and review record. Launch the editor as a visible window; do not use `-WindowStyle Hidden`, because that hides the editor itself. When launching through `Start-Process`, quote each argument inside the `-ArgumentList` string as well: PowerShell joins that list into a command line and otherwise splits paths containing spaces.

For straight-edged selections, choose Line selection, click each corner, then click the first point to close the area. Enter or Close selection also closes it. For freehand selections, choose Pen selection and drag the outline; releasing closes it. Hold Shift when starting another outline to add to the selection, or Alt to subtract from it. The boundary marks the selected area. Brush strokes affect only the selection until it is cleared. Ctrl+I inverts the selection. Use Fill selection to add coverage, or Erase selection or Delete to remove it. Escape clears the selection, and Undo restores the previous mask.

Press P to switch between the mask and a light tint preview. Press Shift+P to cycle through light, dark, wood and mask views. The previews model construction tint on the saved sprite; they are not in-game renders.

Brush opacity sets the maximum strength of a stroke; hardness sets how much of the round or square tip has full strength before fading at the edge. Separate strokes build up or erase coverage gradually. At 100% opacity and hardness, the brush paints or erases fully.

The editor accepts opaque-red masks only and preserves unpainted fractional red coverage. It does not convert legacy masks. When a recipe-backed mask is opened through the gallery or is linked in `artwork/objects.json`, saving to that mask also creates or updates the recipe's output-resolution `paintedMask` input. Save As to a different path creates a separate mask and does not update the recipe. Candidate exports under `work/` are not automatically promoted to `Textures/`. Check the mask at game scale and follow the art guide's review and deployment steps.

## Edit the altar mask in PhotoCraft

Create a fresh layered document and open PhotoCraft:

```powershell
& (git config --get lumen.artworkPython) tools/photocraft-mask.py open work/altar-edit.ora
```

Select `MASK - paint red, erase to remove`. Paint with an opaque red brush to add coverage and erase to remove coverage. The layer's opacity makes it translucent over the colour reference; coverage is stored in the layer's pixel alpha, not its colour or layer opacity. Keep the layer name and save the document as OpenRaster (`.ora`). Keep the launcher window open: it detects saves and updates the mod mask and recipe. The script refuses to overwrite an existing document; choose a fresh name for a new session.

To reopen an existing document and watch for saves, use `watch` instead of `open`:

```powershell
& (git config --get lumen.artworkPython) tools/photocraft-mask.py watch work/altar-edit.ora
```

To import a saved document separately when the watcher is not running:

```powershell
& (git config --get lumen.artworkPython) tools/photocraft-mask.py import work/altar-edit.ora
```

Import updates both the production mask and its recipe input. It refuses to overwrite the mask if the source image or mask changed since the document was created or last imported. Keep the adjacent JSON file with the document. Save as OpenRaster rather than a PhotoCraft project or flattened PNG so the mask layer remains available to import.

## Check artwork and generate a review sheet

```powershell
python tools/artwork.py check --preview work/artwork-review/current
```

The command checks [artwork/objects.json](../artwork/objects.json): declared dimensions and directions, matching masks, opaque-red encoding where required, approved-master hashes, consuming XML graphics, native masked icons and approved visible-height ranges. It parses runtime XML in Defs, Patches and Integrations. Registered aliases must match their corresponding south color/mask exports.

The six initial records cover both Vitalite beds, the restorative cluster, the Glacite/Vitalite pylons and optional upstream Pyrinth. Unregistered art is outside the asset checks. Legacy masks remain explicitly distinguished from the repaired opaque-red masks; the command does not silently convert or repaint them.

Optional `--game-root` and `--pyrinth-root` arguments override installation discovery. The game's default location comes from Source/LumenStones.csproj; Pyrinth is sought in the adjacent Steam Workshop tree. When installed, shader and assembly hashes must match the recorded renderer profile, and Pyrinth's registered patch targets must exist. Without those installations, the portable checks still run and report which runtime/upstream checks were skipped.

The contact sheet includes every registered view and a separate native build-icon row under neutral, steel and light sandstone tints. Color/mask channels are sampled independently with the installed shader equations, native minimum scale ratio, graphic proportions, default direction, offsets and the build-command 0.85 factor. A four-source-pixel transparent-RGB extension is a controlled approximation of the documented loader behavior. The report records this limitation; the sheet is not a GPU capture or a world-camera simulation. Mask-region quality, exact feature counts, perspective, motion and visual acceptance remain human checks.

The checker returns a nonzero exit code for failures. It writes the sheet and JSON model report only after the checks pass.

### Family progression cards

```powershell
& (git config --get lumen.artworkPython) tools/family_cards.py [Family ...]
```

This writes one card per stone family to `work/contact-sheets/<family>-fixtures.png`. Each piece shows its art from before the 2026-11-12 refresh, the design step before its final design, and the current texture. Outline-only commits are not steps, so a piece redesigned once shows only original and final. When a later commit only changes outlines, add it to `OUTLINE_ONLY` in the script. The cards are rendered with the shared contact-sheet tool from the Templates checkout beside this workspace.

## Layered colour and material exports

Use `python tools/export-layered-artwork.py SOURCE/master.json --output work/ARTWORK/export` for an explicit bottom-to-top RGBA layer stack. The manifest declares each layer's construction-tint ownership, a source crop, output canvas and target box, plus `sourceRepair` and `alphaHandling`. Colour and visible housing contribution share one uniform transform and reduction; fixed layers occlude housing before its red-channel coverage is exported. Masks have opaque alpha and zero green/blue. The exporter rejects undeclared source/alpha handling and records both declarations in `export.json`; it does not remove backgrounds, expand contours or sharpen alpha.

Keep original masters, semantic ownership and editable layers together. Any source repair or crop that excludes generated speckles must be declared in the object's provenance. A fixed-colour object needs no runtime mask. The [Serenite pilot](../artwork/serenite-column-layered-pilot.md) records the first source stack and pending visual decision. Exports and hashes do not establish in-game acceptance; the current layered-artwork effort defers that decision to the user's final combined test.

The altar recipe declares `paintedMask`, an opaque-red PNG relative to the manifest, at output canvas dimensions. It replaces the generated mask after the shared colour transform; its hash is recorded in `export.json`. The existing polygons and their diagnostic evidence describe the earlier traced partition, not subsequent painted edits. Reinspect the painted mask whenever the colour source, crop, canvas or placement changes.

Per-layer `offset: [x,y]` is the top-left translation in source-canvas pixels, after uniform `scale` (default 1). Placement defaults to `[0,0]`. Resizing uses Lanczos; colour and contribution both consume the positioned RGBA layer. Without placement fields the existing same-canvas requirement and pixels are preserved. A scale greater than 1 is rejected.

### Ownership recipes and production reproducibility

Each layer declares exactly one of `constructionTint` (the existing whole-layer role) or `ownership` (a nonempty mapping of named polygons). Polygon coordinates are integer pixels in the original layer image before placement. Each polygon needs at least three points within the source bounds and nonzero area. Overlapping regions form a union. Integer offsets are supported; ownership layers currently require scale 1. The common final crop and reduction still apply to both colour and coverage.

```json
{"path": "source.png", "ownership": {"cap": [[10,10], [90,10], [90,30], [10,30]]}}
```

The exporter writes inspection maps under `work/` when exporting there. These maps are derived evidence, never another editable authority. Keep source, manifest and concise provenance durable; keep maps, exports, previews and reports under ignored `work/`. Follow the art guide's [conditional route choice](rimworld-art-guide.md#choose-ownership-polygons-or-separate-layers) and [evidence limits](rimworld-art-guide.md#evidence-still-needed). Exporter support does not establish prompt reliability, directional consistency or runtime acceptance.

When an accepted recipe-built texture is integrated into production, add an optional `recipes` mapping to its object in `artwork/objects.json`. Keys are the exact views yielded by `object_views`: `single` for an undirected object, otherwise `north`, `east`, `south` and `west`. Values are repository-relative manifest paths, for example `"recipes": {"single": "artwork/design-sources/OBJECT/master.json"}`. Each linked recipe must reproduce that view's colour and, when consumed, mask.

`artwork.py check` exports each linked recipe afresh and compares decoded RGBA pixels and dimensions with the registered textures. Different PNG compression is allowed; edited colour or mask pixels fail. Unlinked views report `no recipe`, which is not reproducibility verification. Candidate recipes remain unlinked until production integration is authorized. The pre-commit hook runs the same comparison using only the staged snapshot's recipes, sources and textures.

### Generation capture and attempts

Optional: use this only when a brief asks for an attempt record. Before each image call, save its exact tool arguments as `work/TASK/request.json`, using saved `referenced_image_paths`. The ledger hashes those references. Record the request before spending the call:

```powershell
& $artPython tools/artwork-attempt.py begin --ledger work/TASK/attempts --layer bowl --request work/TASK/request.json --rendering-recipe work/TASK/master.json
```

For fixture calls, `begin` calculates the final-canvas border width and its proportion of the framed object height from the recipe, appends the rendering contract to the prompt and saves the updated exact request. Read that saved JSON after `begin` and pass its arguments to imagegen. Repeat this on every correction; the previous appended contract is replaced rather than duplicated. `--non-fixture` is the explicit alternative for captures outside fixture rendering. Capture changes the request only; it never paints, thickens or otherwise edits an image.

For the built-in tool, return its result through `generatedImage(result)` to display it and obtain the local saved path. Copy that local PNG with the ledger's finish command; base64 is unnecessary. If capture fails, recover the existing result before making another generation call. Record human-readable inspection findings and a stable defect identifier when a defect remains:

```powershell
& $artPython tools/artwork-attempt.py finish work/TASK/attempts/attempt-0001.json --image 'C:/path/returned-by-tool.png' --finding 'Three whole points; bases extend below rim'
```

`finish` retains the original bytes and records their hash, size, alpha histogram and colour-spread evidence. A pending attempt blocks another call on that layer. After two inspected attempts share the same nonempty `--defect`, either stop or describe the actual new method using `begin --method-change '...'`. This is a declared authoring change, not automatic visual approval. Keep rejected images and the full ledger under ignored `work/`; promote the relevant exact requests and records with selected sources. Never reconstruct missing historical attempts as if they were captured.

If the tool fails without a recoverable image, close the attempt with `artwork-attempt.py fail ATTEMPT.json --reason 'exact tool failure'`. This preserves the failed call in the cost record and permits a retry. Recover a returned image before using this escape path. Two consecutive generation failures also require a method change or stopping.

### Candidate validation

Export, check and write labelled neutral/brown/dark CPU views in one operation:

```powershell
& $artPython tools/artwork.py prepare-candidate artwork/design-sources/dreadstone-column/master.json --output work/dreadstone-column/review
```

For separate-layer recipes, declare `reviewJoins` as named `[left, top, right, bottom]` rectangles in the common source canvas, **after layer placement and before the shared crop/reduction**. Right and bottom are exclusive. Use integer coordinates inside `sourceSize`; names may contain letters, numbers, hyphens and underscores. These regions select previews only and never alter colour, alpha, ownership or layer order.

```json
"reviewJoins": {
  "front-lip-left": [280,585,510,780],
  "front-lip-right": [750,585,990,780]
}
```

List names in `candidateChecks.requiredReviewJoins` to make them mandatory: a required name missing from `reviewJoins` is a mechanical failure, not a warning.

Both `prepare-candidate` and `check-candidate` generate labelled 3x nearest-neighbour join views under neutral, brown and dark tints, with 12 output pixels of context padding. `candidate-check.json` lists their paths and output bounds in `joinViews`. Ownership polygon regions retain their automatic join views. A multi-layer recipe with neither kind of region warns that named joins are missing. Reinspect these views after any split, placement, scale or framing change; a passing report does not verify correct front/rear assignment.

The output must be a directory inside `work/`. Named ownership regions also receive zoomed tinted join views. The command returns nonzero on a mechanical failure and leaves the exports and `candidate-check.json` for diagnosis. It does not promote or deploy the candidate. Separate export and check commands remain available:

```powershell
& $artPython tools/artwork.py check-candidate artwork/layered-sources/vitalite-cluster-generated/master.json --output work/generated-layer-pilot/export
```

The command checks an existing export directory without rewriting its images. It exports the manifest twice into temporary directories under `work/`, checks both hashes, and compares the saved colour and mask byte for byte with fresh output. It checks opaque mask alpha, zero green/blue, source dimensions, placement scales, tinted-layer neutrality, baseline hashes, canvas, visible bounds, foot line and the consuming definition's draw size. Exit code 1 means a mechanical failure; `candidate-check.json` is written beside the exports with the manifest and source hashes and every measured failure.

Use `candidateChecks.mode: "new-design"` for a design with no approved baseline. Provide output-pixel `expectedBounds`, `boundsTolerance` and an exclusive-bottom `footLine` anchor. Baseline comparison is explicitly unavailable. Omit `definition` and `drawSize` until a real consumer exists; the report then marks runtime placement unavailable. A supplied definition must still pass its draw-size check. Replacement mode remains the default and requires baseline hashes and a consuming definition. Neither mode permits missing inputs to count as a completed comparison.

For a flattened concept whose material ownership is unresolved, declare `materialOwnership: "unresolved"` and keep the source fixed-colour in the manifest. The exporter's zero mask can be checked for encoding and repeatability, but the report explicitly marks material ownership unavailable; it is not a fixture/crystal partition and must not be shipped as one.

Specifically, use `layers: [{"path": "source.png", "constructionTint": false}]` and put `materialOwnership` inside `candidateChecks`. Start without old `ownership`, `paintedMask` or `paintedColor` fields. A placeholder filename or prose warning does not make stale ownership valid. For a redesign of an existing object, keep replacement-mode baseline/consumer checks; `new-design` is for an object without an approved baseline. Describe changed framing as an intended design change before exporting, rather than fitting expected bounds to a surprising result.

The manifest's `candidateChecks` declares:

- `baseline.color` and `baseline.mask`: each has a repository-relative `path` and expected `sha256`.
- `definition` and `drawSize`: the consuming ThingDef and expected `[width,height]`.
- `boundsTolerance`, `maxOpaqueSpread`, `minSourceSize`, `minSourcePixelsPerOutputPixel`: explicit task limits. Use `minSourceSize: 1` and density at least 1 unless the task justifies a higher requirement before generation. Density is `1 / (layerScale * exportScale)`; it includes both uniform reductions. A short portrait-canvas dimension alone does not establish inadequate source detail. Meaningful source and projected visible sizes are reported at alpha above 8 as diagnostics; these sizes do not prove visual detail or correct material ownership.

For ownership layers, the neutrality blocker uses alpha-weighted mean RGB spread only inside the declared polygons, limited by `maxOpaqueSpread`. The exactly-255 sample is evidence only. Whole-layer `constructionTint` stacks retain their existing opaque-spread gate.

The opaque spread gate reports its sample count and fraction of visible pixels. Alpha histograms and alpha-weighted mean spread supplement it; a small exactly-255 sample does not establish neutrality of the remaining surface. An empty opaque sample fails the literal opaque-spread gate. These measurements never alter source alpha, colour or mask.

`passed` and `mechanicalPassed` mean no export/placement/preservation errors. `outline` measures the actual export using `candidateChecks.drawSize` and `outlineKind` (default `building`; use `weapon` or `resource` for those assets). `outlinePassed` reports whether it meets the rule; it is shown to the user and never blocks anything. `readyForTintReview` additionally requires resolved material ownership. `readyForProduction` requires those mechanical/ownership checks and a consuming definition; it describes technical readiness, not human visual acceptance. The CLI prints these verdicts separately. Concepts can return success for mechanical checks; use `--require-production-ready` on either candidate command to require all technical gates before integration. A family wrapper that runs several candidate commands must return nonzero when any child fails and offer the same gate, as `artwork/design-sources/darkstone-family-refresh-2026-11-12/prepare.py --production` does. `game-scale.png` compares the candidate and baseline at a common 64 pixels per cell; enlarged previews supplement it.

There is no outline gate and no outline exception record. `check`, `check --staged`, the staged artwork hook and deployment never measure outlines. The user's approval of a piece is final ([art guide](rimworld-art-guide.md#current-furnishing-requirements)); a candidate's outline figures inform that choice and nothing after it.

For a three-way comparison, add `previewReference` under `candidateChecks`. Supply a repository-relative path to the reference's exported colour PNG and its actual draw size in cells. Choose the matching directional view where applicable.

```json
"previewReference": {
  "path": "Textures/Things/Building/Serenite/Cluster.png",
  "drawSize": [1.3288, 1]
}
```

`prepare-candidate` and `check-candidate` append the reference to `game-scale.png` at 64 pixels per cell using its own draw size. The reference is used for visual comparison only.

Inspect the exported-pixel diagnostics: bright neutral pixels with little tint coverage and strongly coloured pixels with substantial coverage. Exact RGB, alpha and coverage thresholds are recorded in `candidate-check.json`; they warn without changing any pixels. CPU views do not establish in-game acceptance.

Alpha evidence includes bounds above 0, 1, 8 and 128. If a side moves more than `alphaDebrisTolerance` (default 8 source pixels) between alpha above 0 and above 8, the checker warns for inspection. These thresholds are diagnostic only; raw alpha above 0 still drives framing gates. No source or mask is cleaned automatically. Any cleanup must be declared as source repair and re-exported.

The former minimum layer scale of 0.9 has been replaced by sampling-density evidence and final framing checks. The Vitalite crystal scale of 0.69 is not itself a failure. Follow the [framing contract](rimworld-art-guide.md#numeric-framing-for-separate-generation) for future work. Candidate checks are explicit and separate from the production pre-commit check; they do not register a master or approve appearance.

### Shared-cup recipes

When new artwork must keep the identical shared wall cup, place the new crystal on the cup reference's canvas, then run:

```powershell
& $artPython tools/artwork_shared_cup.py artwork/design-sources/dreadstone-wall-lamp-shared-cup-2026-11-11/cup-parts.json work/TASK/crystal-placed.png work/TASK/layers
```

It verifies the cup reference hash and writes rear cup → crystal → front rim layers plus `composed.png`, restoring the front rim RGBA-exactly so the rear arms never clip over the crystal. Merge its printed `recipe` fields into the recipe and its `candidateChecks` fields (`requiredReviewJoins: ["cup-crystal"]`) into `candidateChecks`; the `cup-crystal` join preview is then required. Inspect that preview: the check cannot judge perspective or fit. A whole-generated torch, such as the approved Dark Stone wall torch, stays a single-image recipe.

### Protected-region checks

For a local edit that promises exact preservation, add `preservation` inside `candidateChecks` before generation:

```json
"preservation": {
  "reference": {"path": "work/TASK/reference.png", "sha256": "REFERENCE_SHA256"},
  "editableMask": {"path": "work/TASK/editable.png", "sha256": "MASK_SHA256"}
}
```

Paths are repository-relative. Both images must match the candidate's exported canvas exactly. The edit mask is an `L`-mode PNG containing only 0 (protected) and 255 (editable), with at least one protected pixel. Include the intended join and antialiasing margin in the edit region before the call. This permission mask is separate from construction-tint ownership.

When alignment is needed, prepare the reference through a declared recipe/transform first and pin its exported hash. The checker performs no automatic registration, resizing or tolerance adjustment. A uniform export can be used for game-scale comparisons; preservation of full-resolution source pixels needs a separate candidate recipe that retains that resolution.

`prepare-candidate` and `check-candidate` check both input hashes and compare every RGBA channel outside the edit mask, including alpha and RGB beneath transparent pixels. The report's `preservation` field records protected-pixel count, changed-pixel count and the exclusive-right/bottom bounds of any drift. Any protected change fails validation; mismatched canvases, fractional masks and empty protected regions also fail. Inputs and candidate artwork remain unchanged.

Restore protected components through declared recipe inputs and inspect the join, or obtain a new selection that explicitly includes the changed features. A visually similar repaint does not establish exact preservation. Earlier concepts without this declaration receive their existing checks; the tool does not infer a protected region from prose or pixel colour.

### Icon sets before gameplay definitions

`artwork/objects.json` also supports `candidateIconSets` and approved `iconSets`, each with a manifest and texture root. These register fixed-colour icons without inventing ThingDefs. The checker validates exact output hashes, RGBA canvases, nonempty alpha, source and recipe hashes, the upstream licence notice and integration texture paths. Approved sets require `approvedBy` and `approvedOn`; their frozen manifest is separate from regenerated candidate hashes.

For stone implants, run `& (git config --get lumen.artworkPython) tools/export-implant-icons.py --preview work/stone-implants/contact-sheet.png` to regenerate the retained candidate tree. This does not overwrite production textures or `approved-assets.json`. Promotion requires design acceptance; connect gameplay consumers separately and retain the final human in-game review.

## Family-rule tools

- `python tools/artwork.py silhouettes [--output DIR]` writes one flat single-colour silhouette sheet per fixture type (Column, Pylon, Cluster, Lamp, WallLamp, Stack, Equipment) to `work/silhouettes/`. Use the relevant sheet when identity is uncertain and comparison would help.
- `python tools/artwork_measure.py` regenerates `artwork/style-measurements.json`; see [artwork measurements](artwork-measurements.md).
- `python tools/artwork.py outline SPRITE... --draw-height CELLS [--kind building|resource|weapon]` measures sprites against the guide's [outline rule](rimworld-art-guide.md#measuring-an-outline) and exits non-zero when one misses it. It is a report for the user, never a reason to change approved art, and it changes no pixels (the former `--redraw` outline pass was removed on 2026-11-12). It does not yet separate thin parts (rule 9), so a spiky piece can measure in range and still read heavy; check `game-scale.png`. How it measures:
  - RimWorld stretches the canvas to the draw size, so the tool first enlarges the axis with fewer pixels per cell by repeating pixels (no blending), then works on that grid. It never resamples with blending, which would merge the line into the alpha edge. Alpha above half is the silhouette.
  - Width: at each black silhouette-edge pixel, the distance to the nearest non-black pixel inside the silhouette, × 64 ÷ pixels per cell; the median is reported. This is perpendicular to the edge, so slanted crystal tips are not over-counted. Target source pixels = width × pixels per cell ÷ 64, per axis.
  - Evenness: 90th minus 10th percentile of those widths. Coverage: share of row and column scans whose first opaque pixel is black; 98 percent passes, allowing for antialiasing.
  - Dark shares: pixels below 0.35, of the fill (every non-black pixel) and of all opaque pixels.
  - Clipping: a side where the silhouette touches the canvas edge is reported as clipped, and its edge pixels are left out of the width and evenness figures, so a framing cut does not read as an uneven line.
- `python tools/artwork.py outline-layer RECIPE --draw-size W,H --kind ROLE --output work/PIECE/outline [--target PX] [--max-add PX] [--from-mask]` (or `--png TEXTURE.png`) adds an [outside stroke](rimworld-art-guide.md#outline-layer): on every new candidate, and on an approved piece the user named. It writes `baseline/` (the recipe re-exported; for an approved piece, check that it matches the approved texture), `outline-mask.png` and `outline-preview.png` for editing, `export/` (the colour with the stroke under it, plus the unchanged mask) and `outline-layer.json` with the before and after figures, the stroke width and the canvas cut. A piece left as drawn or for a redo gets the report without `export/`. It refuses output outside `work/`.
- `python tools/artwork.py outline-lineup --output SHEET.png LABEL=PATH@W,H...` shows sprites side by side at game scale (64 px per cell, enlarged ×3 without smoothing) under neutral, slate and wood construction tints, using each sprite's `_m` mask.
- `check` also reports diagnostic family measurements: outline weight, hue gap and opaque specks. The outline weight here compares stones with Serenite and predates the outline rule; use `outline` for the rule. Thresholds and optional exceptions live in `artwork/family-checks.json`. Findings and stale exceptions do not fail the check or require a redraw; judge the artwork at game size.
- A pylon that is not 1x1 needs an entry in the registry's `pylonFootprintExceptions` (definition name to size, for example `"KM_SerenitePylon": "(2,1)"`). Ticket 10 adds the first one.
- `python tools/rotate_weapon.py SOURCE OUTPUT` rotates a melee weapon to horizontal, tip right, on a square canvas that keeps the original pivot. Recompute draw size (1.2 times canvas over 256) and `uiIconScale`, and update the registry canvas and hash.

## Record the current selection

There are two levels ([ADR 0001](adr/0001-artwork-approval-is-the-release-contract.md)). A **selected** design is still being refined and nothing protects it. An **approved** design is final: its design pixels never change unless the user names it for a redo. In the turn the user chooses, say which you are recording, for example "Recording this as approved (final)". If their words leave it unclear, ask. Record approval only in the turn the user gives it, using their words; never infer it from earlier messages or another agent's notes.

**Selected:** one line in `ARTWORK.md` naming the source image. Nothing else.

**Approved**, in the same turn:

1. Copy the export into `Textures/`. Keep the source image and recipe in `artwork/design-sources/PIECE/`.
2. In `artwork/objects.json`, set the object's `approvedMaster`: texture path, SHA-256, the user's words as `approvedBy`, and the date as `approvedOn`. For a new object, start from [master-template.json](../artwork/master-template.json) and link its recipe under `recipes`.
3. Add or replace one line in `ARTWORK.md`: piece, date, the user's words, what is still pending (mask, views, in-game review).
4. Run `python tools/artwork.py check`. From then on it fails if that texture changes without a new approval.

An optional `acceptance.json` beside the source may hold lineage. Keep superseded sources rather than deleting them.

**After approval** only these are added: the mask, which the user finishes in the mask editor (see the [mask review queue](#mask-review-queue)), and files computed from the approved export by recipe (crop, scale, mirror), such as other views and charge frames. A view with new pixels, or any change of framing or colour, is a new candidate. Hashes for masks, other views and resource stacks are not checked yet; the [follow-up spec](../.scratch/artwork-approval-gate/spec.md) adds them.

## Stage an explicit deployment

Create a JSON plan with a `files` list. Each entry has a repository-relative `path`. A plain entry deliberately replaces that complete file. For a shared XML file, add `xmlSelectors` to copy only the selected source elements into the installed XML, preserving other installed values and comments. Selectors must match exactly once in both files; use a parent element or an explicitly reviewed whole-file copy for additions/removals.

[deploy-plan.example.json](../artwork/deploy-plan.example.json) demonstrates two icon fields and a cluster mask. It is an example scope, not a full-mod deployment list.

```powershell
python tools/artwork.py check --preview work/artwork-review/current
python tools/deploy.py stage --plan artwork/deploy-plan.example.json --destination "E:/Steam/steamapps/common/RimWorld/Mods/Lumen-Stones" --output work/deployments/my-reviewed-change
```

Use a fresh staging directory. Staging reads the existing installation, checks the package ID and writes local candidates, `review.diff` and `manifest.json`. Inspect the exact file list and changes. The manifest records source, installed-baseline and candidate hashes separately. A field-selected candidate can intentionally differ from the full source file.

Before applying, finish the applicable visual review and run the artwork check if the source changed. A failing approved-master hash means approved art changed: stop and report it. Then:

```powershell
python tools/deploy.py deploy work/deployments/my-reviewed-change/manifest.json
python tools/deploy.py verify work/deployments/my-reviewed-change/manifest.json
```

Deployment checks every source, candidate and installed baseline before writing any installed file. It refuses edits made after staging. Previous installed files are copied to the stage's `before/` directory, copied candidates are hash-verified, and a copy failure restores touched files. The manifest records completed deployment; `verify` checks those installed candidate hashes later.

The tool updates an existing matching installation, supports declared mod runtime/document paths, and keeps all candidates/backups under source `work/`. It does not sync or delete unrelated installed files. Restart the game to load changed definitions/textures, then record visual acceptance separately.

## Enable the pre-commit guardrail

```powershell
./tools/install-artwork-hook.ps1 -Python "C:/path/to/python.exe"
```

The installer preserves an existing different hook configuration. It sets this repository's `core.hooksPath` to `.githooks` and records the selected Python in `lumen.artworkPython`. It changes no global Git settings.

The hook runs `artwork.py check --staged` on an isolated snapshot of the Git index. Unstaged good files cannot hide a broken staged asset, and unrelated unfinished working-tree edits do not substitute for the staged content. Include the new tooling, registry and declared assets in the commit that introduces them. The hook will reject a snapshot missing its registry or required files. It creates no commit and changes no staged content itself.

To run the tooling regression cases:

```powershell
python -m unittest discover -s tools/tests -v
```

The cases exercise the original transparent-mask and undersized-icon failures, master/direction/mask consistency, XML field preservation, stale/tampered deployment rejection and the real Git hook against staged versus unstaged content. They use isolated fixture repositories and installations under ignored work directories.
