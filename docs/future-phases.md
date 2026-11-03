# Lumen Stones future phases

Planning notes requested on 2026-10-28. Future phases are not implemented features or approval to begin implementation.

## Completed phase: consolidation

Lumen Stones now includes Glacite, firefly jars, firefly columns and Glacite columns. Pyrinth columns load only when Epochs - Pyrinth is active. The combined mod requires Harmony.

## Requested direction

- Combine Glacite and Lumenstone Columns under the name Lumen Stones.
- Keep the core mod independent of Epochs - Pyrinth. When Pyrinth is installed, use its resource for optional pyrinth columns and other future constructions.
- Add more stone types and uses over time.
- Existing-save compatibility and migration are not requirements for the combined release.
- The first new type is Vitalite, a healing stone. It has no temperature effect. It improves room sterility, aids healing and helps fight infections. It does not remove dirt or other filth. The accepted visual and medical direction is recorded below.

## Accepted healing-stone direction

Accepted design decisions, updated on 2026-10-29:

- Use pearl white with a pale rose core, distinct from glacite blue, pyrinth orange and firefly green.
- Deliver the full Vitalite fixture range: column, floor lamp, wall lamp, pylon, restorative cluster and restorative casket. The cluster is dedicated hospital furniture with several crystals in a low, rounded housing, serving as the counterpart to the Glacite cooler. Vitalite has no temperature effect. The casket is now part of this delivery, superseding its earlier deferral.
- Give Vitalite columns shallow rounded caps and matching rounded bases whose appearance follows the chosen building material. Pylons use shorter exposed crystal shafts on low matching bases with no top caps. Use simple facets and a pronounced pale rose core for readability at game scale.
- Animate soft, gentle pulses of pearl-white and pale rose motes moving outward from the crystal and fading nearby. Keep the effect sparse and leave clear gaps between pulses.
- Provide modest wound-healing and immunity support for resting patients. Keep doctors, tending and medicine useful. Ordinary fixtures should not directly erase infections.
- The column, floor lamp, wall lamp, pylon and restorative cluster each provide one equal share of a capped room healing and immunity benefit. All five distinct types are required for maximum room support; duplicate fixtures add no medical benefit. Beds and caskets are not required to complete the room set.
- The casket provides stronger wound-healing and immunity support only to its occupant, consuming Vitalite during treatment. Normal tending and medicine remain useful; missing limbs, permanent scars and resurrection are excluded.
- Include an affordable stone slab bed with Vitalite edging or inlay. It gives its occupant a smaller restorative benefit and consumes no Vitalite during use, providing progression toward a casket with a substantially higher Vitalite construction cost and ongoing treatment consumption.
- Add passive room support to a bed or casket's occupant benefit without multiplying bonuses. Ordinary beds receive room support; inlaid beds add their smaller occupant bonus; caskets add their stronger occupant treatment. The inlaid bed and casket provide their own occupant benefit without requiring passive fixtures.
- Establish this stone before adding more stone types. A separate cleaning stone is a future idea; its behavior, color and animation remain undecided. Growth, tranquility and ward stones remain candidates for later discussion, not committed release content.

The healing stone is named Vitalite. Its agreed fixture scope, behavior and implementation defaults are recorded in the [Vitalite ticket](../.scratch/healing-stone/issues/01-add-vitalite.md). The design interview is complete. Exact balance, resource costs, casket treatment consumption rate and research requirements are to be selected and documented during implementation, following the ticket's defaults.

## Vitalite follow-up: chunks, rituals and crystalline casket

Accepted direction on 2026-10-29; design interview complete, not yet implemented:

- Replace Vitalite's surface deposits and deep-drilling availability with rough Vitalite chunks. Process chunks at a stonecutting table into the usable Vitalite pieces consumed by existing furnishings and casket treatment. This advances Vitalite chunk processing from the later stonecutting phase; it does not add cut blocks or walls.
- For players with Ideology, make rituals the main renewable source of Vitalite using Ideology's ritual system. Provide a Glacite-and-medicine ritual without requiring Epochs - Pyrinth, with a more productive optional recipe using Pyrinth when installed.
- Natural Vitalite chunks should be rare, scattered during map generation, with occasional trader stock. There are no mineable deposits or deep-drilling sources. Rituals produce chunks, which must then be processed at a stonecutting table. This supersedes the proposed rare surface deposits.
- Rituals require Ideology. The core mod and Vitalite furnishings remain usable without it; players without Ideology rely on rare natural chunks and trade. Do not add a standalone ritual system or substitute production recipe for those players.
- Every completed ritual consumes 20 Glacite and two medicine of the selected tier. Its base output is 4–6 chunks depending on ritual quality; poor completed ceremonies retain a guaranteed yield.
- Herbal medicine gives the base yield. Industrial medicine adds one chunk; glitterworld medicine adds two. The optional Pyrinth offering consumes 10 Pyrinth and adds two chunks. Medicine and Pyrinth bonuses are additive, before the participation adjustment.
- A ceremony takes roughly two in-game hours. One pawn produces 50% of the calculated yield; two pawns produce 75%; a leader with at least two other participants produces the full yield. Round down to whole chunks, with at least one chunk guaranteed. Additional participants can contribute to native ritual quality but do not increase the participation multiplier above 100%.
- A three-day cooldown is shared across the settlement, across all ritual circles and both recipes. Changing the medicine tier does not bypass it.
- One Vitalite chunk yields five usable Vitalite pieces at a stonecutting table, unlocked by ordinary Stonecutting research. No Vitalite blocks or walls are introduced.
- The Vitalite ritual is unlocked by the existing Vitalite restoration research and is available to any ideoligion, without a required meme or selection during ideology creation.
- Make the all-Vitalite casket more visibly crystalline through a jagged rim, angular outer walls and a few broad facets in its interior. Preserve its coffin silhouette, clear sleeping recess and the existing stones' simple sprite style.

The agreed starting economy gives a full-participation herbal ceremony 20–30 usable pieces after stonecutting, before any Pyrinth offering. The existing casket construction cost remains 250 pieces. Exact rare-chunk map/trader frequencies, stonecutting work and the mapping of native quality outcomes onto the 4–6 base yield are implementation choices within this direction. [Ticket 02](../.scratch/healing-stone/issues/02-vitalite-chunks-rituals-and-casket.md) carries these decisions and acceptance criteria; this interview does not authorize implementation.

## Accepted expansion: Serenite, Verdite, Aegite and Ferrite

Accepted design decisions on 2026-10-30; design interview complete, to be implemented one stone at a time (`Serenite` -> `Verdite` -> `Aegite` -> `Ferrite`):

### Shared acquisition, research and ritual rules

- All five chunk-based lumen stones (`Vitalite`, `Serenite`, `Verdite`, `Aegite` and `Ferrite`) share the same acquisition model: no mineable rock veins and no deep-drilling sources.
- Map generation uses one shared random chunk-scatter pool (`~4–8` total chunks on a standard 250×250 map, chosen randomly across the five chunk types) so map generation is not bloated. A single map might not naturally spawn every stone type.
- Bulk-goods and exotic traders occasionally carry small stocks of chunks.
- Ordinary `Stonecutting` research unlocks cutting any rough lumen chunk into `5` usable crystal pieces at a stonecutting table, as well as each stone's basic column, floor lamp and wall lamp.
- Each stone has its own `1200`-cost Neolithic research project after `Stonecutting` (`KM_VitaliteRestoration`, `KM_SereniteTranquility`, `KM_VerditeGrowth`, `KM_AegiteWarding` and `KM_FerriteForging`), which unlocks that stone's pylon, cluster and Ideology ritual.
- Replace the Vitalite-specific circle with one shared **Lumen ritual circle** costing `30` stone blocks and `10` Glacite, allowing colonies to grow any researched stone even if none spawned on the map.
- All five stones' rituals share a single settlement-wide **three-day cooldown**, two-hour ceremony duration (`5,000` ticks), base quality yield (`4 / 5 / 6` chunks for poor / successful / exceptional), participation multipliers (`50% / 75% / 100%` for `1 / 2 / 3+` participants, minimum `1` chunk) and optional `+10 Pyrinth` offering (`+2` chunks when *Epochs - Pyrinth* is active).

### 1. Serenite (tranquility stone)

- **Palette and animation:** Soft lavender crystal with a deep amethyst/violet core and soft lavender room light. Emits a slow, dreamy upward drift of sparse lavender and deep violet motes that float gently upward like soothing incense wisps and fade out slowly.
- **Ritual offerings:** `20` Glacite plus tiered calming consumables: `4` Smokeleaf joints or Psychite tea (`+0` chunks), `20` Chocolate (`+1` chunk) or `4` Ambrosia (`+2` chunks), plus optional `10` Pyrinth (`+2` chunks).
- **Fixture lineup, artwork and room-set model:** Matches Vitalite's five-fixture enclosed-room model (`column`, `floor lamp`, `wall lamp`, `pylon` and `soothing cluster`), with high fixture `Beauty` instead of `Cleanliness`. Floor lamps across all stones (including Vitalite) use `GlaciteLamp`'s native slim silhouette (`~48px` wide on `128×128`, `<drawSize>(1,1)</drawSize>`) with shared stuff-tinted bases. Serenite's architectural fixtures use sanctuary lotus motifs: a **lotus-baluster column** (flared lotus-petal base and capital cradling a curved hourglass lavender-amethyst shaft), a **lotus-dish soothing cluster** (`1×1`, sculpted lotus-petal incense dish cradling polished teardrop amethyst crystals) and a **sanctuary shrine pylon** (`2×2`, tiered meditation basin with sweeping crescent arms cradling a large lotus-cut crystal core). Includes no dedicated Serenite beds; instead, it buffs existing vanilla beds and seating in the room.
- **Effects:** Each of the five distinct fixture types in an enclosed room adds `+4%` sleep restoration (`RestRateMultiplier`, up to `+20%`), `+0.04` `Comfort` to beds and seating in the room (up to `+0.20`) and `+1` live situational Mood while in the room (up to `+5` Mood). Sleeping in a Serenite-supported room also grants a lingering `+3` Mood memory for **2 in-game hours** after waking.
- **Apparel/Utility — Serenite pendant:** Soothing crystal amulet (`100` Serenite + `20` Silver, `Smithing` research, `Crafting 6`) worn in the `Belt`/Utility slot. Grants `+15%` `RestRateMultiplier`, `+0.15` `Comfort`, `-12%` `MentalBreakThreshold`, `+15%` `SocialImpact` and a steady `+4` Mood thought while worn.

### 2. Verdite (growth stone)

- **Palette and animation:** Spring peridot-green crystal with a warm sunlit golden-chartreuse core. On the three active agricultural fixtures (`column`, `cluster` and `pylon`), sparse golden-chartreuse and peridot pollen motes drift outward in a gentle arc and settle near the base.
- **Ritual offerings:** `20` Glacite plus tiered flora offerings: `50` Hay (`+0` chunks), `6` Herbal medicine / Healroot (`+1` chunk) or `15` Devilstrand (`+2` chunks), plus optional `10` Pyrinth (`+2` chunks).
- **Fixture lineup and non-stacking radial model:** Works both outdoors and indoors within an `8`-tile radius. Bonuses do **not** stack across fixtures; a plant or farmer receives only the highest single covering fixture's bonus:
  - **Floor lamp and wall lamp:** Visual lighting only (no agricultural bonus or pollen motes); floor lamp uses the shared slim `~48px` silhouette.
  - **Verdite column (Tier 1):** **Root-clasped arbor column** with a rough-hewn botanical base and crown and carved leaf/root clasps wrapping around an organic green-and-gold crystal trunk; provides `+10%` plant growth speed and `+8%` farmer `PlantWorkSpeed` and `PlantHarvestYield` (normal light, no sun-lamp overlight).
  - **Verdite growth cluster (Tier 2):** Three smaller natural faceted crystal spires sitting directly on the ground with no caps or base material, with a total size half that of the pylon. Provides `+15%` plant growth speed, `+12%` farmer `PlantWorkSpeed` and `PlantHarvestYield`, and a `4.5`-tile plant-capable `overlightRadius`.
  - **Verdite pylon (Tier 3, highest):** A large solid chunk of natural faceted crystal, wide at the base and narrowing upward, sitting directly on the ground with no caps or base material. Provides `+20%` plant growth speed, `+15%` farmer `PlantWorkSpeed` and `PlantHarvestYield`, and a `6.5`-tile plant-capable `overlightRadius`.
- **Weapon — Verdite scythe:** Melee weapon at the Glacite blade tier (`100` Verdite + `20` Steel, `LongBlades` research, `Crafting 6`). Heals the wielder's non-permanent injuries by **3% of melee damage dealt** to living organic targets and grants `+15%` `PlantWorkSpeed` and `PlantHarvestYield` while equipped.

### 3. Aegite (ward and purification stone)

- **Palette and animation:** Pale champagne-gold crystal with a bright white-gold core, combining automatic fire suppression and gradual filth cleaning into one stone (superseding a separate cleaning stone). Emits a brief white-gold glint on each cleaned filth or dampened fire tile, plus sparse champagne-gold ward sparkles on the three active fixtures (`column`, `cluster` and `pylon`).
- **Ritual offerings:** `20` Glacite plus tiered precious minerals: `100` Silver (`+0` chunks), `20` Jade (`+1` chunk) or `10` Gold (`+2` chunks), plus optional `10` Pyrinth (`+2` chunks).
- **Fixture lineup and steady-rate pulse model:** Floor lamp and wall lamp emit light only. Active fixtures use sacred ward/obelisk architecture and require no fuel or capacity pool; each pulses on a fixed interval within its radius (both indoors and outdoors), prioritizing fire suppression before thinning filth (`ThinFilth()`):
  - **Aegite column (Tier 1):** **Stepped ward-obelisk column** with a sharp stepped-pyramid base and beveled sentinel crown clamping a four-sided diamond-cut champagne-gold obelisk shaft; `8`-tile radius, pulses every `6s` (`360` ticks) to dampen up to `2` fires (`-0.5` fire size each) or clean `1` filth layer.
  - **Aegite ward cluster (Tier 2):** **Tripod ward brazier** (`1×1`) holding a crisp geometry of symmetric, diamond-cut champagne-gold obelisk prisms; `8`-tile radius, pulses every `4s` (`240` ticks) to dampen up to `3` fires (`-0.6` fire size each) or clean up to `2` filth layers.
  - **Aegite pylon (Tier 3, highest):** **Stepped sentinel monolith** (`2×2`) on an octagonal ward-plinth with four angled corner buttresses holding a tall, razor-sharp white-gold and champagne obelisk crystal; `10`-tile radius, pulses every `3s` (`180` ticks) to dampen up to `4` fires (`-0.75` fire size each) or clean up to `2` filth layers.
- **Weapon — Aegite warhammer:** Blunt melee weapon (`100` Aegite + `20` Steel, `Smithing`/`LongBlades` research, `Crafting 6`) with a chance to **Stun** on hit and equipped warding stats (`-100%` `Flammability` so the wielder cannot catch fire, plus `+10%` `ArmorRating_Sharp` and `ArmorRating_Blunt`).

### 4. Ferrite (forge and mending stone)

- **Palette and animation:** Metallic slate-silver crystal with a glowing burnished-copper core. Emits a brief copper-silver forge spark on each repaired building, stored item or pawn's gear, plus sparse copper-silver sparks on the three active fixtures (`column`, `cluster` and `pylon`).
- **Ritual offerings:** `20` Glacite plus tiered forged materials: `30` Steel (`+0` chunks), `15` Uranium (`+1` chunk) or `15` Plasteel (`+2` chunks), plus optional `10` Pyrinth (`+2` chunks).
- **Fixture lineup and tiered repair pulse model:** Floor lamp and wall lamp emit light only. Active fixtures use heavy dwarf-forge/industrial architecture and pulse on a fuel-free fixed interval within their radius (both indoors and outdoors), repairing only non-rotting items/gear and player-owned buildings while keeping the `"Tainted"` corpse-apparel flag intact:
  - **Ferrite column (Tier 1 — structural repair):** **Riveted forge-girder column** with octagonal dwarf-forge collars, rivets and vertical side-braces caging a blocky copper-and-slate crystal core; `8`-tile radius, pulses every `5s` (`300` ticks) to restore hit points to damaged player buildings and walls in range.
  - **Ferrite mending cluster (Tier 2 — stockpile/armory repair):** **Bolted crucible rack** (`1×1`) with a heavy octagonal iron crucible base clamping three thick hexagonal-cut slate-silver and glowing copper crystal rods; `8`-tile radius, pulses every `5s` (`300` ticks) to restore hit points to damaged non-rotting items stored on shelves/stockpiles or lying on the ground in range.
  - **Ferrite pylon (Tier 3, highest — master forge spire):** **Riveted forge-anvil core** (`2×2`) on a heavy foundry anvil pedestal with iron claw-clamps locking down a broad, glowing forge-heart crystal of burnished copper and slate-silver; `10`-tile radius, pulses every `3s` (`180` ticks) to restore hit points to damaged player buildings, stored/ground items **and** worn apparel or equipped weapons on nearby colonists.
- **Apparel/Utility — Ferrite buckler/shield:** Physical crystal shield (`100` Ferrite + `20` Steel, `Smithing`/`LongBlades` research, `Crafting 6`) worn in the `Belt`/Utility slot and usable with any melee or ranged weapon. Grants `+25%` `ArmorRating_Sharp` and `ArmorRating_Blunt`, `+10%` `MeleeDodgeChance` and `+15%` `ConstructionSpeed` and `GeneralLaborSpeed`, and slowly repairs itself and the wearer's equipped gear over time.

## Later phase: stonecutting and material walls

### Rough rocks and cut blocks

- Produce cut lumen-stone blocks from rough lumen rocks at a stonecutting table, following the vanilla stone-chunk-to-block workflow.
- Give each stone type its own research project to unlock cutting that type into blocks.
- Require the corresponding cut blocks to build that stone's walls.
- Decide recipe yields, research costs and how rough rocks relate to the existing mined crystal resources before implementation.
- Gate Pyrinth-specific recipes and research behind the presence of Epochs - Pyrinth. They must not become requirements for the core mod.

### Walls

- Add walls for the lumen-stone materials.
- Match each wall's appearance to the stone from which it is made.
- Let walls express their material's effects, including cooling for glacite and the healing stone's restorative and sterility effects.
- Use very sparse animations. Depending on the material, particles may move along connected walls or appear occasionally at random positions along their length.
- Treat a connected wall run as a visual whole so long walls do not produce a busy effect at every tile.
- Resolve room boundaries, effect strength, stacking limits and the total particle budget before building this phase.

## Candidate DLC integrations

Ideas the user picked from a DLC brainstorm on 2026-11-03. These are candidates, not accepted designs; each needs a design interview before a ticket. Gate DLC content with `MayRequire` so the core mod stays playable without the DLC.

### Biotech: Sanguinite and deathrest

- Sanguinite is the natural tie to Biotech's hemogen and sanguophages.
- Sanguinite counts as a deathrest building and speeds up deathrest.
- Open: which Sanguinite fixtures count, how the bonus compares to vanilla deathrest buildings, whether it draws Warp energy, and how it counts toward the deathrest building limit.

### Anomaly: dark stone containment

- Rather than adding a new containment stone (the brainstormed Abyssite), give one existing dark stone a role in Anomaly entity containment, such as raising holding-platform containment strength.
- Open: which stone takes the role. Hollowstone already requires Anomaly for Work husks; Dreadstone's subjugation theme also fits holding captive entities. Also open: which fixtures contribute and whether the effect needs Warp energy.

### Ideology: Lumen veneration, style and reliquary

- Lumen veneration precepts and memes: followers approve of Lumenstone fixtures and find dark stones abhorrent, or the reverse for a dark ideoligion. This mirrors the existing Lumenstone shrine and Dark Stone shrine exclusion rules.
- A Lumen ideoligion style that reskins fixtures.
- A relic reliquary built as a Lumenstone fixture.
- Lumenstone itself slightly raises certainty and the quality of any ritual, not just the Lumenstone ritual. Keep this much more subtle than a dedicated stone would be.
- Open: precept strengths and which fixtures trigger approval or disapproval, which fixtures the style reskins (each reskin is new artwork), whether the reliquary counts toward Lumenstone ritual support or shrine rules, and what Lumenstone source (raw stack, fixtures or altar) gives the certainty and ritual bonus.

### Odyssey: fishing, hot biomes and Riftstone

- Verdite pylons near water raise fish populations.
- Glacite is preferred in Odyssey's hot new biomes.
- Riftstone, a new dark stone (deep cobalt with a tearing magenta seam): a sacrifice powers a gravship jump instead of normal fuel, using Warp energy as gravship fuel. Riftstone fixtures on the ship's substructure soften landing damage.
- Open: the fish-population radius and size, what "preferred" means for Glacite (a stronger cooling effect, trader demand or biome-specific value), how Warp energy converts to jump range, and Riftstone's fixture range and acquisition.

## Current delivery: restorative casket

- Add a casket made from the healing stone that acts as a medieval healing pod.
- Make the entire casket from Vitalite, including rim, walls and base. Use a simple open coffin silhouette with angled shoulders and a tapered foot, referencing HRK Medieval deathrest coffin, Workshop ID 2881104575. The reference supplies visual direction only; treatment remains available without Biotech or deathrest. Match the other stones' flat, simple sprite style. The first generated set was rejected as too detailed.
- Provide accelerated wound healing and stronger immunity support to its occupant, consuming Vitalite during treatment while retaining normal tending and medicine. Exclude missing limbs, permanent scars and resurrection.
- Add a cheaper stone slab bed with Vitalite edging or inlay and a smaller occupant benefit as progression toward the expensive casket.
- The user supplied Fast regen (Workshop ID 943925765) as a reference for medical-bed progression. Its installed 1.6 XML uses native bed healing and immunity values, construction costs and research gates, with no treatment-fuel component in its human-bed definitions. This is a reference pattern, not agreed Vitalite balance. See the ticket for evidence and limitations.

## Current boundary

Consolidation, the Vitalite fixture range and both bed types are implemented. Ticket 02 adds rare chunks, stonecutting and optional Ideology restoration ceremonies. Subsequent user feedback replaces the casket with a full crystalline Vitalite bed and gives the cheaper inlaid bed a crystalline pillow and three central insets. Both beds are fuel-free. The earlier dated casket proposals above are superseded. Ticket completion notes record the remaining in-game verification. The design interview for Serenite, Verdite, Aegite (which combines fire suppression and filth cleaning) and Ferrite (which handles building and item/gear repair) is complete and recorded above for sequential implementation (`Serenite` -> `Verdite` -> `Aegite` -> `Ferrite`). Wider block-cutting research and material walls remain future work.
