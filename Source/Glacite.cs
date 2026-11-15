using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LumenStones;

public static class GlaciteSolar
{
    // Solar energy per day while the cooler is cooling between its passive and active limits.
    public static float FreezerDrain = 40f;

    internal static void DeclareTunables(TunableScope s) =>
        s.In("Lumen_Section_SolarEnergy".Translate()).Add("Glacite.freezerSolarDrain", "Lumen_Glacite_FreezerDrain".Translate(), FreezerDrain, v => FreezerDrain = v);
}

// The native heat pusher reads one cutoff per def. This one cools to passiveCutoff on its own, and down to
// heatPushMinTemperature (the active limit) while its Solar active effect is powered.
public class CompProperties_GlaciteCooler : CompProperties_HeatPusher, ITunableProps, IHeatCutoffLabel
{
    public float passiveCutoff = 4f;
    public CompProperties_GlaciteCooler() => compClass = typeof(CompGlaciteCooler);

    public string CutoffLabel => "Lumen_Glacite_ActiveCutoff".Translate();

    // The registry's heat rows come first, so this follows the active cutoff.
    public void DeclareTunables(TunableScope s, Def owner) =>
        s.Add(owner.defName + ".passiveCutoff", "Lumen_Glacite_PassiveCutoff".Translate(), passiveCutoff, v => passiveCutoff = v, Limit.Number(-273.15f, 1000));

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        foreach (var row in base.SpecialDisplayStats(req)) yield return row;
        var rows = new InfoStats()
            .Add(Effects.GlaciteRefrigeration, "Lumen_Glacite_CoolsTo".Translate(), passiveCutoff.ToStringTemperature("F0"), "Lumen_Glacite_CoolsToDesc".Translate())
            .Add(Effects.GlaciteFreezing, InfoStats.Solar("Lumen_Glacite_CoolsToActive".Translate()), heatPushMinTemperature.ToStringTemperature("F0"), "Lumen_Glacite_CoolsToActiveDesc".Translate() + InfoStats.SolarRule);
        if (SolarRules.Enabled) rows.Add("Lumen_Info_SolarPerDay".Translate(), GlaciteSolar.FreezerDrain.ToString("0.##"), "Lumen_Glacite_SolarPerDayDesc".Translate());
        foreach (var row in rows.Rows) yield return row;
    }
}

public class CompGlaciteCooler : CompHeatPusher, IActiveEffectDrain
{
    private CompSolarToggle toggle;
    private CompProperties_GlaciteCooler Cooler => (CompProperties_GlaciteCooler)props;
    private CompSolarToggle Toggle => toggle ??= parent.GetComp<CompSolarToggle>();

    private static bool Refrigerating => Effects.GlaciteRefrigeration.On;

    // The range the active effect pays for: below the passive limit, or everything above the active limit when
    // refrigeration is switched off.
    private bool InBand(float temperature) => temperature > Cooler.heatPushMinTemperature &&
        (Refrigerating ? temperature <= Cooler.passiveCutoff : temperature < Cooler.heatPushMaxTemperature);

    public float DrainPerTick => Effects.GlaciteFreezing.On && InBand(parent.AmbientTemperature) ? GlaciteSolar.FreezerDrain / GenDate.TicksPerDay : 0;

    private bool ActiveEffect => Effects.GlaciteFreezing.On && Toggle.Powered;

    public override bool ShouldPushHeatNow
    {
        get
        {
            if (!parent.SpawnedOrAnyParentSpawned || !enabled) return false;
            float temperature = parent.AmbientTemperature;
            if (temperature >= Cooler.heatPushMaxTemperature) return false;
            return Refrigerating && temperature > Cooler.passiveCutoff || temperature > Cooler.heatPushMinTemperature && ActiveEffect;
        }
    }

    public override string CompInspectStringExtra()
    {
        if (!Refrigerating && !ActiveEffect) return "Lumen_Glacite_Cooling".Translate(Effects.GlaciteFreezing.On ? "Lumen_Glacite_NotRunning".Translate().ToString() : EffectSwitch.OffText.ToLower());
        return (ShouldPushHeatNow ? "Lumen_Glacite_CoolingTo" : "Lumen_Glacite_CoolingToIdle").Translate((ActiveEffect ? Cooler.heatPushMinTemperature : Cooler.passiveCutoff).ToStringTemperature("F0"));
    }
}
