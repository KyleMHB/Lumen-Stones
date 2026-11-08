using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public static partial class StoneSettings
{
    public static bool PyrinthPresent => ModsConfig.IsActive("det.epochspyrinth") && DefDatabase<ThingDef>.GetNamedSilentFail("DV_Pyrinth") != null;

    // The availability patches run for every recipe, bill and designator in the game, so they read these sets, which are
    // rebuilt only when settings load or the settings window closes.
    private static readonly HashSet<ThingDef> unavailable = new();
    private static readonly HashSet<RecipeDef> unavailableRecipes = new();

    public static bool Available(ThingDef def) => def == null || !unavailable.Contains(def);

    public static bool RecipeAvailable(RecipeDef recipe) => !unavailableRecipes.Contains(recipe);

    public static void RefreshAvailability()
    {
        unavailable.Clear();
        unavailableRecipes.Clear();
        var settings = LumenstoneMod.Settings;
        foreach (var pair in Tunables.Items)
        {
            if (pair.Value.External && !settings.overridePyrinth) continue;
            // Tabs without a switch still honour a hand-written or preset "<Tab>.enabled" key, as before.
            var tab = pair.Value.Tab;
            bool tabOn = tab.Enabled?.On ?? TunableRules.Resolve(Tunables.Overrides, tab.Name + ".enabled", 1) != 0;
            if (tabOn && pair.Value.Enabled.On) continue;
            if (DefDatabase<ThingDef>.GetNamedSilentFail(pair.Key) is { } def) unavailable.Add(def);
        }
        bool pyrinth = StoneTabs.Pyrinth.Enabled.On;
        foreach (var recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
        {
            // Pyrinth's stone is external content, so its toggle alone wouldn't reach this mod's infusion recipe.
            bool available = (recipe.products == null || recipe.products.All(p => Available(p.thingDef))) &&
                (recipe.defName != "KM_InfusePyrinth" || pyrinth);
            if (!available) unavailableRecipes.Add(recipe);
        }
    }

    public static bool DepositAvailable(ThingDef def) => Available(def) && Available(def?.building?.mineableThing);

    // Declares every tunable in one pass, in the order the old registry registered them. That order is the order saved
    // overrides are applied in (where two keys write one target, the later wins), so it stays one global sequence
    // rather than running tab by tab. A tab's rows and sections are the subsequence declared on it.
    internal static void DeclareAll()
    {
        foreach (var tab in new[] { StoneTabs.Firefly, StoneTabs.Glacite, StoneTabs.Vitalite, StoneTabs.Serenite, StoneTabs.Verdite,
                     StoneTabs.Aegite, StoneTabs.Ferrite, StoneTabs.Pyrinth })
            Tunables.Scope(tab);
        Tunables.Scope(StoneTabs.Darklight).Add(StoneTabs.DarklightMode, null);
        Tunables.Scope(StoneTabs.Firefly).Things(false, "KM_LumenFireflyJar", "KM_LumenFireflyColumn");
        Tunables.Scope(StoneTabs.Glacite).Things(false, "KM_Glacite", "KM_LumenGlaciteColumn", "KM_GlaciteLamp",
            "KM_GlaciteWallLamp", "KM_GlacitePylon", "KM_GlaciteCooler", "KM_GlaciteBlade");
        Tunables.Scope(StoneTabs.Vitalite).Things(false, "KM_Vitalite", "KM_LumenVitaliteColumn", "KM_VitaliteLamp",
            "KM_VitaliteWallLamp", "KM_VitalitePylon", "KM_VitaliteCluster", "KM_VitaliteSlabBed", "KM_VitaliteCasket");
        Tunables.Scope(StoneTabs.Serenite).Things(false, "KM_Serenite", "KM_LumenSereniteColumn", "KM_SereniteLamp",
            "KM_SereniteWallLamp", "KM_SerenitePylon", "KM_SereniteCluster", "KM_SerenitePendant");
        Tunables.Scope(StoneTabs.Verdite).Things(false, "KM_Verdite", "KM_LumenVerditeColumn", "KM_VerditeLamp",
            "KM_VerditeWallLamp", "KM_VerditePylon", "KM_VerditeCluster", "KM_VerditeScythe");
        Tunables.Scope(StoneTabs.Aegite).Things(false, "KM_Aegite", "KM_LumenAegiteColumn", "KM_AegiteLamp",
            "KM_AegiteWallLamp", "KM_AegitePylon", "KM_AegiteCluster", "KM_AegiteShield", "KM_AegiteShieldCharm");
        Tunables.Scope(StoneTabs.Ferrite).Things(false, "KM_Ferrite", "KM_LumenFerriteColumn", "KM_FerriteLamp",
            "KM_FerriteWallLamp", "KM_FerriteCluster", "KM_FerritePylon", "KM_FerriteWarhammer");
        var sunstone = Tunables.Scope(StoneTabs.Sunstone);
        sunstone.Things(false, "KM_Sunstone", "KM_SunstoneCollector", "KM_SunstoneArray", "KM_SunstoneVessel", "KM_SunstoneObelisk", "KM_SunstoneAxe",
            "KM_Lunite", "KM_LuniteStabilizer", "KM_AegiteShieldProjector");
        SolarRules.DeclareTunables(sunstone);
        ActiveEffectInterruption.DeclareTunables(sunstone);
        LuniteRules.DeclareTunables(sunstone);
        BastionRules.DeclareTunables(sunstone);
        AegiteShield.DeclareCharmTunables(Tunables.Scope(StoneTabs.Aegite));
        var lumenstone = Tunables.Scope(StoneTabs.Lumenstone);
        lumenstone.Things(false, "KM_LumenstoneColumn", "KM_LumenstoneLamp", "KM_LumenstoneWallLamp",
            "KM_LumenstonePylon", "KM_LumenstoneCluster");
        LumenstoneSupply.DeclareTunables(lumenstone);
        LumenstoneRitual.DeclareTunables(lumenstone);
        // Animastone loads only with Royalty.
        if (ModsConfig.RoyaltyActive && DefDatabase<ThingDef>.GetNamedSilentFail("KM_Animastone") != null)
        {
            var animastone = Tunables.Scope(StoneTabs.Animastone);
            animastone.Things(false, "KM_Animastone", "KM_AnimastoneCirclet", "KM_AnimastoneStaff");
            AnimastoneStaff.DeclareTunables(animastone);
            animastone.Things(false, AnimastoneTunables.Fixtures);
            AnimastoneTunables.DeclareTunables(animastone);
        }
        // Dark content exists only with Ideology. Raw Dark Stone has no off toggle.
        if (ModsConfig.IdeologyActive && DefDatabase<ThingDef>.GetNamedSilentFail("KM_SacrificialAltar") != null)
        {
            var darkStone = Tunables.Scope(StoneTabs.DarkStone);
            darkStone.Things(false, "KM_LumenDarkStoneColumn", "KM_DarkStoneLamp", "KM_DarkStoneWallLamp", "KM_DarkStonePylon",
                "KM_DarkStoneCluster", "KM_BloodstoneVessel", "KM_BloodstoneObelisk");
            AltarRules.DeclareTunables(darkStone);
            WarpRules.DeclareTunables(darkStone);
            AltarRules.DeclareSupportTunables(darkStone);
            DarkSeedRules.DeclareTunables(darkStone);
            var sanguinite = Tunables.Scope(StoneTabs.Sanguinite);
            sanguinite.Things(false, "KM_Sanguinite", "KM_LumenSanguiniteColumn", "KM_SanguiniteLamp", "KM_SanguiniteWallLamp",
                "KM_SanguiniteCluster", "KM_SanguinitePylon", "KM_SanguiniteBlade");
            SanguiniteRules.DeclareTunables(sanguinite);
            var dreadstone = Tunables.Scope(StoneTabs.Dreadstone);
            dreadstone.Things(false, "KM_Dreadstone", "KM_LumenDreadstoneColumn", "KM_DreadstoneLamp", "KM_DreadstoneWallLamp",
                "KM_DreadstoneCluster", "KM_DreadstonePylon", "KM_DreadstoneArbalest");
            DreadstoneRules.DeclareTunables(dreadstone);
            var hollowstone = Tunables.Scope(StoneTabs.Hollowstone);
            hollowstone.Things(false, "KM_Hollowstone", "KM_LumenHollowstoneColumn", "KM_HollowstoneLamp", "KM_HollowstoneWallLamp",
                "KM_HollowstoneCluster", "KM_HollowstonePylon", "KM_HollowstoneGlaive");
            HollowstoneRules.DeclareTunables(hollowstone);
        }
        var vitalite = Tunables.Scope(StoneTabs.Vitalite);
        var serenite = Tunables.Scope(StoneTabs.Serenite);
        var glacite = Tunables.Scope(StoneTabs.Glacite);
        var ferrite = Tunables.Scope(StoneTabs.Ferrite);
        var aegite = Tunables.Scope(StoneTabs.Aegite);
        var verdite = Tunables.Scope(StoneTabs.Verdite);
        VitaliteTunables.DeclareSolarTunables(vitalite);
        SereniteTunables.DeclareSolarTunables(serenite);
        GlaciteSolar.DeclareTunables(glacite);
        FerriteSolar.DeclareTunables(ferrite);
        AegiteSolar.DeclareTunables(aegite);
        SunstoneAxe.DeclareTunables(sunstone);
        FerriteWarhammer.DeclareTunables(ferrite);
        VerditeScythe.DeclareTunables(verdite);
        SerenitePendantRules.DeclareTunables(serenite);
        if (PyrinthPresent)
        {
            var pyrinth = Tunables.Scope(StoneTabs.Pyrinth);
            pyrinth.Things(false, "KM_LumenPyrinthColumn");
            pyrinth.Things(true, "DV_Pyrinth", "DV_MineablePyrinth", "DV_PyrinthLamp", "DV_PyrinthWallLamp",
                "DV_PyrinthBrazier", "DV_PyrinthHeater", "DV_MeleeWeapon_PyrinthBlade");
        }
        VitaliteTunables.DeclareRoomTunables(vitalite);
        SereniteTunables.DeclareRoomTunables(serenite);
        ImplantRules.DeclareTunables(Tunables.Scope(StoneTabs.Implants));
    }

    // The per-def rows: an availability switch, then whatever balance numbers the def's comps, costs, tools and recipes carry.
    // The mod's own comps declare theirs through ITunableProps.
    internal static void DeclareThings(TunableScope s, bool external, string[] names)
    {
        foreach (string name in names)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if (def == null) continue;
            var enabled = new Tunable(name + ".enabled", 1, Limit.Toggle);
            if (!Tunables.AddItem(name, s.Tab, external, enabled))
            {
                Log.Error("Lumen Stones: " + name + " is registered for availability twice.");
                continue;
            }
            string section = def.LabelCap;
            s.In("Availability").Add(enabled, "Allow new " + def.label + (def.building?.mineableThing != null ? " deposits" : ""), external);
            var item = s.In(section);
            if (def.equippedStatOffsets != null)
                foreach (var offset in def.equippedStatOffsets)
                    item.Add(name + ".equipped." + offset.stat.defName, offset.stat.LabelCap + " offset while equipped",
                        offset.value, v => offset.value = v, Limit.Percent(10, -10), external);
            var glow = def.GetCompProperties<CompProperties_Glower>();
            if (glow != null) item.Add(name + ".light", "Light radius", glow.glowRadius, v => glow.glowRadius = v, Limit.Number(max: 100), external);
            var heat = def.GetCompProperties<CompProperties_HeatPusher>();
            if (heat != null)
            {
                bool cooling = heat.heatPerSecond < 0;
                item.Add(name + ".heat", cooling ? "Cooling strength (heat / second)" : "Heating strength (heat / second)",
                    Math.Abs(heat.heatPerSecond), v => heat.heatPerSecond = cooling ? -v : v, external: external);
                item.Add(name + ".cutoff", (heat as IHeatCutoffLabel)?.CutoffLabel ?? "Temperature cutoff (C)",
                    cooling ? heat.heatPushMinTemperature : heat.heatPushMaxTemperature,
                    v => { if (cooling) heat.heatPushMinTemperature = v; else heat.heatPushMaxTemperature = v; }, Limit.Number(-273.15f, 1000), external);
            }
            var comps = s.In(section, external);
            foreach (var comp in def.comps)
                if (comp is ITunableProps own) own.DeclareTunables(comps, def);
            if (def.costList != null)
                foreach (var cost in def.costList)
                    item.Add(name + ".cost." + cost.thingDef.defName, cost.thingDef.LabelCap + " quantity", cost.count,
                        v => cost.count = (int)v, Limit.Integer(), external);
            if (def.costStuffCount > 0)
                item.Add(name + ".stuff", "Construction material quantity", def.costStuffCount,
                    v => def.costStuffCount = (int)v, Limit.Integer(1), external);
            if (def.tools != null)
                for (int i = 0; i < def.tools.Count; i++)
                {
                    var tool = def.tools[i];
                    item.Add(name + ".damage." + i, tool.label + " damage", tool.power, v => tool.power = v, external: external);
                    if (tool.extraMeleeDamages == null) continue;
                    for (int j = 0; j < tool.extraMeleeDamages.Count; j++)
                    {
                        var extra = tool.extraMeleeDamages[j];
                        item.Add(name + ".extraDamage." + i + "." + j, tool.label + " " + extra.def.label + " damage", extra.amount,
                            v => extra.amount = v, external: external);
                    }
                }
            var rock = def.building;
            if (rock?.mineableThing != null)
            {
                var acquisition = s.In("Acquisition");
                acquisition.Add(name + ".sizeMin", "Minimum deposit size", rock.mineableScatterLumpSizeRange.min,
                    v => rock.mineableScatterLumpSizeRange.min = (int)v, Limit.Integer(1), external);
                acquisition.Add(name + ".sizeMax", "Maximum deposit size", rock.mineableScatterLumpSizeRange.max,
                    v => rock.mineableScatterLumpSizeRange.max = (int)v, Limit.Integer(1), external);
                acquisition.Add(name + ".yield", "Mining yield", rock.mineableYield, v => rock.mineableYield = (int)v,
                    Limit.Integer(1), external);
            }
            foreach (var recipe in DefDatabase<RecipeDef>.AllDefsListForReading.Where(r => r.products?.Any(p => p.thingDef == def) == true))
            {
                // Generated recipes have already copied costList during implied-def generation.
                // A cost override must update that recipe too, without replacing modded recipe counts by default.
                if (recipe.defName == "Make_" + name)
                {
                    if (def.costList != null)
                    foreach (var cost in def.costList)
                    {
                        var ingredient = recipe.ingredients.FirstOrDefault(i => i.filter.AllowedDefCount == 1 && i.filter.Allows(cost.thingDef));
                        if (ingredient == null) continue;
                        var entry = Tunables.Find(name + ".cost." + cost.thingDef.defName);
                        if (entry == null) continue;
                        entry.Default = ingredient.GetBaseCount();
                        entry.Apply = v => { cost.count = (int)v; ingredient.SetBaseCount(v); };
                    }
                    var stuff = recipe.ingredients.FirstOrDefault(i => i.filter.AllowedThingDefs.Any(d => d.IsStuff));
                    var stuffEntry = Tunables.Find(name + ".stuff");
                    if (stuff != null && stuffEntry != null)
                    {
                        stuffEntry.Default = stuff.GetBaseCount();
                        stuffEntry.Apply = v => { def.costStuffCount = (int)v; stuff.SetBaseCount(v); };
                    }
                    continue;
                }
                RegisterRecipe(s, section, recipe, external);
            }
        }
    }

    private static void RegisterRecipe(TunableScope s, string section, RecipeDef recipe, bool external)
    {
        for (int i = 0; i < recipe.ingredients.Count; i++)
        {
            var ingredient = recipe.ingredients[i];
            string key = recipe.defName + ".ingredient." + i;
            if (Tunables.Has(key)) continue;
            s.In(section).Add(key, ingredient.filter.Summary + " quantity (recipe)", ingredient.GetBaseCount(),
                v => ingredient.SetBaseCount(v), external: external);
        }
    }
}
