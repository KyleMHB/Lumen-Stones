using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LumenStones;

// One on/off setting for one effect, keyed "<Tab>.effect.<id>". Off stops the effect for everything, built, worn or
// implanted, without touching availability. Run-time switches are read on hot paths, so the saved value is cached until
// any setting changes. A restart switch changes defs once at startup, so it keeps the value it started with.
public sealed class EffectSwitch
{
    private readonly Func<bool> present;
    private int version = -1;
    private bool on = true, startup = true;

    internal EffectSwitch(TunableTab tab, string id, bool restart = false, Func<bool> present = null, Action disable = null)
    {
        Tab = tab;
        Tunable = new Tunable(tab.Name + ".effect." + id, 1, Limit.Toggle);
        Id = id;
        Restart = restart;
        Disable = disable;
        this.present = present;
    }

    public TunableTab Tab { get; }
    public Tunable Tunable { get; }
    public string Id { get; }
    // Keyed as Lumen_Effect_<Tab>_<id>.
    public string Label => ("Lumen_Effect_" + Tab.Name + "_" + Id).Translate();
    public bool Restart { get; }
    // What a restart switch undoes in the defs when it starts off.
    internal Action Disable { get; }
    // Content the switch belongs to is loaded, such as Pyrinth or Anomaly.
    public bool Present => present?.Invoke() ?? true;

    public bool On
    {
        get
        {
            if (Restart) return startup;
            if (version != Tunables.Version)
            {
                on = Tunable.On;
                version = Tunables.Version;
            }
            return on;
        }
    }

    // Restart switches read their saved value once, after the other settings are applied.
    internal void Capture() => startup = Tunable.On;

    public static string OffText => "Lumen_Effect_Off".Translate();
}

// Every effect switch, by tab, in the order the Effects section lists them.
public static class Effects
{
    private static readonly List<EffectSwitch> all = new();

    private static EffectSwitch Add(TunableTab tab, string id, Func<bool> present = null)
    {
        var effect = new EffectSwitch(tab, id, false, present);
        all.Add(effect);
        return effect;
    }

    private static EffectSwitch AddRestart(TunableTab tab, string id, Action disable, Func<bool> present = null)
    {
        var effect = new EffectSwitch(tab, id, true, present, disable);
        all.Add(effect);
        return effect;
    }

    public static IEnumerable<EffectSwitch> For(TunableTab tab) => all.Where(e => e.Tab == tab);

    private static bool Ideology() => ModsConfig.IdeologyActive;

    // Glacite
    public static readonly EffectSwitch GlaciteFixtureCooling = AddRestart(StoneTabs.Glacite, "fixtureCooling",
        () => EffectDefs.StopHeat("KM_GlaciteLamp", "KM_GlaciteWallLamp", "KM_GlacitePylon", "KM_LumenGlaciteColumn"));
    public static readonly EffectSwitch GlaciteRefrigeration = Add(StoneTabs.Glacite, "coolerRefrigeration");
    public static readonly EffectSwitch GlaciteFreezing = Add(StoneTabs.Glacite, "coolerFreezing");
    // Keeps the id bladeFrost so saved settings carry over; it now switches the greatsword's chill.
    public static readonly EffectSwitch GlaciteBladeChill = Add(StoneTabs.Glacite, "bladeFrost");

    // Vitalite
    public static readonly EffectSwitch VitaliteRoomImmunity = Add(StoneTabs.Vitalite, "roomImmunity");
    public static readonly EffectSwitch VitaliteRoomHealing = Add(StoneTabs.Vitalite, "roomHealing");
    public static readonly EffectSwitch VitaliteRoomInfection = Add(StoneTabs.Vitalite, "roomInfection");
    public static readonly EffectSwitch VitaliteBedBonuses = Add(StoneTabs.Vitalite, "bedBonuses");
    public static readonly EffectSwitch VitaliteCleanliness = AddRestart(StoneTabs.Vitalite, "cleanliness",
        () => EffectDefs.ZeroStat(StatDefOf.Cleanliness, "KM_LumenVitaliteColumn", "KM_VitaliteLamp", "KM_VitaliteWallLamp", "KM_VitalitePylon",
            "KM_VitaliteCluster", "KM_VitaliteSlabBed", "KM_VitaliteCasket"));
    public static readonly EffectSwitch VitaliteCirclet = AddRestart(StoneTabs.Vitalite, "circlet",
        () => EffectDefs.ClearEquipped("KM_VitaliteCirclet"));

    // Serenite
    public static readonly EffectSwitch SereniteRoomComfort = Add(StoneTabs.Serenite, "roomComfort");
    public static readonly EffectSwitch SereniteRoomMood = Add(StoneTabs.Serenite, "roomMood");
    public static readonly EffectSwitch SereniteSleep = Add(StoneTabs.Serenite, "sleep");
    public static readonly EffectSwitch SerenitePendant = AddRestart(StoneTabs.Serenite, "pendant",
        () => EffectDefs.ClearEquipped("KM_SerenitePendant"));

    // Verdite
    public static readonly EffectSwitch VerditeGrowth = Add(StoneTabs.Verdite, "growth");
    public static readonly EffectSwitch VerditeWorkSpeed = Add(StoneTabs.Verdite, "workSpeed");
    public static readonly EffectSwitch VerditeHarvestYield = Add(StoneTabs.Verdite, "harvestYield");
    public static readonly EffectSwitch VerditePlantLight = Add(StoneTabs.Verdite, "plantLight");
    public static readonly EffectSwitch VerditeScytheFarming = Add(StoneTabs.Verdite, "scytheFarming");
    public static readonly EffectSwitch VerditeScytheLifeSteal = Add(StoneTabs.Verdite, "scytheLifeSteal");
    public static readonly EffectSwitch VerditeBracer = AddRestart(StoneTabs.Verdite, "bracer",
        () => EffectDefs.Neutralize("KM_VerditeSustenance"));
    public static readonly EffectSwitch VerditeChew = AddRestart(StoneTabs.Verdite, "chew",
        () => { EffectDefs.Neutralize("KM_VerditeChewed"); EffectDefs.Silence("KM_VerditeBleedingGums"); });

    // Aegite
    public static readonly EffectSwitch AegiteFilth = Add(StoneTabs.Aegite, "filthCleaning");
    public static readonly EffectSwitch AegiteFire = Add(StoneTabs.Aegite, "fireSuppression");
    public static readonly EffectSwitch AegiteShieldArmor = AddRestart(StoneTabs.Aegite, "shieldArmor",
        () => EffectDefs.ClearEquipped("KM_AegiteShield"));
    public static readonly EffectSwitch AegiteShieldFire = Add(StoneTabs.Aegite, "shieldFireImmunity");
    public static readonly EffectSwitch AegiteCharm = AddRestart(StoneTabs.Aegite, "charm",
        () => DefDatabase<ThingDef>.GetNamedSilentFail("KM_AegiteShieldCharm")?.comps.RemoveAll(c => c is CompProperties_Shield));

    // Ferrite
    public static readonly EffectSwitch FerriteBuildings = Add(StoneTabs.Ferrite, "repairBuildings");
    public static readonly EffectSwitch FerriteItems = Add(StoneTabs.Ferrite, "repairItems");
    public static readonly EffectSwitch FerriteGear = Add(StoneTabs.Ferrite, "repairGear");
    public static readonly EffectSwitch FerriteHammerStun = Add(StoneTabs.Ferrite, "warhammerStun");
    public static readonly EffectSwitch FerriteHammerMending = Add(StoneTabs.Ferrite, "warhammerMending");

    // Sunstone, which also holds the Lunite and shield projector settings
    public static readonly EffectSwitch SunstoneAxeBane = Add(StoneTabs.Sunstone, "axeBane");
    public static readonly EffectSwitch SunstoneAxeProc = Add(StoneTabs.Sunstone, "axeBlindStun");
    public static readonly EffectSwitch SunstoneFlare = Add(StoneTabs.Sunstone, "flare");
    public static readonly EffectSwitch ProjectorInterception = Add(StoneTabs.Sunstone, "projectorInterception");
    public static readonly EffectSwitch ProjectorCrushing = Add(StoneTabs.Sunstone, "projectorCrushing");
    public static readonly EffectSwitch LuniteStabilizer = Add(StoneTabs.Sunstone, "stabilizer");

    // Lumenstone
    public static readonly EffectSwitch LumenstoneRitualSupport = Add(StoneTabs.Lumenstone, "ritualSupport", Ideology);
    public static readonly EffectSwitch LumenstoneStaff = AddRestart(StoneTabs.Lumenstone, "staff",
        () => EffectDefs.ClearEquipped("KM_LumenstoneStaff"));

    // Pyrinth column
    public static readonly EffectSwitch PyrinthColumnHeat = AddRestart(StoneTabs.Pyrinth, "columnHeat",
        () => EffectDefs.StopHeat("KM_LumenPyrinthColumn"), () => StoneSettings.PyrinthPresent);
    public static readonly EffectSwitch PyrinthColumnFocus = AddRestart(StoneTabs.Pyrinth, "columnFocus",
        () => EffectDefs.StopFocus("KM_LumenPyrinthColumn"), () => StoneSettings.PyrinthPresent);

    // Animastone
    public static readonly EffectSwitch AnimastoneFocus = AddRestart(StoneTabs.Animastone, "focus",
        () => EffectDefs.StopFocus(AnimastoneTunables.Fixtures));
    public static readonly EffectSwitch AnimastoneRoomHeat = Add(StoneTabs.Animastone, "roomHeatRecovery");
    public static readonly EffectSwitch AnimastoneRoomFocus = Add(StoneTabs.Animastone, "roomFocusGain");
    public static readonly EffectSwitch AnimastoneClusterFocus = Add(StoneTabs.Animastone, "clusterFocus");
    public static readonly EffectSwitch AnimastonePylonHeat = Add(StoneTabs.Animastone, "pylonHeatRecovery");
    public static readonly EffectSwitch AnimastonePylonGrowth = Add(StoneTabs.Animastone, "pylonGrowth");
    public static readonly EffectSwitch AnimastoneCirclet = AddRestart(StoneTabs.Animastone, "circlet",
        () => EffectDefs.ClearEquipped("KM_AnimastoneCirclet"));
    public static readonly EffectSwitch AnimastoneStaffStats = AddRestart(StoneTabs.Animastone, "staffStats",
        () => EffectDefs.ClearEquipped("KM_AnimastoneStaff"));
    public static readonly EffectSwitch AnimastoneStaffStun = Add(StoneTabs.Animastone, "staffStun");

    // Dark Stone
    public static readonly EffectSwitch DarkRitualSupport = Add(StoneTabs.DarkStone, "ritualSupport");
    public static readonly EffectSwitch Crystallize = Add(StoneTabs.DarkStone, "crystallize");
    public static readonly EffectSwitch Harvest = Add(StoneTabs.DarkStone, "harvest");
    public static readonly EffectSwitch RitualKnife = Add(StoneTabs.DarkStone, "ritualKnife");
    public static readonly EffectSwitch DreadstoneSeed = Add(StoneTabs.DarkStone, "seed");

    // Sanguinite
    public static readonly EffectSwitch SanguiniteBleeding = Add(StoneTabs.Sanguinite, "bleeding");
    public static readonly EffectSwitch SanguiniteScars = Add(StoneTabs.Sanguinite, "scars");
    public static readonly EffectSwitch SanguiniteDigits = Add(StoneTabs.Sanguinite, "regrowDigits");
    public static readonly EffectSwitch SanguiniteSmallParts = Add(StoneTabs.Sanguinite, "regrowSmallParts");
    public static readonly EffectSwitch SanguiniteOrgans = Add(StoneTabs.Sanguinite, "regrowOrgans");
    public static readonly EffectSwitch SanguiniteHemorrhage = Add(StoneTabs.Sanguinite, "hemorrhage");
    public static readonly EffectSwitch SanguiniteWielderBleed = AddRestart(StoneTabs.Sanguinite, "wielderBleed",
        () => EffectDefs.Neutralize("KM_FlensingWielder"));

    // Dreadstone
    public static readonly EffectSwitch DreadstoneSubjugation = Add(StoneTabs.Dreadstone, "subjugation");
    public static readonly EffectSwitch DreadstoneDread = Add(StoneTabs.Dreadstone, "dread");
    public static readonly EffectSwitch DreadstoneSiphon = Add(StoneTabs.Dreadstone, "siphon");
    public static readonly EffectSwitch DreadstoneArbalest = Add(StoneTabs.Dreadstone, "arbalestDread");
    public static readonly EffectSwitch DreadstoneCudgel = Add(StoneTabs.Dreadstone, "cudgel");
    public static readonly EffectSwitch DreadstoneCollar = Add(StoneTabs.Dreadstone, "collar");

    // Hollowstone
    public static readonly EffectSwitch HollowstonePain = Add(StoneTabs.Hollowstone, "painWard");
    public static readonly EffectSwitch HollowstoneBreaks = Add(StoneTabs.Hollowstone, "breakSteadiness");
    public static readonly EffectSwitch HollowstoneHusks = Add(StoneTabs.Hollowstone, "workHusks", () => HuskRules.Available);
    public static readonly EffectSwitch HollowstoneGlaive = AddRestart(StoneTabs.Hollowstone, "glaive",
        () => { EffectDefs.Neutralize("KM_GlaiveWielder"); EffectDefs.ClearEquipped("KM_HollowstoneGlaive"); });

    // Implants and prosthetics
    public static readonly EffectSwitch ImplantApparelMending = Add(StoneTabs.Implants, "apparelMending");
    public static readonly EffectSwitch ImplantPartHealing = Add(StoneTabs.Implants, "partHealing");
    public static readonly EffectSwitch ImplantScarMending = Add(StoneTabs.Implants, "scarMending", Ideology);
    public static readonly EffectSwitch ImplantDreadAura = Add(StoneTabs.Implants, "dreadAura", Ideology);
    public static readonly EffectSwitch ImplantMood = Add(StoneTabs.Implants, "mood");
    public static readonly EffectSwitch ImplantRelief = Add(StoneTabs.Implants, "relief");
}

// The def changes behind restart switches. Each runs once at startup, after the settings are applied.
internal static class EffectDefs
{
    private static ThingDef Thing(string name) => DefDatabase<ThingDef>.GetNamedSilentFail(name);

    public static void StopHeat(params string[] names)
    {
        foreach (string name in names)
            if (Thing(name)?.GetCompProperties<CompProperties_HeatPusher>() is { } heat) heat.heatPerSecond = 0f;
    }

    public static void StopFocus(params string[] names)
    {
        var strength = DefDatabase<StatDef>.GetNamed("MeditationFocusStrength");
        foreach (string name in names)
        {
            var def = Thing(name);
            if (def == null) continue;
            def.SetStatBaseValue(strength, 0f);
            if (def.GetCompProperties<CompProperties_MeditationFocus>() is { } focus) focus.offsets?.Clear();
        }
    }

    public static void ZeroStat(StatDef stat, params string[] names)
    {
        foreach (string name in names)
            if (Thing(name) is { } def && def.statBases?.Any(s => s.stat == stat) == true) def.SetStatBaseValue(stat, 0f);
    }

    public static void ClearEquipped(string name)
    {
        if (Thing(name) is not { } def) return;
        def.equippedStatOffsets?.Clear();
    }

    // Leaves the hediff in place, with every stage doing nothing.
    public static void Neutralize(string name)
    {
        if (DefDatabase<HediffDef>.GetNamedSilentFail(name)?.stages is not { } stages) return;
        foreach (var stage in stages)
        {
            stage.painFactor = 1f;
            stage.painOffset = 0f;
            stage.totalBleedFactor = 1f;
            stage.hungerRateFactor = 1f;
            stage.hungerRateFactorOffset = 0f;
            stage.restFallFactor = 1f;
            stage.restFallFactorOffset = 0f;
            stage.statOffsets = null;
            stage.statFactors = null;
            stage.capMods = new List<PawnCapacityModifier>();
        }
    }

    public static void Silence(string thought)
    {
        if (DefDatabase<ThoughtDef>.GetNamedSilentFail(thought)?.stages is not { } stages) return;
        foreach (var stage in stages)
            if (stage != null) stage.baseMoodEffect = 0f;
    }
}
