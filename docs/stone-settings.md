# Stone settings

Open Options > Mod options > Lumen Stones. Select Lumenstone or a stone, then a section. Availability controls apply immediately. Balance, ingredient costs and effects require a restart and then apply to existing instances as well. Generation settings affect future maps.

Every balance control starts with the value from the active mod list. Checking an override stores a value; Reset removes it. Resetting a stone removes its overrides, including saved settings for temporarily absent Pyrinth content. Saved column visibility and Pyrinth heat settings migrate without disabling the rest of a stone's content. Saved overrides for removed keys are ignored.

**Fixture animation**, at the top of the window, allows the pulses, glints and fireflies of fixtures. It is on by default and applies immediately. Each fixture's own animation starts off and is switched on with its **Fixture animation** button. Turning the setting off removes that button and stops every fixture's animation without changing each fixture's switch. Animation never changes what a fixture does.

Disabling a stone or item blocks new construction, crafting and its infusion recipe. Lumenstone itself has no off toggle. Existing blueprints can still finish and existing furnishings keep their effects. No definitions or placed things are deleted.

Base Epochs - Pyrinth settings are available only when that mod and its definitions are loaded. Its balance and availability remain unchanged until **Override Epochs - Pyrinth balance** is enabled. The Lumen Stones Pyrinth column and the Pyrinth infusion toggle work regardless of that switch. Pyrinth deposits and deep drilling are patched to zero, and no setting restores them. Pyrinth add-ons are excluded.

## Darklight

The Darklight tab controls the initial mode of newly placed player fixtures and jars of fireflies. Choose **Off (Normal light)**, **Auto (by Ideoligion)** (the default), or **Always Darklight**. Auto uses Darklight when the player's primary ideoligion prefers darkness, and Normal without Ideology. Existing fixtures retain their chosen mode. Reset Darklight restores Auto.

Each supported fixture has a light toggle, available without Ideology. A mixed selection switches every fixture to the clicked button's target mode, using each stone's own deeper hue. Sprites and decorative effects keep their colours. Roofed lit tiles count as darklight only when at least one darklight source and no normal light reach them, including faint light from overlapping lamps or natural Lumenstone veins. Vanilla darklights can share the room. Cave plants still die in the light.

Verdite spire clusters and pylons retain their ordinary light around the clock. Darklight disables their separate plant-capable light and its Solar drain; Normal restores its powered daily schedule. Farming bonuses are unchanged. External Pyrinth fixtures support Darklight only with **Override Epochs - Pyrinth balance** enabled; the Lumen Stones Pyrinth column always supports it when present.

For AutoConfig, store `Darklight.defaultMode` in `overrides`: `0` for Off, `1` for Auto, `2` for Always Darklight. This setting applies to future placements without a restart.

## Vitalite support

Room fixtures fall into four categories: pylon (45%), restorative cluster (25%), column (20%) and lamp (10%). A floor lamp and a wall lamp share the lamp category. Each category counts once per enclosed, fully roofed room at the highest weight present, and the weights sum to the room's support fraction. Duplicate fixtures do not add medical support. Healing, immunity and infection prevention are that fraction times their caps, which default to 25%, 10% and 10%. Each room fixture exposes `.support` (its category weight), and the caps can be changed. Beds keep their direct `.healing`, `.immunity` and `.infection` values.

Room infection prevention is the support fraction times `Vitalite.infectionCap` (10% by default). The inlaid and full beds add 5% and 15% occupant protection. Protection requires bed rest in an enclosed, fully roofed room. Bed and room protection multiply their remaining risk: a full bed with all four fixture categories leaves 0.85 × 0.90 = 76.5% of the original risk. A 20% chance becomes 15.3%. Immunity is independent; prevention never cures an existing infection.

Full Vitalite beds require Stonecutting and Complex Furniture.

## Lumenstone

The Lumenstone tab holds all supply settings: veins, surface finds, traders, the Ideology ritual and the circle's cost. Traders charge about seven times its market value: the global 1.4 buy factor times the Exorbitant 5, then difficulty and negotiator adjustments. Infusion recipes appear under each stone, and their ingredient counts use the game's recipe units: 2 for silver means 20 pieces.

## Lumenstone ritual support

With Ideology, Lumenstone fixtures in the same room as a Lumen ritual circle and within the support radius raise the Lumenstone ritual's quality (up to +20%) and yield (up to +40%). Fixtures use the same four categories as Vitalite and Serenite: pylon 45%, cluster 25%, column 20% and lamp 10%, with floor and wall lamps sharing the lamp slot. Support is full in an enclosed, fully roofed room and half outdoors or unroofed. A fixture in another room gives nothing. The circle and fixtures draw a silver-blue ring at the radius while selected or being placed. An enclosed room holding a circle and a Lumenstone fixture becomes a Lumenstone shrine, unless it also holds a bed, an unrelated workbench, a research bench or Dark Stone content.

## Dark Stone

With Ideology, the Dark Stone tab holds the Sacrificial altar, its rituals and the Bloodstone batteries. It is absent without Ideology. Raw Dark Stone and Bloodstone have no off toggle.

- **Crystallize Dark Stone:** `DarkStone.crystallizeInput` (Lumenstone consumed, default 20), `.crystallizePoor`, `.crystallizeSuccess` and `.crystallizeExceptional` (conversion share by outcome, 25%, 50%, 75%) and `.crystallizeCooldown` (days, default 3). Converted Dark Stone is `input x share x victim factor`, at least 1 and at most the input. The rest returns as Lumenstone.
- **Harvest Warp Energy:** `DarkStone.harvestPoor`, `.harvestSuccess` and `.harvestExceptional` (Warp energy by outcome, 50, 100, 150) and `.harvestCooldown` (days, default 0.25). Energy is the base amount times the victim factor, deposited into batteries within the link radius. Excess is lost.
- **Link and victims:** `DarkStone.linkRadius` (tiles, default 15), `.statusSlave` (1.25) and `.statusColonist` (1.5). The victim factor is a curve of market value times status: 0 gives 0.5, 1000 gives 1.0, 2000 or more gives 1.25. The curve points are not exposed.
- **Ritual support:** `DarkStone.ritualQualityCap`, `.ritualYieldCap` and `.ritualSupportRadius` (defaults 20%, 40%, 8 tiles). Dark Stone fixtures in the altar's room within the radius use the same four categories as the Lumenstone fixtures, full in an enclosed roofed room and half outdoors. Quality rises on both rituals, Crystallize conversion rises up to the Lumenstone input, and Harvest energy rises before deposit. The fixtures are `KM_LumenDarkStoneColumn`, `KM_DarkStoneLamp`, `KM_DarkStoneWallLamp`, `KM_DarkStonePylon` and `KM_DarkStoneCluster`, each with availability, light, cost, material and `.support` settings. An enclosed room with an altar and a Dark Stone fixture becomes a Dark Stone shrine, unless it holds a bed, an unrelated workbench, a research bench or Lumenstone content.
- **Warp network:** Warp energy behaves like Solar energy. Every Bloodstone battery on a map shares one network, and dark fixtures with an active effect have a **Warp active effect** toggle. When stored energy falls below 0.01, or a continuous drain cannot be paid, the network is depleted: every draw fails until stored energy reaches `DarkStone.warpHysteresis` (default 5). Fixtures without Warp energy, or toggled off, only give light.
- **Sacrificial altar:** `DarkStone.altarCost` (Lumenstone, default 50) and `.altarStuff` (stone blocks, default 100).
- **Batteries:** `KM_BloodstoneVessel` and `KM_BloodstoneObelisk` have the usual availability, light, cost and material settings, plus `.capacity` (Warp energy, default 100 and 400).
- **Dreadstone seed:** `DarkSeed.crystalCost` (Dreadstone per seed crystal, default 5), `.days` (days from Binding to full growth, 4), `.maxPain` (cap on the seed's own pain, 72%, under the 80% shock threshold), `.cutIntervalStart` and `.cutIntervalEnd` (mean hours between cuts, 12 when bound falling to 4 in the last stage), `.cutSeverity` (cut damage multiplier, 1) and `.baseYield` (12). The yield is `baseYield x victim factor`, at least 1, using the Crystallize victim factor at the moment of death. The seed crystal follows the Dreadstone availability toggle. A dormant seed can be bound without the Dark attunement research or the Binding cooldown, and waking only a seed starts no cooldown.

Moral cost has no settings. The thought `KM_WarpSacrifice` is -4 mood for 6 days, and can be edited in the definition.

## Sanguinite

With Ideology, Sanguinite fixtures treat patients resting in a bed in the same room within `Sanguinite.radius` tiles (default 8), spending Warp energy. Colonists, slaves and prisoners are patients. Only the strongest active fixture in reach treats a patient, and each treatment pass (250 ticks) slows bleeding and then does one more thing per patient: heal one scar, or advance a regrowth. Fixtures need Warp energy and their **Warp active effect** on, and lamps only give light.

- **Column:** slows bleeding to 70%, heals surface scars (not eyes) and regrows fingers and toes.
- **Cluster:** slows bleeding to 50%, heals every scar and also regrows eyes, ears, noses and jaws.
- **Pylon:** slows bleeding to 30%, heals every scar and also regrows internal organs. Nothing regrows limbs.
- **Costs:** `Sanguinite.bleedCost` (1 per bleeding patient per pass; unpaid, bleeding isn't slowed), `.scarCost` (5), `.digitCost` (5), `.smallPartCost` (20) and `.organCost` (100). A regrowth is paid gradually over `.digitDays` (1), `.smallPartDays` (3) and `.organDays` (7), and the part returns when it's paid off. Progress pauses and is kept when the patient leaves bed or Warp energy runs out.
- **Safety:** bionics and the parts under them are never regrown or removed. Eyes and eye scars are skipped when the patient's ideoligion approves of blindness.
- **Flensing Blade:** `KM_SanguiniteBlade` is fast and light, with a cut of 14 every 1.4 seconds against a gladius's 16 and a longsword's 23 every 2 seconds. Each damaging hit on a living, bleeding-capable target adds a Hemorrhage stack, up to `Sanguinite.hemorrhageMaxStacks` (3), and each stack fades after about ten seconds. The wielder bleeds at half rate only while it's equipped. It costs no Warp energy and has the usual damage, cost and material settings.
- **Items:** `KM_Sanguinite`, `KM_LumenSanguiniteColumn`, `KM_SanguiniteLamp`, `KM_SanguiniteWallLamp`, `KM_SanguiniteCluster` and `KM_SanguinitePylon` have the usual availability, light, cost and material settings. `Sanguinite.enabled` is the stone's availability toggle.

## Dreadstone

With Ideology, Dreadstone fixtures spend Warp energy to hold prisoners and slaves and to weaken enemies. Each needs Warp energy and its **Warp active effect** on, and lamps only give light. Reach is `Dreadstone.radius` tiles (default 8).

- **Column and pylon, subjugation:** prisoners and slaves of the colony in the same room lose resistance and will each day and slaves gain suppression: `Dreadstone.resistancePerDay` (1), `.willPerDay` (0.05) and `.suppressionPerDay` (10%) for a column, times `.pylonErosionFactor` (2) for a pylon. They also pay `Dreadstone.subjugationCost` (2 Warp energy per pawn per day). Covered pawns break out and rebel `Dreadstone.breakMtbFactor` times less often (5), and a result of "never" stays never. Only the strongest covering fixture counts.
- **Cluster and pylon, Dread:** hostile humanlikes in line of sight within reach get the Dread hediff, which lowers Moving, Consciousness and shooting accuracy by 15%, 10% and 15% for a cluster and 25%, 15% and 25% for a pylon. Each hostile costs `Dreadstone.dreadCostPerPass` (1) Warp energy each 250-tick pass, so an idle cluster costs nothing. Mechanoids and animals are immune.
- **Death Siphon:** when a pawn marked by a pylon or the Arbalest dies, `Dreadstone.siphonFraction` (1%) of its market value goes to any battery on the map. Energy with no spare capacity is lost. A cluster alone refunds nothing.
- **Dreadstone Arbalest:** `KM_DreadstoneArbalest` is a lighter, faster bow than the greatbow (range 26, damage 12, warmup and cooldown 1.3 seconds). Its bolts apply Cluster-strength Dread for `Dreadstone.arbalestDreadSeconds` (10) with the siphon mark. It costs no Warp energy to fire.
- **Items:** `KM_Dreadstone`, `KM_LumenDreadstoneColumn`, `KM_DreadstoneLamp`, `KM_DreadstoneWallLamp`, `KM_DreadstoneCluster`, `KM_DreadstonePylon` and the arbalest have the usual availability, light, cost and material settings. `Dreadstone.enabled` is the stone's availability toggle.

## Hollowstone

With Ideology, Hollowstone fixtures spend Warp energy to dull pain and steady minds. Each needs Warp energy and its **Warp active effect** on, and lamps only give light. Colonists and slaves in the same room as a fixture, within `Hollowstone.radius` tiles (default 8), get the Hollowstone ward hediff. Only the strongest covering fixture counts, and the ward clears soon after a pawn leaves reach or power is lost.

- **Column:** pain is multiplied by 0.75.
- **Cluster:** pain is multiplied by 0.5 and the mental break threshold is 5 percentage points lower.
- **Pylon:** pain is multiplied by 0.25 and the mental break threshold is 10 percentage points lower.
- **Cost:** `Hollowstone.wardCostPerDay` (1 Warp energy per covered pawn per day). Nothing is spent for pawns out of reach. Nothing blocks breaks outright.
- **Work husks:** with Ideology and Anomaly, the Sacrificial altar has a third ritual, **Raise a work husk**. It sacrifices a living prisoner, slave or colonist and raises them as a work husk bound to a Hollowstone pylon with no husk. It costs `Hollowstone.raiseCost` (50 Warp energy), refunded if the ceremony is cancelled or the victim is not raised, and recovers for `Hollowstone.raiseCooldown` days (0.25). A bound husk costs `Hollowstone.huskCostPerDay` (10) Warp energy a day. It hauls and cleans, can be drafted to fight unarmed, keeps what it wore, and feels no pain. If its pylon is destroyed, switched off or unpowered, it collapses without dying and wakes when power returns. A collapsed husk with no pylon can be bound to a free pylon from the pylon's **Rebind a husk** button. Without Anomaly none of this appears.
- **Hollowstone Glaive:** `KM_HollowstoneGlaive` is a slow, heavy blade (cut 28 every 2.8 seconds). While equipped it gives the wielder half pain and +25% pain shock threshold. It costs no Warp energy and has the usual damage, cost, material and equipped stat settings.
- **Items:** `KM_Hollowstone`, `KM_LumenHollowstoneColumn`, `KM_HollowstoneLamp`, `KM_HollowstoneWallLamp`, `KM_HollowstoneCluster`, `KM_HollowstonePylon` and the glaive have the usual availability, light, cost and material settings. `Hollowstone.enabled` is the stone's availability toggle.

## Sunstone and Solar energy

Sunstone collects daylight as Solar energy. Collectors and arrays gather it into small buffers, vessels and obelisks store it, and every battery and collector on a map shares one network. Each infused stone has a passive effect that costs nothing and an active effect that spends Solar energy. Every fixture with an active effect has a **Solar active effect** toggle, which appears once Sunstone channeling is researched and can be set on several selected buildings together.

**Sunstone.enabled** is the Sunstone availability toggle. Turning it off also removes the Solar requirement: active effects work for free, nothing drains, and toggles and status text are hidden. The remaining keys are in the Sunstone tab:

- **Solar network:** `Sunstone.collectorOutput` and `.arrayOutput` (energy per day at full sun, default 100 and 300), `.vesselCapacity` and `.obeliskCapacity` (default 300 and 1000), and `.solarHysteresis` (stored energy needed to recover from depletion, default 5). Collector buffers (50 and 150) are fixed in the definitions. Output is the nominal rate times sky glow, a weather factor and the share of the building's cells that are unroofed. Real output varies with latitude, season and time of day.
- **Weather:** `Sunstone.weather0` to `.weather3` are the output factors for fog (70%), rain and gentle snow (60%), thunderstorms (50%), and foggy rain and hard snow (40%). Other weather is full sun.
- **Items:** every Sunstone item has the usual availability, light, cost and material settings. Light settings are the radius at full glow: collectors and arrays dim to half at night, and batteries grow from about 40% to full as they fill.
- **Sunstone axe:** `KM_SunstoneAxe.baneMultiplier` (damage multiplier against Anomaly entities and mutants, default 1.5), `.procChance` (25%) and `.procChanceUnnatural` (50%) for the blind and stun proc, `.blindTicks` (300) and `.stunTicks` (60), plus the usual damage and cost settings.

- **Interruptions:** `Interruptions.enabled` (default on) lets map events pause active effects. A solar flare stops every Solar and Warp active effect and Sunstone collection; stored energy, passive effects, implants, prosthetics and worn gear are unaffected. A fixture linked to a Lunite stabilizer keeps running. With Sunstone off there are no interruptions.
- **Lunite:** `Lunite.sunstoneCost` (25 Sunstone) and `.yield` (25 Lunite) per conversion, and `.cooldownDays` (5, started only by a successful conversion). A conversion can only start between 20:00 and 04:00. `Lunite.linkRadius` (4 tiles) is how far a Lunite stabilizer reaches, measured to the fixture's nearest cell.
- **Aegite shield projector:** `KM_AegiteShieldProjector.radius` (10 tiles), `.hitPoints` (500), `.upkeep` (150 Solar energy per day while up), `.solarPerHp` (0.5 per hit point recharged), `.crushCost` (150 hit points per crushed hostile drop pod or infestation), `.rebootTicks` (60000 after breaking) and `.empTicks` (1500). Recharge speed is fixed at 1 hit point per 240 ticks.

When stored energy falls below 0.01, or a continuous drain cannot be paid, the network is depleted: every draw fails until stored energy reaches `Sunstone.solarHysteresis`.

Active-effect costs live with the stones that use them:

- `Glacite.freezerSolarDrain` (default 40 a day while the cooler is cooling between its two limits). `KM_GlaciteCooler.cutoff` is the active limit (-10 C by default) and `KM_GlaciteCooler.passiveCutoff` the limit it reaches without Solar energy (4 C, refrigeration). `.heat` applies to both.
- `Verdite.clusterSolarDrain` and `Verdite.pylonSolarDrain` (60 and 120 per lit schedule). For clusters and pylons, `.light` is the ordinary green glow, which is always on, and `.plantLight` is the plant-capable light, which runs only during the 25% to 80% schedule while powered.
- `Ferrite.solarCostPerHp` (0.5 per HP of items or worn gear repaired). Buildings and walls are repaired for free.
- `Aegite.solarCostPerFire` (2.0 per fire suppressed).
- `Vitalite.patientSolarDrain` (20 a day for each injured patient in a bed that has an active bonus).
- `Serenite.sleeperSolarDrain` (10 a day for each sleeper in a room with active support).

Passive effects always count every fixture. Active room bonuses count only fixtures whose toggle is on. A Vitalite bed's toggle controls only its own occupant bonus.

## Ferrite warhammer and Aegite defences

- **Ferrite warhammer:** `KM_FerriteWarhammer.stunChance` (20%) and `.stunTicks` (120) for the stun on damaging melee hits, and `.repairBudget` (0.0225% of maximum hit points per pulse, every 300 ticks) for the free mending of the hammer and the wielder's worn apparel and weapons. Replaces the old Aegite warhammer; the settings keys were renamed with it.
- **Aegite shield:** replaces the old Ferrite shield, with the usual availability, cost and material settings.
- **Aegite shield charm:** `KM_AegiteShieldCharm.energyMax` (0.60), `.rechargeRate` (0.08) and `.resetTicks` (4200).

## Animastone

With Royalty, the Animastone tab holds the psychic stone. It is absent without Royalty. Animastone is infused from 10 Lumenstone and 2 jade (jade counts are whole pieces) after Anima attunement. `Animastone.enabled` is the stone's availability toggle.

- **Meditation focus:** every fixture is a meditation focus for psycasters with the Natural or Dignified focus type. `.focus` on each fixture is its focus strength: lamps 8%, column 14%, cluster 18%, pylon 22%. Anima trees and other natural foci don't count Animastone fixtures as artificial buildings.
- **Room support:** fixtures fall into four categories: pylon (40%), cluster (25%), column (20%) and lamp (15%). A floor lamp and a wall lamp share the lamp category. Each category counts once per enclosed, fully roofed room at the highest weight present, and each fixture's `.support` is its weight. While a pawn meditates in the room, neural heat recovery rises by the support fraction times `Animastone.heatRecoveryCap` (50%), and psyfocus gain by the fraction times `Animastone.focusGainCap` (25%).
- **Solar active effects:** the cluster and pylon run only while a pawn meditates within `.activeRadius` (6 and 8 tiles), spending `.activeDrain` Solar energy a day (30 and 60). They work outdoors as well as in rooms. A running cluster raises its own focus strength by `.activeFocus` (6%). A running pylon raises meditating pawns' neural heat recovery by `.activeHeatRecovery` (25%) and gives anima trees in range `.activeGrowth` (+50% meditation plant growth, so anima grass grows faster).
- **Animastone circlet:** `KM_AnimastoneCirclet` is headgear on the Overhead layer, so it can't be worn with a crown or coronet. Its `.equipped.PsychicSensitivityOffset` (+8%) and `.equipped.PsychicEntropyRecoveryRate` (+0.054 a second, about 10% of the base rate) apply while worn.
- **Animastone staff:** `KM_AnimastoneStaff` is a weak blunt weapon. While wielded, `.equipped.PsychicEntropyGain` (-20%) lowers neural heat gained from every source and `.equipped.PsychicEntropyMaxOffset` (+15) raises the neural heat limit. A damaging melee hit stuns the target for `KM_AnimastoneStaff.stunTicks` (60) with a chance of `KM_AnimastoneStaff.stunChance` (15%) times the wielder's psychic sensitivity.
- **Items:** every Animastone item has the usual availability, light, cost and material settings. The circlet and staff need no Solar energy.

## Implants and prosthetics

The Implants tab holds the stone implants, crystal prosthetics and the portable cells. It appears once Lumen attunement content is loaded. Each part also follows its stone's availability: disabling Glacite hides Glacite part bills too.

- **Shared rules:** `Implants.stoneCost` (infused stone per implant, default 3) and `Prosthetics.stoneCost` (4), each plus one component. `Prosthetics.dormantEfficiency` (80%) is a dormant prosthetic's efficiency, including the parts it carries. `Implants.attunementCooldownDays` and, with Ideology, `Implants.bindingCooldownDays` (3 each) are per settlement and separate from the Lumenstone ritual and Crystallize.
- **Energy relief:** each active implant and each active dark prosthetic draws `Implants.drainPerDay` (3) Solar energy (light) or Warp energy (dark) on a home map. While paid, its drawback is reduced by `Implants.reliefFactor` (25%); when refused, the label shows `(unrelieved)`. Light prosthetics have no drawback and draw nothing. With Sunstone disabled, Solar draws are free.
- **Pairing:** with an active light and an active dark implant, both benefits use `Implants.pairedBenefitFactor` (80%). Drawbacks stay in full.
- **Portable cells:** without Ideology, the Sunstone cell holds Solar energy only. With Ideology the Twinstone cell replaces its bill and adds a Warp pool for dark parts. `Twinstone.capacity` (60 per pool, about ten days for two implants) applies to both. Away from a home map a cell feeds every implanted pawn on its map or in its caravan. At home it charges from the part of each network above half its capacity.
- **Each part:** one section per implant and prosthetic lists every benefit and drawback value it sets (for example `KM_GlaciteImplant.benefit.offset.ComfyTemperatureMax`), plus `.efficiency` for prosthetics and perk values such as `.dreadRadius`, `.daysPerScar`, `.apparelRepair`, `.partHealing` and `.mood`. Relief and pairing scale each value's distance from neutral, so a +20 offset at 75% is +15. Vitalite's sterility with Biotech is strict: it can't be relieved, so it draws no energy.
- **Verdite sunlight reserve:** the Verdite implant keeps a sunlight reserve hediff, starting at `.startReserve` (50%) when woken. Unroofed daylight fills it in `.hoursToFill` (4) hours of clear sun, scaled by sky glow and the Sunstone weather factors; a lit Verdite overlight fills it at `.overlightRate` (50%); a world caravan fills it in daylight. Otherwise it empties over `.daysToDrain` (1.5). Its stages set hunger: sun-fed (50% and up) `.hunger.sunfed` (60%), fading (15% to 50%) `.hunger.fading` (85%), sun-starved (below 15%) `.hunger.sunstarved` (130%). Only the sun-starved stage draws Solar energy for relief.

## Auto Mod Config

Auto Mod Config (Continued) is optional. The example in `Extras/Lumen-Stones-AutoConfig` enables content while leaving balance to the active mod list and external Pyrinth overrides off. Importing it replaces this mod's saved settings. Copy the example folder to the game's Mods folder as a separate mod and enable it with Auto Mod Config before Lumen Stones initializes.

All controls serialize through the ordinary `LumenStones.LumenstoneSettings` object. The filename is `Mod_Lumen-Stones_LumenstoneMod.xml`. Use `settingsVersion` 1. `overridePyrinth` is a boolean. `overrides` is a Scribe dictionary with parallel `keys/li` and `values/li` lists; numbers in the file use fractions for percentages and 0/1 for availability. Omitting a key restores the active mod list's default on the next restart.

```xml
<SettingsBlock>
  <ModSettings Class="LumenStones.LumenstoneSettings">
    <settingsVersion>1</settingsVersion>
    <overridePyrinth>false</overridePyrinth>
    <overrides>
      <keys>
        <li>Glacite.enabled</li>
        <li>KM_GlaciteCooler.heat</li>
        <li>KM_VitaliteCasket.infection</li>
        <li>Lumenstone.traderChance</li>
      </keys>
      <values><li>1</li><li>14</li><li>0.15</li><li>0.25</li></values>
    </overrides>
  </ModSettings>
</SettingsBlock>
```

Keys are case sensitive. Applicable item fields appear only when the loaded definition has that effect or cost. Export a settings file after changing controls for an exact preset for the active mod list.

| Key | Value |
| --- | --- |
| `Firefly.enabled`, `Glacite.enabled`, `Vitalite.enabled`, `Serenite.enabled`, `Verdite.enabled`, `Aegite.enabled`, `Ferrite.enabled`, `Pyrinth.enabled` | Stone availability, 0 or 1 |
| `<defName>.enabled` | Item availability, 0 or 1 |
| `<defName>.light` | Light radius in cells |
| `<defName>.heat` | Positive heating or cooling strength, heat per second; 0 disables the temperature effect |
| `<defName>.cutoff` | Heating maximum or cooling minimum, degrees C |
| `<defName>.healing`, `.immunity`, `.infection` | Fractional medical contribution, such as 0.05 for 5% |
| `<defName>.cleanliness` | Cleanliness contribution |
| `<defName>.cost.<ingredientDefName>` | Fixed construction or generated crafting recipe ingredient count |
| `<defName>.stuff` | Construction material or generated crafting recipe material count |
| `<recipeDefName>.ingredient.<index>` | Ingredient count in other recipes, zero-based XML order |
| `<defName>.damage.<index>` | Weapon tool damage, zero-based tool order |
| `<defName>.extraDamage.<toolIndex>.<damageIndex>` | Extra melee damage, zero-based order |
| `DV_MineablePyrinth.sizeMin`, `.sizeMax`, `.yield` | External Pyrinth deposit size and mining yield |
| `Lumenstone.veinFrequency`, `.veinSizeMin`, `.veinSizeMax`, `.veinYield` | Vein scatter weight, minimum/maximum size and yield per cell |
| `Lumenstone.surfaceChance`, `.surfaceStacksMin`, `.surfaceStacksMax`, `.surfaceCountMin`, `.surfaceCountMax` | Fractional chance per map, stacks and stack size |
| `Lumenstone.traderChance`, `.traderCountMin`, `.traderCountMax`, `.marketValue` | Fractional stock chance, stock range and market value |
| `Lumenstone.ritualPoor`, `.ritualSuccess`, `.ritualExceptional`, `.ritualCooldown`, `.ritualChunks` | Ritual yields by quality, cooldown in days and stone chunk cost; Ideology only |
| `Lumenstone.circleCost`, `.circleStuff` | Lumen ritual circle Lumenstone and stone block cost |
| `Lumenstone.ritualQualityCap`, `.ritualYieldCap`, `.ritualSupportRadius` | Ritual quality and yield bonus at full fixture support (fractions, default 20% and 40%) and the support radius in tiles (default 8); Ideology only |
| `KM_Lumenstone<Fixture>.support` | A Lumenstone fixture's category weight in room and ritual support |
| `Vitalite.healingCap`, `.immunityCap`, `.infectionCap` | Room healing, immunity and infection prevention caps, fractions |

Lumenstone fixtures are `KM_LumenstoneColumn`, `KM_LumenstoneLamp`, `KM_LumenstoneWallLamp`, `KM_LumenstonePylon` and `KM_LumenstoneCluster`. They share the Lumenstone tab and have availability, light, cost, material and `.support` settings, but raw Lumenstone has no off toggle.

Item identifiers are `KM_LumenFireflyJar`, `KM_LumenFireflyColumn`; `KM_Glacite`, `KM_LumenGlaciteColumn`, `KM_GlaciteLamp`, `KM_GlaciteWallLamp`, `KM_GlacitePylon`, `KM_GlaciteCooler`, `KM_GlaciteBlade`; `KM_Vitalite`, `KM_LumenVitaliteColumn`, `KM_VitaliteLamp`, `KM_VitaliteWallLamp`, `KM_VitalitePylon`, `KM_VitaliteCluster`, `KM_VitaliteSlabBed`, `KM_VitaliteCasket`; and optional `KM_LumenPyrinthColumn`, `DV_Pyrinth`, `DV_MineablePyrinth`, `DV_PyrinthLamp`, `DV_PyrinthWallLamp`, `DV_PyrinthBrazier`, `DV_PyrinthHeater`, `DV_MeleeWeapon_PyrinthBlade`.

Other mods can add `LumenStones.StockGenerator_Lumenstone` to more trader kinds; the shared trader settings apply to them too. Infusion recipes are `KM_InfuseGlacite`, `KM_InfuseVitalite`, `KM_InfuseSerenite`, `KM_InfuseVerdite`, `KM_InfuseAegite`, `KM_InfuseFerrite` and optional `KM_InfusePyrinth`.
