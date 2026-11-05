using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public sealed class StoneSetting
{
    public string Key, Group, Section, Label, Unit = "";
    public float Default, Min, Max = 100000, DisplayScale = 1;
    public bool Integer, Toggle, External;
    public Action<float> Apply;
    public float Clamp(float value) => Integer ? Mathf.Round(Mathf.Clamp(value, Min, Max)) : Mathf.Clamp(value, Min, Max);
}

public static partial class StoneSettings
{
    public static readonly List<StoneSetting> Entries = new();
    private static readonly Dictionary<string, (string Group, bool External)> items = new();
    public static bool PyrinthPresent => ModsConfig.IsActive("det.epochspyrinth") && DefDatabase<ThingDef>.GetNamedSilentFail("DV_Pyrinth") != null;
    public static float RoomHealingCap = VitaliteSupport.MaxRoomHealingBonus;
    public static float RoomImmunityCap = VitaliteSupport.MaxRoomImmunityBonus;
    public static float RoomInfectionPreventionCap = VitaliteSupport.MaxRoomInfectionPrevention;

    private static void Add(string group, string section, string key, string label, float value, Action<float> apply = null,
        float min = 0, float max = 100000, bool integer = false, bool toggle = false, bool external = false, bool percent = false)
    {
        Entries.Add(new StoneSetting { Key = key, Group = group, Section = section, Label = label, Default = value,
            Apply = apply, Min = min, Max = max, Integer = integer, Toggle = toggle, External = external,
            DisplayScale = percent ? 100 : 1, Unit = percent ? "%" : "" });
    }

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
        foreach (var pair in items)
        {
            if (pair.Value.External && !settings.overridePyrinth) continue;
            if (settings.Value(pair.Value.Group + ".enabled", 1) != 0 && settings.Value(pair.Key + ".enabled", 1) != 0) continue;
            if (DefDatabase<ThingDef>.GetNamedSilentFail(pair.Key) is { } def) unavailable.Add(def);
        }
        bool pyrinth = settings.Value("Pyrinth.enabled", 1) != 0;
        foreach (var recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
        {
            // Pyrinth's stone is external content, so its toggle alone wouldn't reach this mod's infusion recipe.
            bool available = (recipe.products == null || recipe.products.All(p => Available(p.thingDef))) &&
                (recipe.defName != "KM_InfusePyrinth" || pyrinth);
            if (!available) unavailableRecipes.Add(recipe);
        }
    }

    public static bool DepositAvailable(ThingDef def) => Available(def) && Available(def?.building?.mineableThing);

    public static void Initialize()
    {
        Entries.Clear();
        items.Clear();
        foreach (string group in new[] { "Firefly", "Glacite", "Vitalite", "Serenite", "Verdite", "Aegite", "Ferrite", "Pyrinth" })
            Add(group, "Availability", group + ".enabled", "Allow new " + group + " content", 1, toggle: true);
        Register("Firefly", false, "KM_LumenFireflyJar", "KM_LumenFireflyColumn");
        Register("Glacite", false, "KM_Glacite", "KM_LumenGlaciteColumn", "KM_GlaciteLamp",
            "KM_GlaciteWallLamp", "KM_GlacitePylon", "KM_GlaciteCooler", "KM_GlaciteBlade");
        Register("Vitalite", false, "KM_Vitalite", "KM_LumenVitaliteColumn", "KM_VitaliteLamp",
            "KM_VitaliteWallLamp", "KM_VitalitePylon", "KM_VitaliteCluster", "KM_VitaliteSlabBed", "KM_VitaliteCasket");
        Register("Serenite", false, "KM_Serenite", "KM_LumenSereniteColumn", "KM_SereniteLamp",
            "KM_SereniteWallLamp", "KM_SerenitePylon", "KM_SereniteCluster", "KM_SerenitePendant");
        Register("Verdite", false, "KM_Verdite", "KM_LumenVerditeColumn", "KM_VerditeLamp",
            "KM_VerditeWallLamp", "KM_VerditePylon", "KM_VerditeCluster", "KM_VerditeScythe");
        Register("Aegite", false, "KM_Aegite", "KM_LumenAegiteColumn", "KM_AegiteLamp",
            "KM_AegiteWallLamp", "KM_AegitePylon", "KM_AegiteCluster", "KM_AegiteShield", "KM_AegiteShieldCharm");
        Register("Ferrite", false, "KM_Ferrite", "KM_LumenFerriteColumn", "KM_FerriteLamp",
            "KM_FerriteWallLamp", "KM_FerriteCluster", "KM_FerritePylon", "KM_FerriteWarhammer");
        Add("Sunstone", "Availability", "Sunstone.enabled", "Allow new Sunstone content, and require Solar energy for active effects", 1, toggle: true);
        Register("Sunstone", false, "KM_Sunstone", "KM_SunstoneCollector", "KM_SunstoneArray", "KM_SunstoneVessel", "KM_SunstoneObelisk", "KM_SunstoneAxe",
            "KM_Lunite", "KM_LuniteStabilizer", "KM_AegiteShieldProjector");
        AddSunstone();
        AddBastion();
        Register("Lumenstone", false, "KM_LumenstoneColumn", "KM_LumenstoneLamp", "KM_LumenstoneWallLamp",
            "KM_LumenstonePylon", "KM_LumenstoneCluster");
        var repairLumenstoneRanges = AddLumenstone();
        // Animastone loads only with Royalty.
        if (ModsConfig.RoyaltyActive && DefDatabase<ThingDef>.GetNamedSilentFail("KM_Animastone") != null)
        {
            Add("Animastone", "Availability", "Animastone.enabled", "Allow new Animastone content", 1, toggle: true);
            string[] fixtures = { "KM_LumenAnimastoneColumn", "KM_AnimastoneLamp", "KM_AnimastoneWallLamp", "KM_AnimastoneCluster", "KM_AnimastonePylon" };
            Register("Animastone", false, "KM_Animastone", "KM_AnimastoneCirclet", "KM_AnimastoneStaff");
            const string staff = "Animastone staff";
            Add("Animastone", staff, "KM_AnimastoneStaff.stunChance", "Stun chance at 100% psychic sensitivity (%)", AnimastoneStaff.StunChance,
                v => AnimastoneStaff.StunChance = v, max: 1, percent: true);
            Add("Animastone", staff, "KM_AnimastoneStaff.stunTicks", "Stun duration (ticks)", AnimastoneStaff.StunTicks,
                v => AnimastoneStaff.StunTicks = (int)v, min: 1, max: 60000, integer: true);
            Register("Animastone", false, fixtures);
            var focus = DefDatabase<StatDef>.GetNamed("MeditationFocusStrength");
            foreach (string name in fixtures)
            {
                var def = DefDatabase<ThingDef>.GetNamed(name);
                Add("Animastone", def.LabelCap, name + ".focus", "Meditation focus strength (%)", def.GetStatValueAbstract(focus),
                    v => def.SetStatBaseValue(focus, v), max: 1, percent: true);
            }
            Add("Animastone", "Room support", "Animastone.heatRecoveryCap", "Neural heat recovery cap while meditating (%)", AnimastoneSupport.HeatRecoveryCap,
                v => AnimastoneSupport.HeatRecoveryCap = v, max: 10, percent: true);
            Add("Animastone", "Room support", "Animastone.focusGainCap", "Psyfocus gain cap while meditating (%)", AnimastoneSupport.FocusGainCap,
                v => AnimastoneSupport.FocusGainCap = v, max: 10, percent: true);
        }
        if (ModsConfig.IdeologyActive && DefDatabase<ThingDef>.GetNamedSilentFail("KM_SacrificialAltar") != null)
        {
            AddDarkStone();
            AddSanguinite();
            AddDreadstone();
            AddHollowstone();
        }
        Add("Vitalite", "Solar energy", "Vitalite.patientSolarDrain", "Solar energy per day for each injured patient treated", VitaliteSupport.PatientSolarDrain,
            v => VitaliteSupport.PatientSolarDrain = v);
        Add("Serenite", "Solar energy", "Serenite.sleeperSolarDrain", "Solar energy per day for each supported sleeper", SereniteSupport.SleeperSolarDrain,
            v => SereniteSupport.SleeperSolarDrain = v);
        Add("Glacite", "Solar energy", "Glacite.freezerSolarDrain", "Solar energy per day while cooling between the two limits", GlaciteSolar.FreezerDrain,
            v => GlaciteSolar.FreezerDrain = v);
        Add("Ferrite", "Solar energy", "Ferrite.solarCostPerHp", "Solar energy per HP of item or gear repaired", FerriteSolar.CostPerHp,
            v => FerriteSolar.CostPerHp = v);
        Add("Aegite", "Solar energy", "Aegite.solarCostPerFire", "Solar energy per fire suppressed", AegiteSolar.CostPerFire,
            v => AegiteSolar.CostPerFire = v);
        const string axe = "Sunstone axe";
        Add("Sunstone", axe, "KM_SunstoneAxe.baneMultiplier", "Melee damage multiplier against Anomaly entities and mutants", SunstoneAxe.UnnaturalDamageMultiplier,
            v => SunstoneAxe.UnnaturalDamageMultiplier = v, min: 1, max: 10);
        Add("Sunstone", axe, "KM_SunstoneAxe.procChance", "Blind and stun chance on damaging melee hits (%)", SunstoneAxe.ProcChance,
            v => SunstoneAxe.ProcChance = v, max: 1, percent: true);
        Add("Sunstone", axe, "KM_SunstoneAxe.procChanceUnnatural", "Blind and stun chance against Anomaly entities and mutants (%)", SunstoneAxe.UnnaturalProcChance,
            v => SunstoneAxe.UnnaturalProcChance = v, max: 1, percent: true);
        Add("Sunstone", axe, "KM_SunstoneAxe.blindTicks", "Solar blindness duration (ticks)", SunstoneAxe.BlindTicks,
            v => SunstoneAxe.BlindTicks = (int)v, min: 1, max: 60000, integer: true);
        Add("Sunstone", axe, "KM_SunstoneAxe.stunTicks", "Flash stun duration (ticks)", SunstoneAxe.StunTicks,
            v => SunstoneAxe.StunTicks = (int)v, min: 1, max: 60000, integer: true);
        Add("Ferrite", "Ferrite warhammer", "KM_FerriteWarhammer.stunChance", "Stun chance on damaging melee hits (%)", FerriteWarhammer.StunChance,
            v => FerriteWarhammer.StunChance = v, max: 1, percent: true);
        Add("Ferrite", "Ferrite warhammer", "KM_FerriteWarhammer.stunTicks", "Stun duration (ticks)", FerriteWarhammer.StunTicks,
            v => FerriteWarhammer.StunTicks = (int)v, min: 1, max: 60000, integer: true);
        var hammerMending = FerriteWarhammer.Def.GetCompProperties<CompProperties_Ferrite>();
        Add("Ferrite", "Ferrite warhammer", "KM_FerriteWarhammer.repairBudget", "Condition mended per pulse (% of maximum hit points)", hammerMending.repairBudget,
            v => hammerMending.repairBudget = v, max: 1, percent: true);
        Add("Verdite", "Verdite scythe", "KM_VerditeScythe.farming", "Plant work speed and harvest yield bonus (%)", VerditeScythe.FarmingBonus,
            v => VerditeScythe.FarmingBonus = v, max: 10, percent: true);
        Add("Verdite", "Verdite scythe", "KM_VerditeScythe.lifeSteal", "Actual melee damage converted to healing (%)", VerditeScythe.LifeStealFraction,
            v => VerditeScythe.LifeStealFraction = v, max: 1, percent: true);
        var pendantMood = DefDatabase<ThoughtDef>.GetNamed("KM_SerenitePendantWorn").stages[0];
        Add("Serenite", "Serenite pendant", "KM_SerenitePendant.mood", "Mood while worn", pendantMood.baseMoodEffect,
            v => pendantMood.baseMoodEffect = v, min: -100, max: 100);
        if (PyrinthPresent)
        {
            Register("Pyrinth", false, "KM_LumenPyrinthColumn");
            Register("Pyrinth", true, "DV_Pyrinth", "DV_MineablePyrinth", "DV_PyrinthLamp", "DV_PyrinthWallLamp",
                "DV_PyrinthBrazier", "DV_PyrinthHeater", "DV_MeleeWeapon_PyrinthBlade");
        }
        Add("Vitalite", "Room support", "Vitalite.healingCap", "Room healing cap (%)", RoomHealingCap,
            v => RoomHealingCap = v, max: 10, percent: true);
        Add("Vitalite", "Room support", "Vitalite.immunityCap", "Room immunity cap (%)", RoomImmunityCap,
            v => RoomImmunityCap = v, max: 10, percent: true);
        Add("Vitalite", "Room support", "Vitalite.infectionCap", "Room infection prevention cap (%)", RoomInfectionPreventionCap,
            v => RoomInfectionPreventionCap = v, max: 1, percent: true);
        Add("Serenite", "Room support", "Serenite.sleepCap", "Sleep restoration cap (%)", SereniteSupport.SleepCap,
            v => SereniteSupport.SleepCap = v, max: 10, percent: true);
        Add("Serenite", "Room support", "Serenite.comfortCap", "Bed/seat Comfort cap", SereniteSupport.ComfortCap,
            v => SereniteSupport.ComfortCap = v, max: 10);
        Add("Serenite", "Room support", "Serenite.moodCap", "Room Mood cap", SereniteSupport.MoodCap,
            v => SereniteSupport.MoodCap = v, max: 100);
        AddImplants();
        foreach (var entry in Entries)
            if (entry.Apply != null && (!entry.External || LumenstoneMod.Settings.overridePyrinth) &&
                LumenstoneMod.Settings.overrides.ContainsKey(entry.Key))
                entry.Apply(entry.Clamp(LumenstoneMod.Settings.Value(entry.Key, entry.Default)));
        repairLumenstoneRanges();
        foreach (var def in items.Keys.Select(name => DefDatabase<ThingDef>.GetNamed(name)))
        {
            if (def.building != null && def.building.mineableScatterLumpSizeRange.max < def.building.mineableScatterLumpSizeRange.min)
                def.building.mineableScatterLumpSizeRange.max = def.building.mineableScatterLumpSizeRange.min;
        }
        RefreshAvailability();
    }

    private static void AddSunstone()
    {
        const string group = "Sunstone", section = "Solar network";
        foreach (var (key, label, name) in new[] {
            ("collectorOutput", "Collector output per day at full sun", "KM_SunstoneCollector"),
            ("arrayOutput", "Array output per day at full sun", "KM_SunstoneArray") })
        {
            var comp = DefDatabase<ThingDef>.GetNamed(name).GetCompProperties<CompProperties_Sunstone>();
            Add(group, section, "Sunstone." + key, label, comp.output, v => comp.output = v, min: 0.01f);
        }
        foreach (var (key, label, name) in new[] {
            ("vesselCapacity", "Vessel capacity", "KM_SunstoneVessel"),
            ("obeliskCapacity", "Obelisk capacity", "KM_SunstoneObelisk") })
        {
            var comp = DefDatabase<ThingDef>.GetNamed(name).GetCompProperties<CompProperties_Sunstone>();
            Add(group, section, "Sunstone." + key, label, comp.capacity, v => comp.capacity = v, min: 1);
        }
        Add(group, section, "Sunstone.solarHysteresis", "Stored energy needed to recover from depletion", SolarRules.Hysteresis,
            v => SolarRules.Hysteresis = v, min: 0.02f);
        string[] weather = { "Fog", "Rain and gentle snow", "Thunderstorms", "Foggy rain and hard snow" };
        for (int i = 0; i < weather.Length; i++)
        {
            int tier = i;
            Add(group, section, "Sunstone.weather" + tier, weather[i] + " output factor (%)", SolarRules.WeatherFactors[tier],
                v => SolarRules.WeatherFactors[tier] = v, max: 1, percent: true);
        }
    }

    // Dark content exists only with Ideology. Raw Dark Stone has no off toggle.
    private static void AddDarkStone()
    {
        const string group = "DarkStone";
        Register(group, false, "KM_LumenDarkStoneColumn", "KM_DarkStoneLamp", "KM_DarkStoneWallLamp", "KM_DarkStonePylon",
            "KM_DarkStoneCluster", "KM_BloodstoneVessel", "KM_BloodstoneObelisk");
        const string crystallize = "Crystallize Dark Stone", harvest = "Harvest Warp Energy", link = "Link and victims";
        Add(group, crystallize, "DarkStone.crystallizeInput", "Lumenstone input", AltarRules.CrystallizeInput,
            v => AltarRules.CrystallizeInput = (int)v, min: 1, integer: true);
        Add(group, crystallize, "DarkStone.crystallizePoor", "Poor outcome conversion share (%)", AltarRules.CrystallizePoor,
            v => AltarRules.CrystallizePoor = v, max: 1, percent: true);
        Add(group, crystallize, "DarkStone.crystallizeSuccess", "Successful outcome conversion share (%)", AltarRules.CrystallizeSuccess,
            v => AltarRules.CrystallizeSuccess = v, max: 1, percent: true);
        Add(group, crystallize, "DarkStone.crystallizeExceptional", "Exceptional outcome conversion share (%)", AltarRules.CrystallizeExceptional,
            v => AltarRules.CrystallizeExceptional = v, max: 1, percent: true);
        Add(group, crystallize, "DarkStone.crystallizeCooldown", "Cooldown (days)", AltarRules.CrystallizeCooldownDays,
            v => AltarRules.CrystallizeCooldownDays = v);
        Add(group, harvest, "DarkStone.harvestPoor", "Poor outcome Warp energy", AltarRules.HarvestPoor, v => AltarRules.HarvestPoor = v);
        Add(group, harvest, "DarkStone.harvestSuccess", "Successful outcome Warp energy", AltarRules.HarvestSuccess, v => AltarRules.HarvestSuccess = v);
        Add(group, harvest, "DarkStone.harvestExceptional", "Exceptional outcome Warp energy", AltarRules.HarvestExceptional,
            v => AltarRules.HarvestExceptional = v);
        Add(group, harvest, "DarkStone.harvestCooldown", "Cooldown (days)", AltarRules.HarvestCooldownDays, v => AltarRules.HarvestCooldownDays = v);
        Add(group, link, "DarkStone.linkRadius", "Altar to battery link radius (tiles)", AltarRules.LinkRadius,
            v => AltarRules.LinkRadius = v, min: 1, max: 100);
        Add(group, link, "DarkStone.statusSlave", "Slave victim factor multiplier", AltarRules.StatusSlave, v => AltarRules.StatusSlave = v, max: 10);
        Add(group, link, "DarkStone.statusColonist", "Colonist victim factor multiplier", AltarRules.StatusColonist, v => AltarRules.StatusColonist = v, max: 10);

        Add(group, "Warp network", "DarkStone.warpHysteresis", "Stored Warp energy needed to recover from depletion", WarpRules.Hysteresis,
            v => WarpRules.Hysteresis = v, min: 0.02f);

        Add(group, "Ritual support", "DarkStone.ritualQualityCap", "Fixture support quality bonus at full support (%)", DarkStoneSupport.RitualQualityCap,
            v => DarkStoneSupport.RitualQualityCap = v, max: 10, percent: true);
        Add(group, "Ritual support", "DarkStone.ritualYieldCap", "Fixture support conversion and harvest bonus at full support (%)", DarkStoneSupport.RitualYieldCap,
            v => DarkStoneSupport.RitualYieldCap = v, max: 10, percent: true);
        Add(group, "Ritual support", "DarkStone.ritualSupportRadius", "Fixture support radius (tiles)", DarkStoneSupport.RitualSupportRadius,
            v => DarkStoneSupport.RitualSupportRadius = v, min: 1, max: 50);

        var altar = DefDatabase<ThingDef>.GetNamed("KM_SacrificialAltar");
        var altarCost = altar.costList.First(c => c.thingDef.defName == "KM_Lumenstone");
        Add(group, "Sacrificial altar", "DarkStone.altarCost", "Lumenstone quantity", altarCost.count, v => altarCost.count = (int)v, integer: true);
        Add(group, "Sacrificial altar", "DarkStone.altarStuff", "Stone block quantity", altar.costStuffCount,
            v => altar.costStuffCount = (int)v, min: 1, integer: true);

        var seedCrystal = DefDatabase<RecipeDef>.GetNamedSilentFail("KM_CraftDreadstoneSeedCrystal");
        if (seedCrystal == null) return;
        // The crystal is made from Dreadstone, so it follows the Dreadstone toggle.
        items.TryAdd("KM_DreadstoneSeedCrystal", ("Dreadstone", false));
        const string seed = "Dreadstone seed";
        Add(group, seed, "DarkSeed.crystalCost", "Dreadstone per seed crystal", seedCrystal.ingredients[0].GetBaseCount(), v =>
        {
            seedCrystal.ingredients[0].SetBaseCount(v);
            seedCrystal.description = "Shape " + v + " Dreadstone into a dreadstone seed crystal.";
        }, min: 1, integer: true);
        Add(group, seed, "DarkSeed.days", "Days to full growth", DarkSeedRules.Days, v => DarkSeedRules.Days = v, min: 0.1f);
        Add(group, seed, "DarkSeed.maxPain", "Maximum seed pain (%)", DarkSeedRules.MaxPain, v => DarkSeedRules.MaxPain = v, max: 1, percent: true);
        Add(group, seed, "DarkSeed.cutIntervalStart", "Mean hours between cuts when bound", DarkSeedRules.CutIntervalStart,
            v => DarkSeedRules.CutIntervalStart = v, min: 0.1f);
        Add(group, seed, "DarkSeed.cutIntervalEnd", "Mean hours between cuts in the last stage", DarkSeedRules.CutIntervalEnd,
            v => DarkSeedRules.CutIntervalEnd = v, min: 0.1f);
        Add(group, seed, "DarkSeed.cutSeverity", "Cut damage multiplier", DarkSeedRules.CutSeverity, v => DarkSeedRules.CutSeverity = v, max: 10);
        Add(group, seed, "DarkSeed.baseYield", "Dark Stone at full growth, before the victim factor", DarkSeedRules.BaseYield,
            v => DarkSeedRules.BaseYield = v, min: 1);
    }

    private static void AddSanguinite()
    {
        const string group = "Sanguinite";
        Add(group, "Availability", "Sanguinite.enabled", "Allow new Sanguinite content", 1, toggle: true);
        Register(group, false, "KM_Sanguinite", "KM_LumenSanguiniteColumn", "KM_SanguiniteLamp", "KM_SanguiniteWallLamp",
            "KM_SanguiniteCluster", "KM_SanguinitePylon", "KM_SanguiniteBlade");
        const string reach = "Treatment", costs = "Warp energy costs", times = "Regrowth time";
        Add(group, reach, "Sanguinite.radius", "Treatment radius (tiles)", SanguiniteRules.Radius, v => SanguiniteRules.Radius = v, min: 1, max: 100);
        Add(group, costs, "Sanguinite.bleedCost", "Slowed bleeding, per bleeding patient per pass", SanguiniteRules.BleedCost, v => SanguiniteRules.BleedCost = v);
        Add(group, costs, "Sanguinite.scarCost", "Scar", SanguiniteRules.ScarCost, v => SanguiniteRules.ScarCost = v);
        Add(group, costs, "Sanguinite.digitCost", "Digit", SanguiniteRules.DigitCost, v => SanguiniteRules.DigitCost = v, min: 0.01f);
        Add(group, costs, "Sanguinite.smallPartCost", "Small part (eye, ear, nose, jaw)", SanguiniteRules.SmallPartCost,
            v => SanguiniteRules.SmallPartCost = v, min: 0.01f);
        Add(group, costs, "Sanguinite.organCost", "Internal organ", SanguiniteRules.OrganCost, v => SanguiniteRules.OrganCost = v, min: 0.01f);
        Add(group, times, "Sanguinite.digitDays", "Digit (days)", SanguiniteRules.DigitDays, v => SanguiniteRules.DigitDays = v, min: 0.1f);
        Add(group, times, "Sanguinite.smallPartDays", "Small part (days)", SanguiniteRules.SmallPartDays, v => SanguiniteRules.SmallPartDays = v, min: 0.1f);
        Add(group, "Flensing Blade", "Sanguinite.hemorrhageMaxStacks", "Maximum Hemorrhage stacks", SanguiniteBlade.MaxStacks,
            v => SanguiniteBlade.MaxStacks = (int)v, min: 1, max: 10, integer: true);
        Add(group, times, "Sanguinite.organDays", "Internal organ (days)", SanguiniteRules.OrganDays, v => SanguiniteRules.OrganDays = v, min: 0.1f);
    }

    private static void AddDreadstone()
    {
        const string group = "Dreadstone";
        Add(group, "Availability", "Dreadstone.enabled", "Allow new Dreadstone content", 1, toggle: true);
        Register(group, false, "KM_Dreadstone", "KM_LumenDreadstoneColumn", "KM_DreadstoneLamp", "KM_DreadstoneWallLamp",
            "KM_DreadstoneCluster", "KM_DreadstonePylon", "KM_DreadstoneArbalest");
        const string reach = "Reach and cost", subjugation = "Subjugation", dread = "Dread and Death Siphon";
        Add(group, reach, "Dreadstone.radius", "Fixture radius (tiles)", DreadstoneRules.Radius, v => DreadstoneRules.Radius = v, min: 1, max: 100);
        Add(group, reach, "Dreadstone.subjugationCost", "Warp energy per covered prisoner or slave per day", DreadstoneRules.SubjugationCost,
            v => DreadstoneRules.SubjugationCost = v);
        Add(group, subjugation, "Dreadstone.resistancePerDay", "Resistance lost per day (column)", DreadstoneRules.ResistancePerDay,
            v => DreadstoneRules.ResistancePerDay = v);
        Add(group, subjugation, "Dreadstone.willPerDay", "Will lost per day (column)", DreadstoneRules.WillPerDay, v => DreadstoneRules.WillPerDay = v);
        Add(group, subjugation, "Dreadstone.suppressionPerDay", "Slave suppression gained per day (column, %)", DreadstoneRules.SuppressionPerDay,
            v => DreadstoneRules.SuppressionPerDay = v, max: 1, percent: true);
        Add(group, subjugation, "Dreadstone.pylonErosionFactor", "Pylon erosion multiplier", DreadstoneRules.PylonErosionFactor,
            v => DreadstoneRules.PylonErosionFactor = v, min: 1, max: 10);
        Add(group, subjugation, "Dreadstone.breakMtbFactor", "Prison break and rebellion rarity multiplier", DreadstoneRules.BreakMtbFactor,
            v => DreadstoneRules.BreakMtbFactor = v, min: 1, max: 100);
        Add(group, dread, "Dreadstone.dreadCostPerPass", "Warp energy per hostile per pass", DreadstoneRules.DreadCostPerPass,
            v => DreadstoneRules.DreadCostPerPass = v);
        Add(group, dread, "Dreadstone.siphonFraction", "Market value returned as Warp energy on a marked death (%)", DreadstoneRules.SiphonFraction,
            v => DreadstoneRules.SiphonFraction = v, max: 1, percent: true);
        Add(group, dread, "Dreadstone.arbalestDreadSeconds", "Arbalest Dread duration (seconds)", DreadstoneRules.ArbalestDreadSeconds,
            v => DreadstoneRules.ArbalestDreadSeconds = v, min: 1, max: 600);
    }

    private static void AddHollowstone()
    {
        const string group = "Hollowstone";
        Add(group, "Availability", "Hollowstone.enabled", "Allow new Hollowstone content", 1, toggle: true);
        Register(group, false, "KM_Hollowstone", "KM_LumenHollowstoneColumn", "KM_HollowstoneLamp", "KM_HollowstoneWallLamp",
            "KM_HollowstoneCluster", "KM_HollowstonePylon", "KM_HollowstoneGlaive");
        const string ward = "Pain suppression";
        Add(group, ward, "Hollowstone.radius", "Fixture radius (tiles)", HollowstoneRules.Radius, v => HollowstoneRules.Radius = v, min: 1, max: 100);
        const string husk = "Work husks";
        Add(group, husk, "Hollowstone.raiseCost", "Warp energy to raise a husk", HuskRules.RaiseCost, v => HuskRules.RaiseCost = v);
        Add(group, husk, "Hollowstone.huskCostPerDay", "Warp energy per husk per day", HuskRules.HuskCostPerDay, v => HuskRules.HuskCostPerDay = v);
        Add(group, husk, "Hollowstone.raiseCooldown", "Raise cooldown (days)", HuskRules.RaiseCooldownDays, v => HuskRules.RaiseCooldownDays = v);
        Add(group, ward, "Hollowstone.wardCostPerDay", "Warp energy per covered pawn per day", HollowstoneRules.WardCostPerDay,
            v => HollowstoneRules.WardCostPerDay = v);
    }

    // Returns the range repair to run once saved overrides have been applied.
    private static Action AddLumenstone()
    {
        const string group = "Lumenstone";
        var stone = DefDatabase<ThingDef>.GetNamed("KM_Lumenstone");
        var rock = DefDatabase<ThingDef>.GetNamed("KM_MineableLumenstone").building;
        Add(group, "Veins", "Lumenstone.veinFrequency", "Vein frequency", rock.mineableScatterCommonality,
            v => rock.mineableScatterCommonality = v);
        Add(group, "Veins", "Lumenstone.veinSizeMin", "Minimum vein size", rock.mineableScatterLumpSizeRange.min,
            v => rock.mineableScatterLumpSizeRange.min = (int)v, min: 1, integer: true);
        Add(group, "Veins", "Lumenstone.veinSizeMax", "Maximum vein size", rock.mineableScatterLumpSizeRange.max,
            v => rock.mineableScatterLumpSizeRange.max = (int)v, min: 1, integer: true);
        Add(group, "Veins", "Lumenstone.veinYield", "Mining yield per cell", rock.mineableYield,
            v => rock.mineableYield = (int)v, min: 1, integer: true);

        var surface = (GenStep_LumenstoneSurface)DefDatabase<GenStepDef>.GetNamed("KM_LumenstoneSurface").genStep;
        Add(group, "Surface", "Lumenstone.surfaceChance", "Chance per map (%)", surface.chance, v => surface.chance = v, max: 1, percent: true);
        Add(group, "Surface", "Lumenstone.surfaceStacksMin", "Minimum stacks", surface.stacks.min, v => surface.stacks.min = (int)v, integer: true);
        Add(group, "Surface", "Lumenstone.surfaceStacksMax", "Maximum stacks", surface.stacks.max, v => surface.stacks.max = (int)v, integer: true);
        Add(group, "Surface", "Lumenstone.surfaceCountMin", "Minimum stack size", surface.stackSize.min,
            v => surface.stackSize.min = (int)v, min: 1, integer: true);
        Add(group, "Surface", "Lumenstone.surfaceCountMax", "Maximum stack size", surface.stackSize.max,
            v => surface.stackSize.max = (int)v, min: 1, integer: true);

        var stocks = DefDatabase<TraderKindDef>.AllDefsListForReading.SelectMany(t => t.stockGenerators.OfType<StockGenerator_Lumenstone>()).ToList();
        if (stocks.Count > 0)
        {
            Add(group, "Traders", "Lumenstone.traderChance", "Stock chance (%)", stocks[0].stockChance,
                v => stocks.ForEach(s => s.stockChance = v), max: 1, percent: true);
            Add(group, "Traders", "Lumenstone.traderCountMin", "Minimum stock", stocks[0].countRange.min,
                v => stocks.ForEach(s => s.countRange.min = (int)v), min: 1, integer: true);
            Add(group, "Traders", "Lumenstone.traderCountMax", "Maximum stock", stocks[0].countRange.max,
                v => stocks.ForEach(s => s.countRange.max = (int)v), min: 1, integer: true);
        }
        Add(group, "Traders", "Lumenstone.marketValue", "Market value (traders charge about seven times this)", stone.BaseMarketValue,
            v => stone.SetStatBaseValue(StatDefOf.MarketValue, v), min: 0.01f);

        if (ModsConfig.IdeologyActive)
        {
            Add(group, "Ritual", "Lumenstone.ritualPoor", "Poor yield", LumenstoneRitual.PoorYield,
                v => LumenstoneRitual.PoorYield = (int)v, min: 1, integer: true);
            Add(group, "Ritual", "Lumenstone.ritualSuccess", "Successful yield", LumenstoneRitual.SuccessYield,
                v => LumenstoneRitual.SuccessYield = (int)v, min: 1, integer: true);
            Add(group, "Ritual", "Lumenstone.ritualExceptional", "Exceptional yield", LumenstoneRitual.ExceptionalYield,
                v => LumenstoneRitual.ExceptionalYield = (int)v, min: 1, integer: true);
            Add(group, "Ritual", "Lumenstone.ritualCircleYield", "Yield at a Lumen ritual circle (% of altar yield)", LumenstoneRitual.CircleYieldFactor,
                v => LumenstoneRitual.CircleYieldFactor = v, max: 1, percent: true);
            Add(group, "Ritual", "Lumenstone.ritualCooldown", "Cooldown (days)", LumenstoneRitual.CooldownDays,
                v => LumenstoneRitual.CooldownDays = v);
            Add(group, "Ritual", "Lumenstone.ritualQualityCap", "Fixture support quality bonus at full support (%)", LumenstoneSupport.RitualQualityCap,
                v => LumenstoneSupport.RitualQualityCap = v, max: 10, percent: true);
            Add(group, "Ritual", "Lumenstone.ritualYieldCap", "Fixture support yield bonus at full support (%)", LumenstoneSupport.RitualYieldCap,
                v => LumenstoneSupport.RitualYieldCap = v, max: 10, percent: true);
            Add(group, "Ritual", "Lumenstone.ritualSupportRadius", "Fixture support radius (tiles)", LumenstoneSupport.RitualSupportRadius,
                v => LumenstoneSupport.RitualSupportRadius = v, min: 1, max: 50);
            Add(group, "Ritual", "Lumenstone.ritualChunks", "Stone chunk cost", LumenstoneRitual.ChunkCost,
                v => LumenstoneRitual.ChunkCost = (int)v, integer: true);

            var altar = DefDatabase<ThingDef>.GetNamed("KM_LumenstoneAltar");
            var altarCost = altar.costList.First(c => c.thingDef == stone);
            var altarGold = altar.costList.First(c => c.thingDef == ThingDefOf.Gold);
            Add(group, "Lumenstone altar", "Lumenstone.altarCost", "Lumenstone quantity", altarCost.count, v => altarCost.count = (int)v, integer: true);
            Add(group, "Lumenstone altar", "Lumenstone.altarGold", "Gold quantity", altarGold.count, v => altarGold.count = (int)v, integer: true);
            Add(group, "Lumenstone altar", "Lumenstone.altarStuff", "Stone block quantity", altar.costStuffCount,
                v => altar.costStuffCount = (int)v, min: 1, integer: true);
        }

        var circle = DefDatabase<ThingDef>.GetNamed("KM_LumenCircle");
        var circleCost = circle.costList.First(c => c.thingDef == stone);
        Add(group, "Ritual circle", "Lumenstone.circleCost", "Lumenstone quantity", circleCost.count, v => circleCost.count = (int)v, integer: true);
        Add(group, "Ritual circle", "Lumenstone.circleStuff", "Stone block quantity", circle.costStuffCount,
            v => circle.costStuffCount = (int)v, min: 1, integer: true);

        return () =>
        {
            rock.mineableScatterLumpSizeRange.max = Math.Max(rock.mineableScatterLumpSizeRange.min, rock.mineableScatterLumpSizeRange.max);
            surface.stacks.max = Math.Max(surface.stacks.min, surface.stacks.max);
            surface.stackSize.max = Math.Max(surface.stackSize.min, surface.stackSize.max);
            stocks.ForEach(s => s.countRange.max = Math.Max(s.countRange.min, s.countRange.max));
        };
    }

    private static void Register(string group, bool external, params string[] names)
    {
        foreach (string name in names)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if (def == null) continue;
            items.Add(name, (group, external));
            string section = def.LabelCap;
            Add(group, "Availability", name + ".enabled", "Allow new " + def.label +
                (def.building?.mineableThing != null ? " deposits" : ""), 1, toggle: true, external: external);
            if (def.equippedStatOffsets != null)
                foreach (var offset in def.equippedStatOffsets)
                    Add(group, section, name + ".equipped." + offset.stat.defName, offset.stat.LabelCap + " offset while equipped",
                        offset.value, v => offset.value = v, min: -10, max: 10, external: external, percent: true);
            var glow = def.GetCompProperties<CompProperties_Glower>();
            if (glow != null) Add(group, section, name + ".light", "Light radius", glow.glowRadius, v => glow.glowRadius = v, max: 100, external: external);
            var heat = def.GetCompProperties<CompProperties_HeatPusher>();
            if (heat != null)
            {
                bool cooling = heat.heatPerSecond < 0;
                Add(group, section, name + ".heat", cooling ? "Cooling strength (heat / second)" : "Heating strength (heat / second)",
                    Math.Abs(heat.heatPerSecond), v => heat.heatPerSecond = cooling ? -v : v, external: external);
                Add(group, section, name + ".cutoff", heat is CompProperties_GlaciteCooler ? "Active temperature cutoff (C)" : "Temperature cutoff (C)",
                    cooling ? heat.heatPushMinTemperature : heat.heatPushMaxTemperature,
                    v => { if (cooling) heat.heatPushMinTemperature = v; else heat.heatPushMaxTemperature = v; }, -273.15f, 1000, external: external);
                if (heat is CompProperties_GlaciteCooler cooler)
                    Add(group, section, name + ".passiveCutoff", "Passive temperature cutoff (C)", cooler.passiveCutoff,
                        v => cooler.passiveCutoff = v, -273.15f, 1000);
            }
            var vital = def.GetCompProperties<CompProperties_Vitalite>();
            var serenite = def.GetCompProperties<CompProperties_Serenite>();
            var verdite = def.GetCompProperties<CompProperties_Verdite>();
            var aegite = def.GetCompProperties<CompProperties_Aegite>();
            var ferrite = def.GetCompProperties<CompProperties_Ferrite>();
            if (ferrite != null)
            {
                if (!ferrite.personal)
                    Add(group, section, name + ".radius", "Maintenance radius", ferrite.radius, v => ferrite.radius = v, max: 100);
                Add(group, section, name + ".interval", "Ticks between repair pulses", ferrite.interval,
                    v => ferrite.interval = (int)v, min: 1, integer: true);
                Add(group, section, name + ".budget", "Shared condition budget per pulse (%)", ferrite.repairBudget,
                    v => ferrite.repairBudget = v, max: 1, percent: true);
            }
            if (aegite != null)
            {
                Add(group, section, name + ".radius", "Ward radius", aegite.radius, v => aegite.radius = v, max: 100);
                Add(group, section, name + ".interval", "Ticks between ward pulses", aegite.interval,
                    v => aegite.interval = (int)v, min: 1, integer: true);
                Add(group, section, name + ".fireCount", "Maximum fires per pulse", aegite.fireCount,
                    v => aegite.fireCount = (int)v, integer: true);
                Add(group, section, name + ".fireReduction", "Fire size reduction per service", aegite.fireReduction,
                    v => aegite.fireReduction = v, max: 10);
                Add(group, section, name + ".filthLayers", "Maximum filth layers per pulse", aegite.filthLayers,
                    v => aegite.filthLayers = (int)v, integer: true);
            }
            if (verdite != null)
            {
                Add(group, section, name + ".radius", "Agricultural support radius", verdite.radius,
                    v => verdite.radius = v, max: 100);
                Add(group, section, name + ".growth", "Plant growth bonus (%)", verdite.growthBonus,
                    v => verdite.growthBonus = v, max: 10, percent: true);
                Add(group, section, name + ".work", "PlantWorkSpeed bonus (%)", verdite.workBonus,
                    v => verdite.workBonus = v, max: 10, percent: true);
                Add(group, section, name + ".yield", "PlantHarvestYield bonus (%)", verdite.yieldBonus,
                    v => verdite.yieldBonus = v, max: 10, percent: true);
                // The overlight is its own glower, so .light (ordinary) and .plantLight (plant-capable) target different comps.
                var overlight = def.GetCompProperties<CompProperties_Overlight>();
                if (overlight != null)
                {
                    Add(group, section, name + ".plantLight", "Plant-capable lighting radius", overlight.overlightRadius,
                        v => overlight.overlightRadius = overlight.glowRadius = v, max: 40);
                    Add(group, section, name.EndsWith("Pylon") ? "Verdite.pylonSolarDrain" : "Verdite.clusterSolarDrain",
                        "Solar energy per lit schedule", overlight.solarDrain, v => overlight.solarDrain = v);
                }
            }
            if (serenite != null)
                Add(group, section, name + ".support", "Category room contribution (%)", serenite.weight,
                    v => serenite.weight = v, max: 1, percent: true);
            var animastone = def.GetCompProperties<CompProperties_Animastone>();
            if (animastone != null)
                Add(group, section, name + ".support", "Category room contribution (%)", animastone.weight,
                    v => animastone.weight = v, max: 1, percent: true);
            if (animastone?.activeRadius > 0)
            {
                Add(group, section, name + ".activeRadius", "Solar active effect radius (tiles)", animastone.activeRadius,
                    v => animastone.activeRadius = v, min: 1, max: 50);
                Add(group, section, name + ".activeDrain", "Solar energy per day while a pawn meditates in range", animastone.activeDrain,
                    v => animastone.activeDrain = v);
                if (animastone.activeFocusBonus > 0)
                    Add(group, section, name + ".activeFocus", "Active focus strength bonus (%)", animastone.activeFocusBonus,
                        v => animastone.activeFocusBonus = v, max: 1, percent: true);
                if (animastone.activeHeatRecovery > 0)
                    Add(group, section, name + ".activeHeatRecovery", "Active neural heat recovery bonus (%)", animastone.activeHeatRecovery,
                        v => animastone.activeHeatRecovery = v, max: 10, percent: true);
                if (animastone.activeGrowth > 0)
                    Add(group, section, name + ".activeGrowth", "Active anima grass growth offset (%)", animastone.activeGrowth,
                        v => animastone.activeGrowth = v, max: 10, percent: true);
            }
            var battery = def.GetCompProperties<CompProperties_WarpBattery>();
            if (battery != null)
                Add(group, section, name + ".capacity", "Warp energy capacity", battery.capacity, v => battery.capacity = v, min: 1, external: external);
            var fixture = def.GetCompProperties<CompProperties_LumenstoneFixture>();
            if (fixture != null)
                Add(group, section, name + ".support", "Category ritual support contribution (%)", fixture.weight,
                    v => fixture.weight = v, max: 1, percent: true, external: external);
            if (vital != null)
            {
                if (vital.fixtureIndex >= 0)
                    Add(group, section, name + ".support", "Category room contribution (%)", vital.weight, v => vital.weight = v, max: 1, percent: true);
                else
                {
                    Add(group, section, name + ".healing", "Healing contribution (%)", vital.healingBonus, v => vital.healingBonus = v, max: 10, percent: true);
                    Add(group, section, name + ".immunity", "Immunity contribution (%)", vital.immunityBonus, v => vital.immunityBonus = v, max: 10, percent: true);
                    Add(group, section, name + ".infection", "Infection prevention (%)", vital.infectionPrevention, v => vital.infectionPrevention = v, max: 1, percent: true);
                }
                float cleanliness = def.statBases?.FirstOrDefault(s => s.stat == StatDefOf.Cleanliness)?.value ?? 0;
                Add(group, section, name + ".cleanliness", "Cleanliness", cleanliness, v => def.SetStatBaseValue(StatDefOf.Cleanliness, v), external: external);
            }
            if (def.costList != null)
                foreach (var cost in def.costList)
                    Add(group, section, name + ".cost." + cost.thingDef.defName, cost.thingDef.LabelCap + " quantity", cost.count,
                        v => cost.count = (int)v, integer: true, external: external);
            if (def.costStuffCount > 0)
                Add(group, section, name + ".stuff", "Construction material quantity", def.costStuffCount,
                    v => def.costStuffCount = (int)v, min: 1, integer: true, external: external);
            if (def.tools != null)
                for (int i = 0; i < def.tools.Count; i++)
                {
                    var tool = def.tools[i];
                    Add(group, section, name + ".damage." + i, tool.label + " damage", tool.power, v => tool.power = v, external: external);
                    if (tool.extraMeleeDamages == null) continue;
                    for (int j = 0; j < tool.extraMeleeDamages.Count; j++)
                    {
                        var extra = tool.extraMeleeDamages[j];
                        Add(group, section, name + ".extraDamage." + i + "." + j, tool.label + " " + extra.def.label + " damage", extra.amount,
                            v => extra.amount = v, external: external);
                    }
                }
            var rock = def.building;
            if (rock?.mineableThing != null)
            {
                Add(group, "Acquisition", name + ".sizeMin", "Minimum deposit size", rock.mineableScatterLumpSizeRange.min,
                    v => rock.mineableScatterLumpSizeRange.min = (int)v, min: 1, integer: true, external: external);
                Add(group, "Acquisition", name + ".sizeMax", "Maximum deposit size", rock.mineableScatterLumpSizeRange.max,
                    v => rock.mineableScatterLumpSizeRange.max = (int)v, min: 1, integer: true, external: external);
                Add(group, "Acquisition", name + ".yield", "Mining yield", rock.mineableYield, v => rock.mineableYield = (int)v,
                    min: 1, integer: true, external: external);
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
                        var entry = Entries.First(e => e.Key == name + ".cost." + cost.thingDef.defName);
                        entry.Default = ingredient.GetBaseCount();
                        entry.Apply = v => { cost.count = (int)v; ingredient.SetBaseCount(v); };
                    }
                    var stuff = recipe.ingredients.FirstOrDefault(i => i.filter.AllowedThingDefs.Any(d => d.IsStuff));
                    var stuffEntry = Entries.FirstOrDefault(e => e.Key == name + ".stuff");
                    if (stuff != null && stuffEntry != null)
                    {
                        stuffEntry.Default = stuff.GetBaseCount();
                        stuffEntry.Apply = v => { def.costStuffCount = (int)v; stuff.SetBaseCount(v); };
                    }
                    continue;
                }
                RegisterRecipe(group, section, recipe, external);
            }
        }
    }

    private static void RegisterRecipe(string group, string section, RecipeDef recipe, bool external)
    {
        for (int i = 0; i < recipe.ingredients.Count; i++)
        {
            var ingredient = recipe.ingredients[i];
            string key = recipe.defName + ".ingredient." + i;
            if (Entries.Any(e => e.Key == key)) continue;
            Add(group, section, key, ingredient.filter.Summary + " quantity (recipe)", ingredient.GetBaseCount(),
                v => ingredient.SetBaseCount(v), external: external);
        }
    }
}
