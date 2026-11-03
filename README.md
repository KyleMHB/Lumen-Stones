# Lumen Stones

Lumen Stones combines Glacite and Lumenstone Columns for RimWorld 1.7, and adds Vitalite, Serenite, Verdite, Aegite, Ferrite and Sunstone furnishings and equipment. Each infused stone has a passive effect and an active effect powered by Solar energy from Sunstone collectors and batteries.

## Installation

1. Install and enable [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077).
2. Copy the built mod into `RimWorld/Mods/Lumen-Stones`. `About`, `Defs`, `Textures`, `Assemblies` and `LoadFolders.xml` must be directly inside that folder.
3. Disable the separate Glacite and Lumenstone Columns mods. Enable Lumen Stones after Harmony, then restart RimWorld.
4. Optionally enable [Epochs - Pyrinth](https://steamcommunity.com/sharedfiles/filedetails/?id=3336544632) before Lumen Stones for pyrinth columns. Neither Pyrinth nor DLC is required for the core mod.

Every stone starts as Lumenstone. Mine it from rare veins, find it on the surface or buy it from traders at a high price. With Ideology, the Lumenstone ritual turns stone chunks into more. Research Stonecutting, build a Lumen ritual circle and infuse 10 Lumenstone with a simple reagent into 10 of any researched stone.

Version 2.0 does not work with existing saves. Start a new colony.

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

Glacite's adapted content retains Epochs - Pyrinth's Creative Commons Attribution 4.0 International license. See [LICENSE](LICENSE) and [CREDITS.md](CREDITS.md) for terms and attribution.

## Links

Support me on Ko-fi. This does not imply endorsement by the original authors.

[![Support me on Ko-fi](https://img.shields.io/badge/Support_me_on_Ko--fi-72a4f2?style=for-the-badge&logo=kofi&logoColor=white)](https://ko-fi.com/I7L525WMJ6)
