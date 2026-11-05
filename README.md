# Lumen Stones

Lumenstone is a rare mineral that holds a faint light. Infused with a reagent, it takes on that reagent's character: herbal medicine gives Vitalite its mending, and steel gives Ferrite its slow repairs. Glacite needs no reagent and simply runs cold. With Ideology, a sacrifice corrupts Lumenstone into Dark Stone instead.

Lumen Stones for RimWorld 1.7 turns those stones into lamps, columns, pylons, furnishings, weapons, implants and crystal prosthetics that need no fuel or electricity:

- **Glacite** cools rooms and keeps a 4 C fridge without power. **Vitalite** supports hospitals. **Serenite** improves rest and mood. **Verdite** helps farming. **Aegite** thins filth and suppresses fires. **Ferrite** slowly repairs buildings and gear.
- **Sunstone** gathers daylight as Solar energy, which powers each stone's stronger active effect.
- With Royalty, **Animastone** improves meditation.
- With Ideology, sacrifice yields Dark Stone and Warp energy. **Bloodstone** stores the energy, and **Sanguinite**, **Dreadstone** and **Hollowstone** spend it on regrowth, subjugation and endurance.
- Firefly columns add animated green light, and [Epochs - Pyrinth](https://steamcommunity.com/sharedfiles/filedetails/?id=3336544632) adds optional warm orange columns.

## Balance

Every stone has a price and a ceiling:

- **Scarce supply.** Lumenstone veins and surface finds are rare. Traders charge about seven times its value and never buy it back. The Ideology ritual is the only steady source, once every five days per settlement.
- **Earned stones.** Each stone except Glacite needs its own research and a reagent.
- **Sunstone unlocks the major benefits.** Passive effects are rewarding but not overpowered. Each family's full potential needs Solar energy from Sunstone, which needs open sky, drops in bad weather and stops at night.
- **No stacking.** Room bonuses are capped and each fixture category counts once. Wards and farm fixtures use only the strongest source, and Ferrite fixtures share one repair budget.
- **Trade-offs.** Every implant has a drawback. The dark stones cost lives, carry a mood penalty and spend Warp energy as they work.
- **Vanilla-level gear.** Weapons and equipment match comparable vanilla items in damage, armor and stats, and add utility such as a stun, a bleed or self-repair.
- **Configurable.** Costs, caps, energy use and supply can be changed in Mod options. See [stone settings](docs/stone-settings.md) for every value.

## Installation

1. Install and enable [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077).
2. Copy the built mod into `RimWorld/Mods/Lumen-Stones`. `About`, `Defs`, `Textures`, `Assemblies` and `LoadFolders.xml` must be directly inside that folder.
3. Enable Lumen Stones after Harmony, then restart RimWorld.
4. Optionally enable [Epochs - Pyrinth](https://steamcommunity.com/sharedfiles/filedetails/?id=3336544632) before Lumen Stones for pyrinth columns. Neither Pyrinth nor DLC is required for the core mod.

Every stone starts as Lumenstone. Mine it from rare veins, find it on the surface or buy it from traders at a high price. With Ideology, the Lumenstone ritual turns stone chunks into more. Research Stonecutting, build a Lumen ritual circle and infuse 10 Lumenstone with a simple reagent into 10 of any researched stone.

Open Options > Mod options > Lumen Stones to configure stone and item availability, effects, costs and Lumenstone supply. Balance changes require a restart. Disabling content preserves existing items, buildings and blueprints. Untouched balance values follow the active mod list. See [stone settings](docs/stone-settings.md) for controls, resets and AutoConfig keys.

The optional Auto Mod Config preset is in `Extras/Lumen-Stones-AutoConfig`. Copy it into `RimWorld/Mods` as a separate mod folder. It requires Auto Mod Config Continued and Lumen Stones. Enabling the example preset replaces saved Lumen Stones settings, enables content and leaves balance to the active mod list. The main mod works without it.

## Building from source

Install the .NET 9 SDK or later, then build the assembly:

```powershell
dotnet build Source/LumenStones.csproj -c Release
```

The project writes `Assemblies/LumenStones.dll`. Its default game and Harmony paths match the local installation. Override them when needed:

```powershell
dotnet build Source/LumenStones.csproj -c Release -p:RimWorldPath="path to RimWorld" -p:HarmonyPath="path to 0Harmony.dll"
```

Copy the built assembly with the mod's runtime folders when installing from a source checkout.

## Contributing

Contributions, issues, and feature requests are welcome.

## License

Released under the **GNU General Public License v3.0**. See [`LICENSE`](LICENSE) for details.

Commercial use is allowed under GPLv3. Distributed modified versions must remain under GPLv3 and make corresponding source available as required by the license.

Glacite's definitions adapted from Epochs - Pyrinth retain Creative Commons Attribution 4.0 International; see [Licenses/Glacite-CC-BY-4.0.txt](Licenses/Glacite-CC-BY-4.0.txt). Icon artwork derived from Bionic Icons includes its MIT notice in [Licenses/BionicIcons-MIT.txt](Licenses/BionicIcons-MIT.txt). See [CREDITS.md](CREDITS.md) for attribution.

## Links

Support me on Ko-fi. This does not imply endorsement by the original authors.

[![Support me on Ko-fi](https://img.shields.io/badge/Support_me_on_Ko--fi-72a4f2?style=for-the-badge&logo=kofi&logoColor=white)](https://ko-fi.com/I7L525WMJ6)
