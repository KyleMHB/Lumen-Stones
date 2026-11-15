using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Glow;

namespace LumenStones;

// A second glower that gives plant-capable light. The fixture's ordinary glower stays on, so the fixture must not carry a
// CompSchedule, which would switch every glower on it off. The overlight follows the daily schedule only while the Solar
// active effect is toggled on and powered.
public class CompProperties_Overlight : CompProperties_Glower
{
    public float startTime = 0.25f, endTime = 0.8f;
    // Solar energy per lit schedule, spread evenly over its length.
    public float solarDrain;
    // The settings key of the drain, one per fixture kind.
    public string solarDrainKey;
    public CompProperties_Overlight() => compClass = typeof(CompOverlight);

    public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
    {
        foreach (var error in base.ConfigErrors(parentDef)) yield return error;
        if (solarDrain > 0 && string.IsNullOrEmpty(solarDrainKey)) yield return "a Solar drain needs a solarDrainKey"; // not translated
    }

    // Declared by CompProperties_Verdite, after its own rows, so this is not an ITunableProps.
    internal void DeclareOverlight(TunableScope s, Def owner)
    {
        s.Add(owner.defName + ".plantLight", "Lumen_Overlight_RadiusSetting".Translate(), overlightRadius,
            v => { overlightRadius = v; glowRadius = 2f * v; }, Limit.Number(max: 40));
        if (!string.IsNullOrEmpty(solarDrainKey))
            s.Add(solarDrainKey, "Lumen_Overlight_SolarPerSchedule".Translate(), solarDrain, v => solarDrain = v);
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        foreach (var row in base.SpecialDisplayStats(req)) yield return row;
        string rule = "Lumen_Overlight_Rule".Translate() +
            InfoStats.SolarRule;
        var rows = new InfoStats(4000)
            .Add(Effects.VerditePlantLight, InfoStats.Solar("Lumen_Overlight_Radius".Translate()), InfoStats.Tiles(overlightRadius), rule)
            .Add(InfoStats.Solar("Lumen_Overlight_Hours".Translate()), "Lumen_Overlight_HoursValue".Translate($"{startTime * 24:0}:00", $"{endTime * 24:0}:00"), rule);
        if (SolarRules.Enabled) rows.Add("Lumen_Info_SolarPerDay".Translate(), solarDrain.ToString("0.##"), "Lumen_Overlight_SolarPerDayDesc".Translate() + " " + rule);
        foreach (var row in rows.Rows) yield return row;
    }
}

public class CompOverlight : CompGlower, IActiveEffectDrain
{
    private CompSolarToggle toggle;
    private CompProperties_Overlight Overlight => (CompProperties_Overlight)props;
    private CompSolarToggle Toggle => toggle ??= parent.GetComp<CompSolarToggle>();

    private bool InSchedule
    {
        get
        {
            float time = GenLocalDate.DayPercent(parent);
            return time > Overlight.startTime && time < Overlight.endTime;
        }
    }

    public float DrainPerTick => Effects.VerditePlantLight.On && !CompLumenDarklight.On(parent) && InSchedule
        ? Overlight.solarDrain / ((Overlight.endTime - Overlight.startTime) * GenDate.TicksPerDay) : 0;

    protected override bool ShouldBeLitNow => Effects.VerditePlantLight.On && !CompLumenDarklight.On(parent) && base.ShouldBeLitNow && InSchedule && Toggle.Powered;

    // Schedule edges are noticed within one interval. UpdateLit does nothing when the lit state is unchanged.
    public override void CompTick()
    {
        if (parent.Spawned && parent.IsHashIntervalTick(250)) UpdateLit(parent.Map);
    }

    public override void ReceiveCompSignal(string signal)
    {
        base.ReceiveCompSignal(signal);
        if ((signal == ActiveEffectKind.Solar.ToggledSignal || signal == ActiveEffectKind.Solar.PoweredSignal) && parent.Spawned) UpdateLit(parent.Map);
    }

    public override string CompInspectStringExtra() => !Effects.VerditePlantLight.On ? "Lumen_Overlight_Status".Translate(EffectSwitch.OffText.ToLower())
        : CompLumenDarklight.On(parent) ? "Lumen_Overlight_Darklight".Translate()
        : "Lumen_Overlight_Status".Translate(ShouldBeLitNow ? "Lumen_Overlight_On".Translate()
            : !InSchedule ? "Lumen_Overlight_OffUntil".Translate((Overlight.startTime * 24).ToString("0") + ":00") : "Lumen_Overlight_NotRunning".Translate());
}

// Keep settings, range rings and reserve checks in tile distances. RimWorld's light
// flood starts at distance 1 on the source tile, rather than 0.
[HarmonyPatch(typeof(GlowLight), MethodType.Constructor, typeof(CompGlower), typeof(int))]
public static class VerditeOverlightDistance
{
    public static void Postfix(CompGlower glower, ref float ___overlightRadius)
    {
        if (glower is CompOverlight && ___overlightRadius > 0f) ___overlightRadius += 1f;
    }
}
