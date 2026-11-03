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
4. For generation, follow [capture and attempt recording](#generation-capture-and-attempts). For an unregistered layered candidate, run [candidate validation](#candidate-validation) in addition to the production check.

Use Python 3.12 or later with the development-only image dependency:

```powershell
python -m pip install -r tools/requirements-artwork.txt
```

Codex's bundled Python already has the required Pillow version on this workstation. Use its full executable path in place of `python` if the shell's default Python differs. The hook installer records the chosen executable in local Git configuration; no personal runtime path is committed.

## Paint an existing mask

Open **Artwork gallery** in the editor to browse labelled mod textures and design sources. Items marked **FOR MASK REVIEW** appear first, with recently modified artwork first within each group; use the **For review** or **Reviewed** filters to find them. Confirmed superseded artwork is hidden from the gallery but retained on disk. Search by name or path, or filter between In mod and Designs. A recorded approved image is labelled only when it matches the registry hash; that label does not approve its mask. Select a texture to open its mask when one exists. Painting and selection are paused for one second after opening an item. A design without a matching current export shows its first source layer in the gallery; selecting it exports its colour and mask to a fresh directory under `work/` before opening them.

### Mask review queue

When handing a mask to the user for review or manual finishing, mark the exact colour PNG or recipe before handoff. Use a recipe for recipe-built artwork so the gallery opens a reproducible export; use the colour PNG for an existing non-recipe texture. Add a short note about what needs attention:

```powershell
& $artPython tools/mask_review.py add artwork/design-sources/OBJECT/master.json --note 'Inspect the housing edge'
& $artPython tools/mask_review.py add Textures/Things/Building/Misc/GlaciteCooler.png --note 'Inspect the housing edge'
& $artPython tools/mask_review.py list
```

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

Before each image call, save its exact tool arguments as `work/TASK/request.json`, using saved `referenced_image_paths`. The ledger hashes those references. Record the request before spending the call:

```powershell
& $artPython tools/artwork-attempt.py begin --ledger work/TASK/attempts --layer bowl --request work/TASK/request.json
```

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

Both `prepare-candidate` and `check-candidate` generate labelled 3x nearest-neighbour join views under neutral, brown and dark tints, with 12 output pixels of context padding. `candidate-check.json` lists their paths and output bounds in `joinViews`. Ownership polygon regions retain their automatic join views. A multi-layer recipe with neither kind of region warns that named joins are missing. Reinspect these views after any split, placement, scale or framing change; a passing report does not verify correct front/rear assignment.

The output must be a directory inside `work/`. Named ownership regions also receive zoomed tinted join views. The command returns nonzero on a mechanical failure and leaves the exports and `candidate-check.json` for diagnosis. It does not promote or deploy the candidate. Separate export and check commands remain available:

```powershell
& $artPython tools/artwork.py check-candidate artwork/layered-sources/vitalite-cluster-generated/master.json --output work/generated-layer-pilot/export
```

The command checks an existing export directory without rewriting its images. It exports the manifest twice into temporary directories under `work/`, checks both hashes, and compares the saved colour and mask byte for byte with fresh output. It checks opaque mask alpha, zero green/blue, source dimensions, placement scales, tinted-layer neutrality, baseline hashes, canvas, visible bounds, foot line and the consuming definition's draw size. Exit code 1 means a mechanical failure; `candidate-check.json` is written beside the exports with the manifest and source hashes and every measured failure.

Use `candidateChecks.mode: "new-design"` for a design with no approved baseline. Provide output-pixel `expectedBounds`, `boundsTolerance` and an exclusive-bottom `footLine` anchor. Baseline comparison is explicitly unavailable. Omit `definition` and `drawSize` until a real consumer exists; the report then marks runtime placement unavailable. A supplied definition must still pass its draw-size check. Replacement mode remains the default and requires baseline hashes and a consuming definition. Neither mode permits missing inputs to count as a completed comparison.

For a flattened concept whose material ownership is unresolved, declare `materialOwnership: "unresolved"` and keep the source fixed-colour in the manifest. The exporter's zero mask can be checked for encoding and repeatability, but the report explicitly marks material ownership unavailable; it is not a fixture/crystal partition and must not be shipped as one.

The manifest's `candidateChecks` declares:

- `baseline.color` and `baseline.mask`: each has a repository-relative `path` and expected `sha256`.
- `definition` and `drawSize`: the consuming ThingDef and expected `[width,height]`.
- `boundsTolerance`, `maxOpaqueSpread`, `minSourceSize`, `minSourcePixelsPerOutputPixel`: explicit task limits. The generated-layer pilot uses 8, 8, 1024 and 1 respectively. Density is `1 / (layerScale * exportScale)`; it includes both uniform reductions. Meaningful source and projected visible sizes are reported at alpha above 8 as diagnostics. These sizes do not prove visual detail or correct material ownership.

For ownership layers, the neutrality blocker uses alpha-weighted mean RGB spread only inside the declared polygons, limited by `maxOpaqueSpread`. The exactly-255 sample is evidence only. Whole-layer `constructionTint` stacks retain their existing opaque-spread gate.

The opaque spread gate reports its sample count and fraction of visible pixels. Alpha histograms and alpha-weighted mean spread supplement it; a small exactly-255 sample does not establish neutrality of the remaining surface. An empty opaque sample fails the literal opaque-spread gate. These measurements never alter source alpha, colour or mask.

`passed` means no mechanical errors; `readyForTintReview` additionally requires resolved material ownership. An unresolved flattened concept can pass encoding checks without being ready for tint review. Inspect the exported-pixel diagnostics: bright neutral pixels with little tint coverage and strongly coloured pixels with substantial coverage. Exact RGB, alpha and coverage thresholds are recorded in `candidate-check.json`; they warn without changing any pixels. CPU views do not establish in-game acceptance.

Alpha evidence includes bounds above 0, 1, 8 and 128. If a side moves more than `alphaDebrisTolerance` (default 8 source pixels) between alpha above 0 and above 8, the checker warns for inspection. These thresholds are diagnostic only; raw alpha above 0 still drives framing gates. No source or mask is cleaned automatically. Any cleanup must be declared as source repair and re-exported.

The former minimum layer scale of 0.9 has been replaced by sampling-density evidence and final framing checks. The Vitalite crystal scale of 0.69 is not itself a failure. Follow the [framing contract](rimworld-art-guide.md#numeric-framing-for-separate-generation) for future work. Candidate checks are explicit and separate from the production pre-commit check; they do not register a master or approve appearance.

### Icon sets before gameplay definitions

`artwork/objects.json` also supports `candidateIconSets` and approved `iconSets`, each with a manifest and texture root. These register fixed-colour icons without inventing ThingDefs. The checker validates exact output hashes, RGBA canvases, nonempty alpha, source and recipe hashes, the upstream licence notice and integration texture paths. Approved sets require `approvedBy` and `approvedOn`; their frozen manifest is separate from regenerated candidate hashes.

For stone implants, run `& (git config --get lumen.artworkPython) tools/export-implant-icons.py --preview work/stone-implants/contact-sheet.png` to regenerate the retained candidate tree. This does not overwrite production textures or `approved-assets.json`. Promotion requires design acceptance; connect gameplay consumers separately and retain the final human in-game review.

## Family-rule tools

- `python tools/artwork.py silhouettes [--output DIR]` writes one flat single-colour silhouette sheet per fixture type (Column, Pylon, Cluster, Lamp, WallLamp, Stack, Equipment) to `work/silhouettes/`. Use the relevant sheet when identity is uncertain and comparison would help.
- `python tools/artwork_measure.py` regenerates `artwork/style-measurements.json`; see [artwork measurements](artwork-measurements.md).
- `check` also reports diagnostic family measurements: outline weight, hue gap and opaque specks. Thresholds and optional exceptions live in `artwork/family-checks.json`. Findings and stale exceptions do not fail the check or require a redraw; judge the artwork at game size.
- A pylon that is not 1x1 needs an entry in the registry's `pylonFootprintExceptions` (definition name to size, for example `"KM_SerenitePylon": "(2,1)"`). Ticket 10 adds the first one.
- `python tools/rotate_weapon.py SOURCE OUTPUT` rotates a melee weapon to horizontal, tip right, on a square canvas that keeps the original pivot. Recompute draw size (1.2 times canvas over 256) and `uiIconScale`, and update the registry canvas and hash.

## Approved master for the next artwork task

1. Copy [master-template.json](../artwork/master-template.json) into the task's local work directory and fill in identity, geometry/counts, materials, camera, protected features and permitted edits before generation.
2. Use the closest approved registry object as the style reference. Keep the candidate master and reusable layers under the task's `work/` directory while the design is being settled.
3. Review the master at game scale, including the map and icon paths where relevant. Obtain the design acceptance required by the task; a hash is not a substitute for visual acceptance.
4. Record the accepted source path, SHA-256, approver and date, then add or update the object in `artwork/objects.json`. Declare every required direction and its canvas/mask encoding. Set a visible-icon height range from the accepted framing, not a universal default copied from the template.
5. Derive and review the directions against that master. Run the check/review-sheet command before staging. A changed master hash blocks the check until the approved record is intentionally updated.

The existing six masters record the user's 2026-10-29 visual acceptance. The template's placeholder hash cannot pass. Pyrinth's master is a read-only upstream reference; optional presence does not authorize editing the installed Workshop mod. Ticket 06 remains deferred.

## Stage an explicit deployment

Create a JSON plan with a `files` list. Each entry has a repository-relative `path`. A plain entry deliberately replaces that complete file. For a shared XML file, add `xmlSelectors` to copy only the selected source elements into the installed XML, preserving other installed values and comments. Selectors must match exactly once in both files; use a parent element or an explicitly reviewed whole-file copy for additions/removals.

[deploy-plan.example.json](../artwork/deploy-plan.example.json) demonstrates two icon fields and a cluster mask. It is an example scope, not a full-mod deployment list.

```powershell
python tools/artwork.py check --preview work/artwork-review/current
python tools/deploy.py stage --plan artwork/deploy-plan.example.json --destination "E:/Steam/steamapps/common/RimWorld/Mods/Lumen-Stones" --output work/deployments/my-reviewed-change
```

Use a fresh staging directory. Staging reads the existing installation, checks the package ID and writes local candidates, `review.diff` and `manifest.json`. Inspect the exact file list and changes. The manifest records source, installed-baseline and candidate hashes separately. A field-selected candidate can intentionally differ from the full source file.

Before applying, finish the applicable visual review and run the artwork check if the source changed. Then:

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
