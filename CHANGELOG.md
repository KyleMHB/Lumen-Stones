# Changelog

<!-- Agent read route: read only the newest relevant release; locate older versions by searching `^## ` headings. -->

## 2026-11-01

Version 2.0. This release does not work with existing saves. Start a new colony.

### Added

- Added Lumenstone, one raw resource for every stone. It comes from rare veins, surface stacks and traders at about 100 silver each, and traders won't buy it back.
- Added infusion at the Lumen ritual circle: 10 Lumenstone plus a simple reagent makes 10 of any researched stone, with no cooldown. Glacite needs no reagent.
- Added the Lumenstone tab in Mod options for all supply settings.

### Changed

- The Lumen ritual circle no longer needs Ideology. It costs stone blocks and 5 Lumenstone, works bills and pauses them during a ceremony.
- One Lumenstone ritual replaces all ritual variants. It uses 5 stone chunks and yields 15, 20 or 25 Lumenstone by quality, scaled by participation, with a five-day cooldown per settlement.
- With Epochs - Pyrinth, Pyrinth no longer spawns or drills. Infuse it from Lumenstone and wood.

### Removed

- Removed rough stone chunks, their cutting recipes and Glacite veins, deep drilling and rubble.

### Internal

- Added named join close-ups for separate-layer artwork and required renewed visual inspection after split or placement changes.
- Added ownership polygons for artwork masks and documented when to use polygons or separate material layers.
- Added `prepare-candidate` to export artwork, check it and create labelled CPU tint views with zoomed material joins.
- Added production recipe checks that compare fresh exports with registered texture pixels, including staged pre-commit checks. Views without a recipe report that limitation.

## 2026-10-31

### Added

- Added Serenite furnishings with weighted room support for rest, comfort and mood, plus a utility pendant for rest and tranquility.
- Added Verdite farming fixtures with scheduled growing lights and a scythe that provides portable farming support and heals injuries from damage to living organic targets.
- Added Aegite wards that suppress fires before thinning filth, plus a blunt warhammer with stun and equipped fire protection.
- Added Ferrite columns, clusters and a forge pylon that share slow repair budgets across eligible targets, plus a utility shield with armour and personal gear mending.
- Restored native Ideology ceremonies through one Lumen ritual circle, with offering choices, yields based on participation and one three-day settlement cooldown shared by every stone and circle.

### Changed

- Standardised Glacite definition IDs, settings keys and texture paths. Existing saves using the previous IDs may lose Glacite content; reapply custom Glacite settings after updating.
- Combined rough lumen chunks into one rare surface pool. Each stone has its own research, trader supplies and a recipe that cuts one chunk into five usable pieces.
- Gave the slim floor lamps bases tinted by construction material while preserving their crystal colours.
- Replaced Serenite's pointed lotus column with a sanctuary frame and matched its cluster and shrine to that design. Refined Verdite's leaf clasps and three-spire cluster, gave Aegite a broader sentinel pylon, and changed Ferrite's pylon to a squat forge core.
- Expanded the existing availability, balance and cost settings to cover the new stones and equipment.
- Pylons now occupy one cell and stand on a shared base line, with tall artwork and matching menu icons.
- Floor torches now cost five wood instead of a chosen material. Torches already built or packed up switch to the same look when a save loads.

### Fixed

- Reduced the pale fringe where Glacite crystals meet their cooler and pylon housings on dark and brown materials.
- Opening the Lumen ceremony menu no longer adds rituals to your ideology. A ritual is added when you choose it.
- A future game update to wound infection now disables only Vitalite's infection prevention, not the rest of the mod.
- Reduced the performance cost of Ferrite repair on large maps.

## 2026-10-30

### Added

- Added per-stone and per-item availability, balance, costs and acquisition settings, with active-mod-list defaults and per-stone resets.
- Added configurable Vitalite infection prevention for enclosed hospital rooms and bed occupants, independent of immunity and existing infections.
- Added optional base Pyrinth overrides and expanded AutoConfig support. External overrides start off.

### Removed

- Retired Vitalite restoration ceremonies and research. Exploration, trade and chunk cutting remain available; legacy content stays loadable in existing saves.

### Changed

- Changed Vitalite room support to reach the full +25% healing and +10% immunity bonus with any three distinct fixture types. One or two types provide partial support; further types add no medical bonus.
- Split Workshop item illustrations into individual rows and moved group headings and cooling explanations into the page text.

## 2026-10-29

### Added

- Added Vitalite restorative columns, lamps, pylons and crystal clusters with pearl-white and pale rose artwork.
- Added rare natural Vitalite chunks, occasional small trader stocks and a stonecutting recipe yielding five usable pieces per chunk.
- Added optional Ideology restoration ceremonies using Glacite and a choice of medicine, with optional Pyrinth offerings and a shared three-day settlement cooldown.
- Added a capped hospital-room bonus that rewards five different Vitalite fixture types, with no medical benefit from duplicates.
- Added an affordable Vitalite-inlaid stone bed and an expensive bed made entirely from crystalline Vitalite. Room support adds to their occupant bonuses.
- Added Vitalite column visibility settings and included them in the optional all-columns preset.

### Changed

- Moved Vitalite restoration research to Neolithic with Stonecutting as its prerequisite, keeping its 1,200 research cost.
- Replaced Vitalite mining and deep drilling with rough chunks.
- Restored Glacite's original sparkle animation, removing the slow downward drift.
- Replaced the casket with a full crystalline Vitalite bed. Both bed types provide their healing and immunity support without refueling.
- Replaced the inlaid bed's edging with a crystalline pillow and three inset crystals, decreasing in size from neck to feet.
- Gave both beds separate directional sprites and a south-facing placement default to preserve their perspective when rotated.
- Matched bed proportions across all four views and changed the inlaid bed to smaller, rough crystal insets.
- Matched Vitalite lamp and pylon sizes to the existing stones, aligned wall lamps to walls and corrected the pylon's material mask.
- Flattened the restorative cluster's bowl shading and centered the Glacite and Pyrinth pylon menu icons.

### Fixed

- Preserved crystal colors in the Glacite, Vitalite and Pyrinth pylon menu icons and the inlaid bed icon when choosing construction materials.
- Restored native draw layers for Vitalite beds and low furnishings.
- Raised columns and pylons above tree canopies and kept crystal wall torches below the tall fixtures.
- Removed stray material tint from the inlaid bed's crystal pillow and insets in all four views and its menu icon.

### Internal

- Added artwork checks, approved-master records and material/icon review sheets.
- Added staged deployments with XML field selection, backups and hash verification, plus a local pre-commit artwork guardrail.

## 2026-10-28

### Added

- Added firefly jars, animated firefly columns and Glacite columns from Lumenstone Columns.
- Added optional Pyrinth columns, column visibility settings and a Pyrinth heat switch.
- Added an optional Auto Mod Config preset for the combined mod.

- Added mineable blue glacite deposits and a deep-drilling resource.
- Added lamps, a pylon and a cooler that absorb heat without fuel or electricity.
- Added a craftable crystal blade with a chance to inflict frostbite.
- Added new artwork for the resource, deposit, buildings and blade.
- Added three torch variants, three resource stack sprites, directional building sprites, rubble and a blue glint effect.
- Added a freezing burst when a cooler takes severe damage.
- Added a Steam Workshop description adapted from Pyrinth's page.

### Changed

- Replaced the firefly column sprite with a continuous amber glass chamber and pale caps, with no middle band.
- Combined Glacite and Lumenstone Columns as Lumen Stones, with Glacite included directly and Pyrinth remaining optional.

- Renamed the mod and material to Glacite.
- Changed ambient sparkles to settle slowly toward the crystal base, with a longer fade and fewer particles.

