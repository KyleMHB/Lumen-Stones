# Lumen Stones artwork

## Current artwork workflow

### Animastone column proportions, 2026-11-07

The user reported that the column was too skinny beside the other stones. Its draw width is now 1.6 cells instead of 1.1667, a 37% increase; height remains 1.4583. The native menu scale is adjusted to preserve visible icon height. Source colour and the user-painted mask are unchanged. The recipe's framing record and registry contract reflect the new draw width. The user approved the comparison preview on 2026-11-07: "looks great". Deployment and human in-game review remain pending.

### Workshop gallery and description refresh, 2026-11-04

The 24-image gallery now includes Lunite conversion and stabilization, Aegite personal shields and the shield projector, the Ferrite warhammer/Aegite shield swap, the simplified Sunstone flare and the Animastone pylon border repair. The lineage overview shows Sunstone turning into Lunite at night. The features illustration and Steam description cover solar-flare interruptions and Lunite's limits. The removed barricade is excluded.

`scripts/render-stone-portraits.py` includes Lunite's recorded resource palette. `scripts/render-workshop-art.py --family all` rebuilds the gallery, gallery order and all three description illustrations. Runtime textures are preferred. The projector and stabilizer are now in `Textures/`, exported from their `artwork/design-sources/bastion-lunite/*/master.json` recipes; their reviewed masks are promoted. The projector uses the user's corrected source. This does not promote artwork, change masks or approve in-game appearance.

Both local HTML previews under `artifacts/` were regenerated from `steam-description.md`; the full page follows `docs/images/workshop/gallery-order.txt` and uses `About/Preview.png`. These local changes have not been published to Steam.

### Typeset Workshop cover, 2026-11-04

The user requested a deterministic rebuild to remove the generated cover's checkering and use the actual RimWorld font, followed by release-ready polish. `scripts/render-workshop-cover.py --icon-only` composes a flat navy background, exact local RimWordFont outlines with independent ivory/gold crystal facets per letter, fine directional highlights, the original Glacite cooler, Vitalite cluster and Verdite pylon masters, and retained resource portraits. Visible lettering is centred and the three furnishings are optically balanced. The 1254px master is `artwork/workshop/lumen-stones-typeset-cover-master.png`; the full-resolution deliverable is `docs/images/workshop/lumen-stones-workshop-cover.png`. Source/font/output hashes and the method are recorded in `artwork/workshop/lumen-stones-crystal-cover.json`. The three local 512px preview/icon files use the rebuilt master. Earlier generated covers remain as history; revised appearance awaits review. Runtime textures are unchanged; no Steam publication was performed.

### Animastone pylon border repair, 2026-11-04

The user approved the continuous charcoal border between the tall crystal and its grey stone foot. The repaired source, exact imagegen request, contour patch, preservation evidence and export recipe are in `artwork/design-sources/animastone-pylon-border/`. Pixels outside the narrow contour patch and all source alpha are unchanged; the prior user-painted mask is preserved byte-for-byte. The approved export is integrated into `Textures/` and linked in the artwork registry. The colour texture and unchanged mask were deployed on 2026-11-04 and hash-verified against source; backups and the manifest are under `work/deployments/animastone-pylon-border-20261104/stage/`. Restart the game for the final combined human in-game review.

### Simplified Sunstone flare, 2026-11-04

The user selected the simpler Sunstone flare and requested production integration. The exact shown candidate now supplies the source master and 128px inventory texture: fewer broad amber facets, a smaller highlight, plain iron collar and simple unlit cord fuse. The built-in `image_gen.imagegen` request, original attempt and approval record are retained in `artwork/design-sources/new-equipment/sunstone-flare/revision-2026-11-04/`; the replaced source and export are preserved in `previous-2026-11-04/`. That archived source is the original edit target named by the request and its reference hash. The existing crop and single-reduction export method is retained. Design approval supersedes the flare entry from 2026-11-03; deployment and the final combined human in-game review remain pending.

### High-resolution Workshop resource portraits, 2026-11-04

The gallery renderer uses `artwork/workshop/stone-portraits/` for stone previews, including headers and the overview. `scripts/render-stone-portraits.py` derives the light stones from the retained 1254px Lumenstone portrait and each existing resource palette. Dark Stone, Bloodstone and Hollowstone use their recorded original resource-master crops; Sanguinite and Dreadstone apply their existing recolour recipes before reduction. `portraits.json` records source/output hashes, methods and native dimensions. These are Workshop-only portraits; the 128px game resources are unchanged.

### Pyrinth resource stacks, 2026-11-04

Pyrinth uses the shared Lumenstone one-, two- and three-piece silhouettes with an orange palette. `tools/export-pyrinth-resource.py` generates the three 128px resource textures while preserving source alpha, canvas and facets exactly. Source/output hashes, palette and the short object contract are recorded in `artwork/design-sources/pyrinth-resource/export.json`.

`Integrations/Pyrinth/Patches/ResourceArtwork.xml` replaces only `DV_Pyrinth`'s graphic data when Epochs - Pyrinth is active. The original Workshop installation is unchanged. The overview uses the same single-piece resource texture. Generated at the user's request; visual and combined in-game review remain pending. No game deployment was performed.

### New equipment design approval, 2026-11-03

The user approved all seven current equipment inventory designs after the medieval Sunstone flare revision. The Dreadstone collar is entirely dark crystal with an orange inset around its band; the Verdite bracer has a crystal cuff and living vines. Exact source and export hashes, including the derived Vitalite worn views, are in `artwork/design-sources/new-equipment/approval.json`. All seven inventory sprites are registered as approved masters. Head/hair fit, equipped appearance and gameplay remain for the final combined human in-game review. No deployment follows from this design approval.

### Natural Animastone artwork, 2026-11-03

The user selected the corrected natural Animastone family and finished the column, cluster and tall pylon masks. All 21 textures are integrated into `Textures/`; the three exact saved masks are preserved in their recipes and review history. The palette is green seafoam with an irregular pale opal mineral vein. Floor torch and wall sconce retain the existing shared bodies and alpha exactly, with only crystal pigment changed. The rejected manufactured eye-frame column, independent torch bodies and low two-cell pylon remain historical sources.

Sources, exact built-in imagegen requests, contracts and export recipes are in `artwork/design-sources/animastone-*`; `artwork/design-sources/animastone-candidates.json` maps every source texture to its recipe. Six consuming objects are registered. Shared-shape resource stacks and worn circlet views have exact source/export records outside the object checker's supported graphic classes. The pylon's accepted master and saved mask are integrated; `artwork/design-sources/animastone-pylon-tall/registry-pending.json` holds its registry entry until the implementation owner changes its old two-cell definition to a 1x1 footprint with `(1.1,2.2)` draw size and recalculates native icon framing. Ticket 01 excludes definition edits.

The circlet has separate inventory and four worn graphics, with east/west mirrored. Head and hair fit remain provisional because the installed split-DDS reference pixels could not be recovered reliably. Wall attachment, worn headgear fit, pylon placement, material tint and native icons await the final combined human in-game review. This artwork has not been deployed.

### Equipment cleanup, 2026-11-03

The user accepted the corrected Aegite hammer, Verdite scythe and Sunstone axe after reviewing before/after previews. Their source textures and registry records now use the reproducible recipes in `artwork/design-sources/equipment-refresh-2026-11-03/`. The hammer repair removes only haft/pommel speckles while preserving its source head, fittings, highlight and alpha. The exports retain their designs, orientation, vertical flips, centre pivots, draw sizes and historical outer framing. The Serenite pendant uses world draw size 0.4 with reciprocal icon scale 2.5, preserving its sprite and effective native icon size. Deployment and the combined in-game review remain pending.

### Ferrite maintenance cluster, 2026-11-03

The user selected the replacement cluster and finished its construction mask. Three unequal slate-silver crystals with copper-red facets sit in a smooth low oval cradle with two side braces, replacing the rod-and-clamp design. The selected source, exact imagegen request, shared export recipe and user-painted mask are in `artwork/design-sources/ferrite-cluster-rework-2026-11-03/`. The colour and exact saved mask are applied to the source textures and linked in the registry. Footprint, draw size and gameplay are unchanged. Framing, native menu icon and tinted appearance await the final combined in-game review; this pass has not been deployed.

### Serenite furnishings, 2026-11-03

The selected sanctuary shrine is a low 2x1 bedroom furnishing with three refined lavender crystal peaks in a grey horn-and-hoop cradle. Its same-material black foot seams were removed at the user's request. Source, generation records, ownership and coordinated export are in `artwork/design-sources/serenite-bedroom-shrine/`. It retains `KM_SerenitePylon`, its construction costs, 45% support category and sleep effects. The larger footprint may require reinstalling an existing shrine if its new second cell conflicts with nearby furniture or walls; save identity is retained, but old placement is not automatically relocated.

The user also selected matching column, soothing-cluster and wall-lamp designs in `artwork/design-sources/serenite-family-refinement/`. The floor lamp is explicitly unchanged. Colour exports are integrated into source; mask finishing is tracked in `artwork/mask-reviews.json`. The user finished the shrine, column and cluster masks, and all three exact saved masks are applied to their source textures and retained in their export recipes. The wall lamp retains its fixed-colour cup and existing attachment offsets, with derived mirrored and wall-occluded views. Tint boundaries, attachment, native icons, expanded-footprint placement and appearance await the final combined human in-game review. Nothing from this pass has been deployed.

The local light-stone resource stacks use the Lumenstone one-, two- and three-piece textures recoloured for Glacite, Vitalite, Serenite, Verdite, Aegite, Ferrite and Sunstone, as requested on 2026-11-02. Source hashes, palettes, method and output hashes are recorded in `artwork/design-sources/lightstone-resources/recolor.json`. These textures await the combined in-game review; the optional upstream Pyrinth resource is unchanged.

Before generating, editing, exporting, reviewing or deploying sprites, masks, visual effects, draw settings or menu icons, follow the [RimWorld object artwork guide](docs/rimworld-art-guide.md). It is the current workflow and style reference, including directional consistency, game-scale review, opaque material masks, native icon sizing and scoped deployment.

### Dreadstone seed, 2026-11-03

The user approved the three-claw concept adaptation. The seed has a charcoal-indigo stake with a blunt heel and three forward-pointing claws arranged around its rear in perspective, each with an orange seam. The generated source, user concept, exact prompts, retained prototypes, export recipes and approval hashes are in [dreadstone-seed](artwork/design-sources/dreadstone-seed/review.md). The 128px item graphic, 64px item/product icon and 32px health icon replace the Dreadstone resource and generic shard placeholders. Source textures are promoted; the final combined in-game review remains pending. No installed deployment was performed.

### Stone implant and crystal prosthetic icons, 2026-11-03

Ticket [08](.scratch/stone-implants/issues/08-artwork.md) has 27 user-approved 64x64 icons: ten implants (including optional Pyrinth), eight prosthetics, six health glyphs, and Attunement, Binding and attunement-sickness symbols. The user-designed Twinstone cell and the unspecified EPOE organs are excluded. The source recipe, upstream hashes, exact texture paths and retained candidate integration are in [stone-implants](artwork/design-sources/stone-implants/).

The actual `Boxes/Ultratech.png` box and Shoulder, Leg, Heart, Eye and Jaw masks derive from **Bionic Icons by AUTOMATIC1111**, Workshop [1677616980](https://steamcommunity.com/sharedfiles/filedetails/?id=1677616980), package `automatic.bionicicons`. The reference was read from `E:/Steam/steamapps/workshop/content/294100/1677616980/Textures/BionicIcons` on 2026-11-03. Copies of all exact PNG inputs, including the unused first `Default` box reference, are retained under `upstream/`, with SHA-256 hashes in `icons.json`. The full original MIT notice is included in [Licenses/BionicIcons-MIT.txt](Licenses/BionicIcons-MIT.txt).

The first draft used the plain Default box and added a rectangular stripe; the user found those boxes odd. The revision uses the actual Ultratech box, preserving its alpha, lid shading, stepped lower edges, native band shape and glow falloff. A continuous neutral/cyan palette remap changes only its colours; no stripe or glow is drawn. Prosthetic glyph placement comes from the original masks' green channel. Light boxes have dark glyphs; charcoal boxes have pale glyphs for contrast. The shard glyph alone was generated with the built-in `image_gen.imagegen` tool after the user requested that only the overlaid icon be generated. Its exact prompt and reference are in `shard-request.json`, its retained output is `shard.png`, and its explicit crop and uniform fit are in `icons.json`. Isolated alpha=1 background debris outside the declared crop is excluded; source alpha inside the crop is retained. The actual box and prosthetic glyphs were not generated. Ritual and sickness symbols use native editable polygon/line recipes with four-times supersampling. All output colours are fixed, with no construction masks or runtime glow.

The candidate tree mirrors the proposed mod paths. `BionicIconsIconDef` uses direct `thingDef` registrations to preserve each complete coloured box. A `BionicIconsTextureDef` recomposition is unnecessary. The proposed load folder is conditional on `automatic.bionicicons`. Missing parts are ignored by upstream's `GetNamedSilentFail`, so optional Pyrinth and Ideology parts need no additional registrations. The installed reference has versions through 1.6; compatibility in the target 1.7 game remains unverified.

**Health-tab limitation:** the inspected upstream `Source/BionicIcons.cs` only replaces ThingDef graphics and item icons. Neither Bionic Icons definition type registers a Hediff icon. Separate health glyphs are supplied, but actual health-tab display requires a supported consumer when gameplay is implemented; it must not be claimed as working through these registrations. The mod's own state-icon comp now provides that display, independent of Bionic Icons.

The user approved the full icon sheet on 2026-11-03 after the larger shard revision (45 degrees clockwise, fitted to 34x26 and placed at y12). All 27 exact reviewed PNGs are promoted into source `Textures/`. The optional registrations are in `Integrations/BionicIcons/Defs/StoneImplantIcons.xml`; the conditional folder entry was merged into the current `LoadFolders.xml`. `artwork/objects.json` registers this approved set under `iconSets`, using the frozen `approved-assets.json` hashes. Re-exporting candidates cannot overwrite this approval record. Candidate sources remain under `candidate/` for reproducibility. The gameplay definitions reference all 18 item graphics directly, with a white colour so the approved box palette renders faithfully, and use the Attunement and Binding icons for their rituals, precepts and commands. The six health glyphs and the attunement-sickness icon show in the health tab through the mod's `HediffComp_StateIcon`, the state icon vanilla draws beside each hediff row; part glyphs dim while the part is dormant. The combined human in-game review remains pending. Source promotion is not deployment to an installed game.

### Sunstone and Twinstone cells, 2026-11-03

The user approved twin canisters with shared neutral metal caps, a central divider, a middle retaining band and one squared handle: amber and garnet crystals for the Twinstone cell and two Sunstone crystals for the Solar-only Sunstone cell. Both were made with the built-in `image_gen.imagegen` tool; requests, candidates, design notes and approval hashes are in [twinstone-cell](artwork/design-sources/twinstone-cell/). Each approved image was cropped to its visible pixels, centred on a square and resized once to the 128x128 item textures `Textures/Things/Item/Misc/TwinstoneCell.png` and `SunstoneCell.png`, with fixed colours and no mask. The export hashes are in the approval files. In-game review pending.

### Solar and fixture-animation toggle icons, 2026-11-07

The Solar active-effect and fixture-animation toggles no longer borrow vanilla's forbid icon. `UI/Commands/SolarActiveEffect` recolours the Warp toggle's crystal and power ring onto a Sunstone gold ramp. `UI/Commands/FixtureAnimation` redraws the same crystal without the ring on a pale Lumenstone ramp and adds four-point glints. Both are deterministic derivatives of `UI/Commands/WarpActiveEffect`, not new paintings; the recipe is `artwork/design-sources/gizmo-icons/derive.py`. Appearance and gizmo framing await the combined in-game review.

### Infused dark-stone motes and husk icons, 2026-11-02

The user approved the Sanguinite droplet, Dreadstone ember and Hollowstone ring shapes. Their generated sheet, exact requests, rejected transparency edit, approved brightness-to-alpha conversion and 64px export recipes are in `artwork/design-sources/dark-stone-motes/`. The selected original shapes are preserved; the rejected image edit is not used. On 2026-11-03, the user requested removal of these three families' animations. Their fixture effecter attachments have been removed; the mote textures and definitions remain as unused design records. Lighting and gameplay effects are unchanged.

The shared Warp toggle retains `UI/Commands/WarpActiveEffect`. Raise-husk and rebind-husk commands use the approved Hollowstone pylon's native icon; the raise-husk ritual icons use its texture. In-game gizmo framing remains pending.

### Dark Stone pylon simplification, 2026-11-01

The user accepted a simpler pylon design and finished its construction mask. The source, generation record, acceptance and coordinated export are in `artwork/design-sources/darkstone-pylon-simplified/`. Its pylon silhouette was compared with the other families. The source texture and mask are updated; placement, tint and appearance still await the combined in-game review. The original pylon source and painted mask remain in `artwork/design-sources/darkstone-pylon/`.

The user also accepted the simplified Bloodstone obelisk and finished its mask. Its candidate is in `artwork/design-sources/bloodstone-obelisk-simplified/`. The original pedestal pixels and all four charge overlays are retained; the new composite and mask are exported together. Runtime charge rendering and appearance await the combined in-game review.

The simplified Dark Stone cluster has broad crystal facets, a simple three-support base and a user-painted mask. North/south lean right; east/west use an exact horizontal mirror of the colour and mask. The single-view texture remains available for the new stones' placeholders. Source and recipes are in `artwork/design-sources/darkstone-cluster-base-simplified/`; the earlier design remains in `artwork/design-sources/darkstone-cluster-simplified/`. The mirrored mask is queued for review; placement, rotation and tinted appearance await the combined in-game review.

### Sanguinite artwork, 2026-11-02

The approved pylon and cluster use broad crystal facets with the column's muted crimson palette. Their current recipes are in `artwork/design-sources/sanguinite-pylon-colour/` and `artwork/design-sources/sanguinite-cluster-colour/`. The cluster uses the drawn view for north/south and a mirrored view for east/west. The approved column has a twisted shaft and simplified talon cap and base; its current recipe is in `artwork/design-sources/sanguinite-column-base-simplified/`. The user reviewed the current construction masks, and the saved masks were applied to the project textures. Placement, tint, rotation and appearance remain for the combined in-game review.

The floor torch, wall torch and flensing blade use the approved simplified facets. Sources and recipes are in `artwork/design-sources/sanguinite-floor-lamp-simplified/`, `sanguinite-wall-lamp-simplified/` and `sanguinite-flensing-blade-simplified/`. The floor torch retains the previous post and lower collar; exact preservation of the rest of the collar has not been established. The wall torch has derived attachment views and a centred icon; the blade points right with equipped angle offset -65. Attachment, icons, grip and appearance await in-game review.

The resource stacks retain the exact Dark Stone shapes recoloured crimson, with their mapping and hashes in `artwork/design-sources/sanguinite-resource/recolor.json`. The requested further simplification remains open: the first candidate changed the shared silhouette. Stack switching and appearance await in-game review. Earlier artwork and masks remain in the prior design-source directories.

The Hollowstone cluster uses the user-approved broken ivory arch around a dark cyan hollow. North and south retain the drawn view, and east and west mirror the artwork and its user-painted mask. The directional masks have been reviewed. The matching pylon and roof-bearing column have long cyan-edged hollows. Their user-painted masks tint the bases and the column cap while keeping the ivory fixed. Sources and export recipes are in `artwork/design-sources/hollowstone-cluster/`, `artwork/design-sources/hollowstone-pylon/` and `artwork/design-sources/hollowstone-column/`; appearance and placement await the combined in-game review. The Hollowstone glaive uses the user-approved jagged glass-edged blade and metal haft; its source and export are in `artwork/design-sources/hollowstone-glaive/`. Grip, size and orientation await the final in-game review.

The column's two lower material borders were repaired at the user's request and its mask reviewed again. A rectangular splice caused visible seams, so it was replaced by two narrow patches following the border contours. The current source, contour polygons and recipe are in `artwork/design-sources/hollowstone-column-contour-repair/`; the generated edit, original source and prior painted masks remain in the earlier column directories.

The user then strengthened the lower black borders in the editor. The resulting colour and mask overrides are retained in the contour-repair recipe and reproduced by the artwork check.

The Hollowstone floor lamp uses the user-approved complete generated sprite, including its own wooden post and neutral collar. The user explicitly preferred those to the shared-base composite and asked that they be preserved. Its source and full-sprite export recipe are in `artwork/design-sources/hollowstone-floor-lamp/`; in-game placement and appearance remain pending.

The user approved the Hollowstone wall-lamp concept on 2026-11-02. Its north source, directional frames and centred icon are in `artwork/design-sources/hollowstone-wall-lamp/`. East and west mirror the approved design at the existing wall anchors; south exposes only the wall-occluded tip. Attachment, occlusion and icon appearance await the combined in-game review.

The Hollowstone resource uses the user-approved notched ivory chunk with cyan hollow. The source and stack arrangement are in `artwork/design-sources/hollowstone-resource/`; stack switching and in-game appearance remain pending. The `Graphic_StackCount` resource is outside the current object registry.

The user accepted the simplified Dreadstone cluster on 2026-11-02: three unequal indigo crystals with orange cores on a tall continuous round band with three evenly spaced visible rivets. The new source, requests, acceptance and recipe are in `artwork/design-sources/dreadstone-cluster-simplified/`; the user painted the band mask. The earlier source and mask remain in `artwork/design-sources/dreadstone-cluster/` as history. Tint and appearance await the combined in-game review.

The Dreadstone floor lamp uses the user-approved complete wooden post and riveted crystal collar. Source and recipe are in `artwork/design-sources/dreadstone-floor-lamp/`; appearance awaits the combined in-game review.

The Dreadstone wall lamp uses the user-approved clustered head in a riveted cup. Its derived frames and icon are in `artwork/design-sources/dreadstone-wall-lamp/`; attachment and in-game appearance remain pending.

The Dreadstone resource uses the Dark Stone stack shapes and facet brightness recoloured in the Dreadstone cluster's indigo and orange palette, as requested on 2026-11-02. The mapping and hashes are in `artwork/design-sources/dreadstone-resource/recolor.json`; the revised appearance and stack switching await review.

The Dreadstone pylon uses the user-approved collar-free crystal and adjusted base. The user painted its base mask; source and recipe are in `artwork/design-sources/dreadstone-pylon/`. Placement, tint and appearance await the combined in-game review.

The Dreadstone arbalest uses the simplified material revision accepted by the user on 2026-11-03: Dreadstone stock, rail, grip and ring, metal bow limbs, the foreground string, and a wooden bolt with an iron tip. Source, exact imagegen requests, approval and uniform export recipe are in `artwork/design-sources/equipment-refresh-2026-11-03/`. The earlier user-corrected horizontal-stock source remains in `artwork/design-sources/dreadstone-arbalest/`. The accepted revision is applied to the source texture and registry; it has not been deployed. Aim, grip, size and icon appearance await the combined in-game review. The existing vanilla arrow projectile is retained by user decision on 2026-11-02.

### Lumenstone and Dark Stone floor lamps, 2026-11-01

Both floor-lamp candidates use new pale Lumenstone and smoke-violet Dark Stone crystals with the existing shared wooden post and neutral collar. Their recipes and generated source records are under `artwork/design-sources/lumenstone-floor-lamp/` and `artwork/design-sources/darkstone-floor-lamp/`. The post and collar retain the selected Serenite lamp's pixels from row 126 down; its shared base originated in the read-only Pyrinth reference documented in [the standing-torch audit](artwork/standing-torch-layered-audit.md). The lamps use fixed wood and no construction-tinted pixels. The generator's redrawn posts are not used. Both colour and zero-coverage masks are recipe-verified; placement, appearance and menu icons await the combined in-game review.

The Lumenstone and Dark Stone columns use the user-approved circular-ring and flat-capped claw designs, respectively. Sources, generation records and coordinated colour/mask recipes are in `artwork/design-sources/lumenstone-column/` and `artwork/design-sources/darkstone-column/`. The Lumenstone ring and plinth ownership was revised after inspecting the crystal boundary under dark and brown tints. Both columns are in `Textures/Lumenstone/`; their adjusted menu scales and runtime placement await the combined in-game review.

On 2026-11-01 the user finished eight material masks in the mask editor: both columns, clusters and pylons, and both Bloodstone batteries. The saved inputs and review hashes are recorded in `artwork/mask-reviews.json`; all eight recipe outputs were checked against those saved masks and copied into `Textures/`. The Dark Stone pylon is now connected and registered. The Bloodstone graphics remain unwired while the independent charge-indicator drawing is implemented. Mask review is separate from final in-game acceptance.

The Lumenstone cluster candidate uses three pale crystal points in a low stone ring. Its coordinated colour and mask export is recorded in `artwork/design-sources/lumenstone-cluster/`, with the right inner rim traced separately after dark-tint inspection. Runtime appearance and menu framing await the combined in-game review.

The Dark Stone cluster candidate uses an asymmetric violet monolith over smaller shards in a spiked ritual mount. Its colour, construction mask, source and request are recorded in `artwork/design-sources/darkstone-cluster/`. Its rear housing and central violet inset were inspected under dark and brown CPU tints. Final material boundaries, game-scale silhouette and placement await the combined in-game review.

The Lumenstone pylon candidate uses a pale crystal between curved stone supports. The generated source had a diffuse alpha halo; an explicit exterior contour and two openings, recorded in `artwork/design-sources/lumenstone-pylon/source-repair.json`, remove that matte before the coordinated colour/mask export. Its 1x1 footprint, tall framing, anchor and menu appearance await the combined in-game review.

Lumenstone and Dark Stone wall lamps each use one generated north-facing design. The other placements are uniform reductions or translations of that design, with east/west mirrored as in the existing Serenite reference and south showing the occluded crystal tip. Directional sources, transforms and provenance are in `artwork/design-sources/lumenstone-wall-lamp/` and `artwork/design-sources/darkstone-wall-lamp/`; each also supplies a separate centred menu image. The four views were compared in `work/handoff-masters/wall-lamps-comparison.png`. Wall attachment, occlusion and icon appearance await the combined in-game review.

Dark Stone and Bloodstone resource stacks use generated single-chunk masters arranged as one, two or three pieces on the existing 128px stack canvas. Their sources, exact generation requests and arrangements are recorded in `artwork/design-sources/darkstone-resource/` and `artwork/design-sources/bloodstone-resource/`; a CPU comparison is in `work/handoff-masters/resource-stacks.png`. The current object registry checks `Graphic_Single` and `Graphic_Multi`, so these `Graphic_StackCount` items are not registered there. Stack switching and appearance await the combined in-game review.

The two sacrifice ritual icon candidates are `CrystallizeDarkStone` and `HarvestWarpEnergy` in both `Textures/UI/Icons/` and `Textures/UI/Commands/`. Built-in imagegen sources, requests and export details are in `artwork/design-sources/crystallize-icon/` and `artwork/design-sources/harvest-icon/`; their 256px exports are shown in `work/handoff-masters/ritual-icons.png`. The icon paths still need to be connected by the implementation owner, and their appearance remains pending the combined in-game review.

Lumenstone and Dark Stone pulse motes share a neutral white generated sparkle at `Things/Mote/LumenstonePulse` and `Things/Mote/DarkStonePulse`; their component supplies the family-specific colour. The source, generation request and 64px export details are in `artwork/design-sources/neutral-fixture-pulse/`. Runtime colour, scale and motion await the combined in-game review.

The latest user requirements define the object. Approved repository sprites define its visual target. The records below preserve provenance and superseded attempts; use the guide to decide the process rather than copying an old prompt or export method.

[Artwork tooling](docs/artwork-tooling.md) provides the checked master registry, contact-sheet command, staged deployments and pre-commit hook. Tickets 03, 04 and 05 were closed on the user's explicit visual confirmation on 2026-10-29; earlier pending-verification notes below are historical.

### Lumenstone candidate art, 2026-11-01

Candidate, pending the final human in-game review. No image generator was available, so these are deterministic derivatives rather than new paintings: `Things/Item/Resource/Lumenstone/Lumenstone_a/b/c.png` and both ritual icons (`UI/Icons/LumenstoneRitual.png`, `UI/Commands/LumenstoneRitual.png`) gradient-map the retired Vitalite chunk to a pale grey-blue ramp, then arrange it as one, two or three chunks or add a soft halo. `Things/Building/Linked/Lumenstone_Atlas.png` applies the same ramp to the retired Glacite atlas, preserving its sixteen cells and silhouettes. The sources remain in Git history at commit `535e14d`. `Things/Building/Misc/LumenCircle.png` is a new drawn ring because the vanilla ritual spot ships only in Ideology's assets, and the circle now loads without Ideology. Replace them with painted art if the review finds them too plain or too close to Vitalite.

### Current furnishing review, 2026-10-31

The [consolidated specification](.scratch/layered-artwork-pipeline/spec.md) is the current scope. The user's later in-game screenshots identified residual material-boundary and cluster-fidelity problems, a displaced Vitalite pylon and a Glacite northern-neighbour cutoff. The successful Verdite column is protected. The new target is 1x1 occupied pylon cells with tall one-wide/two-high artwork, fixed-material floor torches, consistent base anchoring and high-fidelity clusters at the user's enhanced zoom.

The layered candidate deployment is recorded separately under [deployment evidence](artwork/layered-artwork-review/deployment.json). Its matching hashes establish installed bytes, not visual approval. User-review captures are retained locally under ignored `work/layered-artwork-pipeline/review-evidence/`. The [current acceptance checklist](.scratch/layered-artwork-pipeline/acceptance.md) carries observed failures and pending decisions; previous approvals remain historical and scoped to what was actually reviewed.

The [cluster layer reconstruction index](artwork/layered-sources/cluster-families/index.json) links provisional editable splits from the current flattened sprites. Their material ownership is not visually approved, and their original source resolution does not meet the final close-zoom criterion by itself.

Follow the guide's [current furnishing requirements](docs/rimworld-art-guide.md#current-furnishing-requirements) before authoring or changing placement/material/rendering settings. Project instructions now route those work types to the same guide. The checker enforces the six one-cell/realtime pylon contracts and fixed five-wood construction for all six floor torches; measured bounds/anchors, saved-material conversion, artwork repairs, deployment and visual acceptance remain open as stated in the [tooling boundary](docs/artwork-tooling.md#current-automation-boundary).

### Approved reference assets

The [Dreadstone column reference](artwork/dreadstone-column-design.md) records the user-approved unbanded redesign and its integrated source and recipe. Material tint, runtime placement and in-game acceptance remain pending.

Choose the nearest relevant sibling, and inspect its masks and definitions with the color sprite. One reference can control style while another controls shape or palette; name those roles before generation.

| Reference | Use it for |
| --- | --- |
| [Improved Verdite column candidate](artwork/verdite-column-layered.md) | Preserve the user's positively reviewed column version; use its recorded deployed colour/mask hashes rather than restoring an older same-named source |
| [Glacite column](Textures/Lumenstone/GlaciteColumn.png) | Simple broad crystal faces, charcoal contour weight and fitting proportions |
| [Glacite cooler](Textures/Things/Building/Misc/GlaciteCooler.png) and [mask](Textures/Things/Building/Misc/GlaciteCooler_m.png) | Low housing, crystal/fitting separation and the cooler family camera; inspect encoding before reusing a legacy mask |
| [Restorative cluster](Textures/Things/Building/Vitalite/Cluster.png) and [mask](Textures/Things/Building/Vitalite/Cluster_m.png) | Current three-point Vitalite palette, open scooped housing and smooth rim/material boundaries |
| [Inlaid bed, south](Textures/Things/Building/Vitalite/SlabBed_south.png) and [full bed, south](Textures/Things/Building/Vitalite/FullBed_south.png) | Approved bed framing and feature proportions; compare all four directions for a bed edit |
| [Glacite resource stack](Textures/Things/Item/Resource/Glacite/Glacite_c.png) | Readable small-item silhouette and detail density |

### Style requirements

Use the guide's [visual style reference](docs/rimworld-art-guide.md#visual-style-reference) and [prompt scaffold](docs/rimworld-art-guide.md#prompt-scaffold). Keep selected sources and export details in the relevant generation record below. The [project retrospective](docs/retrospectives/2026-10-29-project-retrospective.md) explains the failures behind these rules and the proposed automated checks.

## Current artwork and generation history

### Historical correction attempt, 2026-10-31, before the layered review

This earlier attempt set all six Lumen pylon definitions to 2×1 tiles and retained their draw proportions. Glacite and Vitalite menu scales compensated for that footprint. These placement decisions are superseded; the current requirement is a 1x1 footprint with tall artwork. The following records describe that historical attempt, not current visual acceptance.

All 33 local material masks were reviewed against their color sprites and dark/light construction-tint models. Seven legacy alpha-encoded masks now store material coverage in red with opaque-black exclusions. Material selections recover connected neutral housing gaps and exclude colored crystal edges. Mask-only repairs preserve existing color sprites; replacement masters are recorded separately below. The optional upstream Pyrinth masks remain read-only.

The replacement standing torches, flat-topped Aegite column and 512px Verdite pylon/cluster exports were applied after the user instructed continuation following the correction preview. Wall lamps are excluded on the user's explicit clarification. Every standing torch reuses the same original Pyrinth wooden post/collar layer, with fixed wooden pixels and a separate crystal layer. The collar alone accepts construction tint where the existing lamp supports it. The Verdite cluster reuses its original high-resolution source; the other repainted assets use built-in imagegen. Eight master records now describe the replacement exports and their measured menu framing. Contracts, full prompts, source paths and export records are saved under ignored `work/artwork-corrections-20261031/`.

Installed 45 texture files and six scoped definition updates on 2026-10-31. The installed release retains its Crynth names; the deployment maps the corresponding Glacite source paths to those existing names without deploying the unrelated rename. Only pylon size and the two compensating icon-scale fields change in installed XML; other installed settings remain intact. Staging, installed-file backups and the deployment manifest are under `work/artwork-corrections-20261031/installation-overlay/work/stage/`. Restart RimWorld to load the changes. Reloaded in-game appearance remains to be checked.

### Masked construction-menu icons, 2026-10-29

The Glacite, Vitalite and Pyrinth pylons and Vitalite inlaid bed now use native graphic-derived menu icons, matching the restorative cluster's masked rendering. Their explicit `uiIconPath` overrides bypassed `BuildableDef.ResolveIcon`, leaving the menu without its mask-aware UI material and allowing construction-material color to multiply the entire crystal sprite.

Approved source artwork and masks are unchanged: `GlacitePylon_north.png`/`GlacitePylon_northm.png`, `Vitalite/Pylon.png`/`Pylon_m.png`, `Vitalite/SlabBed_south.png`/`SlabBed_southm.png`, and Pyrinth's installed `PyrinthBrazier_north.png`/`PyrinthBrazier_northm.png`. Native default placement is North for the directional pylons and the bed explicitly uses South. The old centered Glacite/Pyrinth menu textures remain framing references. No sprite generation, repainting, export or world draw-size/anchor change was needed.

Removing an icon override enables native icon scaling by the minimum draw-size-to-footprint ratio. Corrected `uiIconScale` values are Vitalite pylon `0.62069`, inlaid bed `1`, Glacite pylon `0.472574` with offset `(0,0.199367)`, and Pyrinth pylon `0.471958` with offset `(0,0.198594)`. The latter offsets compensate the transparent lower padding in their directional sprites; these values derive from their visible alpha bounds. Pyrinth's optional integration changes only its icon scale/offset and no longer adds an icon-path override.

Local steel-tint comparisons and framing measurements are under ignored `work/masked-menu-icons/`. Glacite/Pyrinth bounds remain within one pixel in the 128px comparison. The first preview incorrectly used the maximum size ratio for the Vitalite pylon and bed and therefore did not catch their undersized icons. User screenshots exposed that error. The corrected comparison under `size-correction/` uses the actual `GenUI.IconDrawScale` minimum and `Command.DrawIcon`'s additional 0.85 factor. The pylon's effective scale is restored to 0.9 and the inlaid bed's to 1, preserving masked construction tint and all artwork/world draw settings. This remains a renderer-equation comparison; reloaded in-game confirmation is pending.

Deployed `Defs/Buildings.xml`, `Defs/Vitalite.xml` and `Integrations/Pyrinth/Patches/PylonIcon.xml` to the installed Lumen Stones mod on 2026-10-29. Source/installed hashes match. Previous installed definitions are backed up under ignored `work/deploy-backups/masked-menu-icons-20261029-193055/`.

Follow-up deployment corrected only the two Vitalite icon scale values. The installed XML has unrelated research settings that differ from source, so deployment applied the scale edits to the installed file rather than replacing those settings. Both installed scale values match source and the deployed file matches the staged candidate. Backup: `work/deploy-backups/vitalite-icon-size-20261029-194416/`.

The earlier bed views used coordinated four-direction sprite sheets. The original inlaid bed had a crystalline pillow and three smaller insets; the accepted 2026-11-02 stone-bed design supersedes that artwork.

The column artwork generation record is in [COLUMN-ART-PROMPTS.txt](COLUMN-ART-PROMPTS.txt).

Vitalite's final prompts, references and export details are in [VITALITE-ART-PROMPTS.md](VITALITE-ART-PROMPTS.md). Its first generated set was rejected as too detailed; the shipped sprites use fewer, broader faces. The full bed is entirely crystalline Vitalite. The earlier inlaid-bed design had a crystalline pillow and three centre insets; it is superseded by the accepted stone-bed design. Separate directional sprites preserve the fixed camera perspective. Rough chunks use a separate sprite; the usable-piece sprites are unchanged. The restoration circle reuses RimWorld's native ritual spot graphic with a pale rose tint.

The generation record below uses current Glacite names and asset paths.

Generated with the built-in OpenAI image generation tool. Each original sprite was supplied as a reference. Export retains the original PNG dimensions and aligns each sprite to the original placement bounds. Shader masks are derived from the replacement crystal and housing regions.

## Things/Building/Filth/GlaciteFilth_A.png

Reference: `Things/Building/Filth/PyrinthFilth_A.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 128x128 pixels. The same sparse scatter of six little flat crystal chips, changed from orange to blue.

## Things/Building/Linked/Glacite_Atlas.png

Reference: `Things/Building/Linked/Pyrinth_Atlas.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 2048x2048 pixels. TECHNICAL 4-by-4 CONNECTED ROCK TILE ATLAS. Preserve all sixteen cell positions, seam junctions, crystal cliff edge profiles, empty regions, and transparent silhouette EXACTLY. Change the orange/red mineral to blue/cobalt/cyan; retain the large flat untextured facets. Do not rearrange or invent cells. Output square atlas.

## Things/Building/Misc/GlaciteCooler.png

Reference: `Things/Building/Misc/PyrinthHeater.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 256x256 pixels. The same simple rounded light gray metal cage and three large flat crystal shapes inside it, changed from orange to blue. No rivets, extra crystals, stone texture, added bands, or extra detail.

## Things/Building/Misc/GlaciteLamp.png

Reference: `Things/Building/Misc/PyrinthLamp.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 128x128 pixels. A plain short brown wooden torch post with a gray rim and one angular blue crystal. Same on-canvas position and small silhouette.

## Things/Building/Misc/GlacitePylon/GlacitePylon_east.png

Reference: `Things/Building/Misc/PyrinthBrazier/PyrinthBrazier_east.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 512x512 pixels. The same EAST facing small pale gray cup with two little feet and one long asymmetric blue crystal. Preserve the large clear space below the feet and the reference crystal profile.

## Things/Building/Misc/GlacitePylon/GlacitePylon_north.png

Reference: `Things/Building/Misc/PyrinthBrazier/PyrinthBrazier_north.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 512x512 pixels. The same NORTH facing small pale gray cup with two little feet and one long asymmetric blue crystal. Preserve the large clear space below the feet and the reference crystal profile.

## Things/Building/Misc/GlacitePylon/GlacitePylon_south.png

Reference: `Things/Building/Misc/PyrinthBrazier/PyrinthBrazier_south.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 512x512 pixels. The same SOUTH facing small pale gray cup with two little feet and one long asymmetric blue crystal. Preserve the large clear space below the feet and the reference crystal profile.

## Things/Building/Misc/GlaciteLamp/GlaciteLamp_a.png

Reference: `Things/Building/Misc/PyrinthLamp/PyrinthLamp_a.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 128x128 pixels. Random torch variant A: one single blue crystal on a short brown post. Keep reference variant's particular outline and placement. Very small and simple.

## Things/Building/Misc/GlaciteLamp/GlaciteLamp_b.png

Reference: `Things/Building/Misc/PyrinthLamp/PyrinthLamp_b.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 128x128 pixels. Random torch variant B: one slightly left-leaning blue crystal on a short brown post. Keep reference variant's particular outline and placement. Very small and simple.

## Things/Building/Misc/GlaciteLamp/GlaciteLamp_c.png

Reference: `Things/Building/Misc/PyrinthLamp/PyrinthLamp_c.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 128x128 pixels. Random torch variant C: one slightly right-leaning blue crystal on a short brown post. Keep reference variant's particular outline and placement. Very small and simple.

## Things/Building/Misc/WallGlaciteTorch/WallGlaciteTorch_east.png

Reference: `Things/Building/Misc/WallPyrinthTorch/WallPyrinthTorch_east.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 128x128 pixels. EAST wall torch sprite. The little crystal and wooden bracket are placed tight against the LEFT edge, exactly as in reference. Keep the empty rest of canvas and the same offset; only change orange crystal to blue. Do not center the object.

## Things/Building/Misc/WallGlaciteTorch/WallGlaciteTorch_north.png

Reference: `Things/Building/Misc/WallPyrinthTorch/WallPyrinthTorch_north.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 128x128 pixels. NORTH wall torch sprite. The little crystal and wooden bracket are in the LOWER center, exactly as in reference. Keep the empty rest of canvas and the same offset; only change orange crystal to blue. Do not center vertically.

## Things/Building/Misc/WallGlaciteTorch/WallGlaciteTorch_south.png

Reference: `Things/Building/Misc/WallPyrinthTorch/WallPyrinthTorch_south.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 128x128 pixels. SOUTH wall torch sprite. Only a small triangular blue crystal tip is visible at the TOP center, touching the top edge, exactly as reference. The rest is EMPTY transparent. No post, no bracket, no other objects. Preserve this intentional wall occlusion.

## Things/Building/Misc/WallGlaciteTorch/WallGlaciteTorch_UI.png

Reference: `Things/Building/Misc/WallPyrinthTorch/WallPyrinthTorch_UI.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 256x256 pixels. The same inventory icon of the short dark brown wall torch with gray collar and one blue crystal, no extra detail.

## Things/Item/Equipment/WeaponMelee/GlaciteBlade.png

Reference: `Things/Item/Equipment/WeaponMelee/PyrinthBlade.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 256x256 pixels. The same horizontal short crystalline sword. Gray hilt on LEFT, long curved blue blade pointing RIGHT. Keep the horizontal orientation, simple broad facets, short proportions, and original margin. No ornament or extra detail.

## Things/Item/Resource/Glacite/Glacite_a.png

Reference: `Things/Item/Resource/Pyrinth/Pyrinth_a.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 128x128 pixels. Small inventory stack A, one small squat blue shard. Same small size on canvas, same simple flat facet count.

## Things/Item/Resource/Glacite/Glacite_b.png

Reference: `Things/Item/Resource/Pyrinth/Pyrinth_b.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 128x128 pixels. Medium inventory stack B, one taller blue shard. Same size on canvas, same simple flat facet count.

## Things/Item/Resource/Glacite/Glacite_c.png

Reference: `Things/Item/Resource/Pyrinth/Pyrinth_c.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 128x128 pixels. Large inventory stack C, broad jagged blue shard pile. Same size on canvas, same simple flat facet count.

## Things/Mote/GlaciteSpark.png

Reference: `Things/Mote/PyrinthSpark.png`

Use the attached image as the DIRECT SPRITE REFERENCE for its cold blue counterpart in Glacite, an adaptation of Epochs - Pyrinth. Recreate its exact visual simplicity and composition. This is production in-game sprite art, not a detailed illustration. Keep the source canvas proportions, object scale and placement, bold near-black contour, low facet count, flat shaded metal and wood. Replace orange/red crystal with mid blue, cobalt shadows, pale cyan highlights. Preserve gray housing and brown wood. No added decoration, new objects, surface texture, perspective change, zoom, text, ground or shadow. Genuinely transparent background; preserve existing transparency. Intended final resolution 128x128 pixels. The same tiny soft four-point magical glint changed from orange to pale cyan blue. Preserve its diffuse transparent edges. This is a light effect, keep soft glow exactly as reference.

## Texture coverage

Each gameplay texture in Pyrinth has a Glacite counterpart. The original east and west pylon files are identical; Glacite retains that convention.

| Pyrinth texture | Glacite texture | Pixels | Preparation |
| --- | --- | --- | --- |
| `Things/Building/Filth/PyrinthFilth_A.png` | `Things/Building/Filth/GlaciteFilth_A.png` | 128 × 128 | generated |
| `Things/Building/Linked/Pyrinth_Atlas.png` | `Things/Building/Linked/Glacite_Atlas.png` | 2048 × 2048 | generated |
| `Things/Building/Misc/PyrinthHeater.png` | `Things/Building/Misc/GlaciteCooler.png` | 256 × 256 | generated |
| `Things/Building/Misc/PyrinthLamp.png` | `Things/Building/Misc/GlaciteLamp.png` | 128 × 128 | generated |
| `Things/Building/Misc/PyrinthBrazier/PyrinthBrazier_east.png` | `Things/Building/Misc/GlacitePylon/GlacitePylon_east.png` | 512 × 512 | generated |
| `Things/Building/Misc/PyrinthBrazier/PyrinthBrazier_north.png` | `Things/Building/Misc/GlacitePylon/GlacitePylon_north.png` | 512 × 512 | generated |
| `Things/Building/Misc/PyrinthBrazier/PyrinthBrazier_south.png` | `Things/Building/Misc/GlacitePylon/GlacitePylon_south.png` | 512 × 512 | generated |
| `Things/Building/Misc/PyrinthLamp/PyrinthLamp_a.png` | `Things/Building/Misc/GlaciteLamp/GlaciteLamp_a.png` | 128 × 128 | generated |
| `Things/Building/Misc/PyrinthLamp/PyrinthLamp_b.png` | `Things/Building/Misc/GlaciteLamp/GlaciteLamp_b.png` | 128 × 128 | generated |
| `Things/Building/Misc/PyrinthLamp/PyrinthLamp_c.png` | `Things/Building/Misc/GlaciteLamp/GlaciteLamp_c.png` | 128 × 128 | generated |
| `Things/Building/Misc/WallPyrinthTorch/WallPyrinthTorch_east.png` | `Things/Building/Misc/WallGlaciteTorch/WallGlaciteTorch_east.png` | 128 × 128 | generated |
| `Things/Building/Misc/WallPyrinthTorch/WallPyrinthTorch_north.png` | `Things/Building/Misc/WallGlaciteTorch/WallGlaciteTorch_north.png` | 128 × 128 | generated |
| `Things/Building/Misc/WallPyrinthTorch/WallPyrinthTorch_south.png` | `Things/Building/Misc/WallGlaciteTorch/WallGlaciteTorch_south.png` | 128 × 128 | generated |
| `Things/Building/Misc/WallPyrinthTorch/WallPyrinthTorch_UI.png` | `Things/Building/Misc/WallGlaciteTorch/WallGlaciteTorch_UI.png` | 256 × 256 | generated |
| `Things/Item/Equipment/WeaponMelee/PyrinthBlade.png` | `Things/Item/Equipment/WeaponMelee/GlaciteBlade.png` | 256 × 256 | generated |
| `Things/Item/Resource/Pyrinth/Pyrinth_a.png` | `Things/Item/Resource/Glacite/Glacite_a.png` | 128 × 128 | generated |
| `Things/Item/Resource/Pyrinth/Pyrinth_b.png` | `Things/Item/Resource/Glacite/Glacite_b.png` | 128 × 128 | generated |
| `Things/Item/Resource/Pyrinth/Pyrinth_c.png` | `Things/Item/Resource/Glacite/Glacite_c.png` | 128 × 128 | generated |
| `Things/Mote/PyrinthSpark.png` | `Things/Mote/GlaciteSpark.png` | 128 × 128 | generated |
| `Things/Building/Misc/PyrinthHeater_m.png` | `Things/Building/Misc/GlaciteCooler_m.png` | 256 × 256 | shader mask derived from replacement |
| `Things/Building/Misc/PyrinthBrazier/PyrinthBrazier_eastm.png` | `Things/Building/Misc/GlacitePylon/GlacitePylon_eastm.png` | 512 × 512 | shader mask derived from replacement |
| `Things/Building/Misc/PyrinthBrazier/PyrinthBrazier_northm.png` | `Things/Building/Misc/GlacitePylon/GlacitePylon_northm.png` | 512 × 512 | shader mask derived from replacement |
| `Things/Building/Misc/PyrinthBrazier/PyrinthBrazier_southm.png` | `Things/Building/Misc/GlacitePylon/GlacitePylon_southm.png` | 512 × 512 | shader mask derived from replacement |
| `Things/Building/Misc/PyrinthBrazier/PyrinthBrazier_westm.png` | `Things/Building/Misc/GlacitePylon/GlacitePylon_westm.png` | 512 × 512 | shader mask derived from replacement |
| `Things/Building/Misc/PyrinthBrazier/PyrinthBrazier_west.png` | `Things/Building/Misc/GlacitePylon/GlacitePylon_west.png` | 512 × 512 | same as east, matching original's identical pair |

The Glacite pylon uses the user's accepted stepped blue ice shard and simplified grey supports and slab, with their hand-painted construction mask. Sources, generation records and matching colour/mask recipes are in `artwork/design-sources/glacite-pylon/`. North and south use the selected view; east and west mirror it and the mask. Placement, rotation, tint and icon appearance await the combined in-game review.

The Glacite cooler now uses the user-accepted two-shard design in a continuous angular housing. Its source, generation records, export recipe and user-painted construction mask are in `artwork/design-sources/glacite-cooler/`. The colour and mask are integrated into `Textures/`; native placement, tint, icon and cooling behavior await the combined in-game review.


The Vitalite pylon uses the user-accepted tall rose crystal, smooth base and central gold band. Its source and uniform 1:2 export recipe are in `artwork/design-sources/vitalite-pylon-banded/`. The base accepts construction tint; the crystal and gold band retain their colours. Draw size `(1.08,2.16)` matches the 256x512 canvas without anisotropic stretching, and offset `(0,0,0.615625)` preserves the previous calculated foot position. The native icon scale is adjusted to preserve its previous effective size. The generated source has faint low-alpha debris outside its substantial bounds; candidate diagnostics and tint previews are under `work/vitalite-fixtures/pylon-resolved/`. The exact runtime appearance, mask edge and placement await the combined in-game review.

The full Vitalite bed uses the rose crystalline version of the stone bed accepted on 2026-11-05: a pale pink slab with broad subtle facets, three-crystal headboard, gold band and small foot crystals where visible. All four directions retain the corresponding stone-bed geometry. Sources, exact requests, attempt records and uniform export recipes are in `artwork/design-sources/vitalite-crystal-stone-bed/`. The south view is also the menu alias. Crystal and gold colours are fixed, so this Cutout graphic needs no construction mask. The earlier central-gem and branching-inlay design is retained as history in `artwork/design-sources/vitalite-full-bed-inlaid/`. Directional appearance and placement await the final in-game review.

The user accepted the Vitalite inlaid bed with a flat stone slab, three-crystal headboard, gold band and three small foot-face crystals where visible. Four directional sources, user-painted masks and uniform export recipes are in `artwork/design-sources/vitalite-stone-bed/`. Source exports and menu alias are integrated; placement, proportions, construction tints and appearance await the combined in-game review. North, east and west contain cool-grey stone with channel spread up to 30; their painted masks override diagnostic ownership polygons.

On 2026-11-02, staged and deployed 70 changed or missing artwork textures and selected Vitalite, Hollowstone and Sanguinite graphic definition fields to the installed Lumen Stones mod. Deployment and verification recorded 73 matching files in `work/deployments/artwork-20261102-stage/`, including backups of replaced installed files. The Vitalite banded column was excluded because its construction-material mask requires hand finishing; its exact recipe is in the mask review queue. Restart the game before the combined visual review.

The Glacite floor lamp uses the user-approved stepped blue shard on a fixed wood post and neutral collar. Source, generation record and export recipes are in `artwork/design-sources/glacite-floor-lamp/`; the three random textures include a mirrored view. Random selection, icon and placement await the combined in-game review. Graphic_Random is outside the current artwork registry.

GlaciteSpark uses the user-approved six-arm snowflake. Its source, generation record and export are in `artwork/design-sources/glacite-snowflake/`; small-scale appearance and animation await the final in-game review.

The Glacite wall lamp uses the user-approved shallow front view of a stepped blue shard and neutral collar. Its source, directional recipes and centered icon are in `artwork/design-sources/glacite-wall-lamp/`. Wall attachment, occlusion and native icon appearance await the combined in-game review.

The Glacite column uses the user-approved stepped blue crystal with two plain rounded bands, cap and foot. Its source and user-painted mask are preserved in `artwork/design-sources/glacite-column-stepped/`; the previous column source remains in `artwork/design-sources/glacite-column/`. Placement, material tint and icon appearance await the combined in-game review.

The Vitalite column's user-painted cap mask and accepted colour sprite were deployed and hash-verified on 2026-11-02 using `work/deployments/vitalite-column-reviewed/`. Restart the game for the combined visual review.

The Lumenstone pylon, column and cluster were redesigned around the altar's softened radiating diamonds and exported on 2026-11-03. The user approved the designs and reviewed the construction masks, adding outlines to the pylon and cluster. The exact reviewed colour and mask sources, repaired alpha, coordinated export recipes and output masks are preserved under `artwork/design-sources/lumenstone-diamond-2026-11-03/`. Exterior alpha values of 8 or less were removed before the shared crop and reduction. The pylon's existing draw size is larger than the column's; actual relative scale, anchoring, material tint and appearance await the combined in-game review. These source exports have not been deployed.
