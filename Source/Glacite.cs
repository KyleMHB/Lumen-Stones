using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LumenStones;

public static class GlaciteSolar
{
    // Solar energy per day while the cooler is cooling between its passive and active limits.
    public static float FreezerDrain = 40f;

    internal static void DeclareTunables(TunableScope s) =>
        s.In("Solar energy").Add("Glacite.freezerSolarDrain", "Solar energy per day while cooling between the two limits", FreezerDrain, v => FreezerDrain = v);
}

// The native heat pusher reads one cutoff per def. This one cools to passiveCutoff on its own, and down to
// heatPushMinTemperature (the active limit) while its Solar active effect is powered.
public class CompProperties_GlaciteCooler : CompProperties_HeatPusher, ITunableProps, IHeatCutoffLabel
{
    public float passiveCutoff = 4f;
    public CompProperties_GlaciteCooler() => compClass = typeof(CompGlaciteCooler);

    public string CutoffLabel => "Active temperature cutoff (C)";

    // The registry's heat rows come first, so this follows the active cutoff.
    public void DeclareTunables(TunableScope s, Def owner) =>
        s.Add(owner.defName + ".passiveCutoff", "Passive temperature cutoff (C)", passiveCutoff, v => passiveCutoff = v, Limit.Number(-273.15f, 1000));

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        foreach (var row in base.SpecialDisplayStats(req)) yield return row;
        var rows = new InfoStats()
            .Add("Cools to", passiveCutoff.ToStringTemperature("F0"), "Absorbs heat on its own, without fuel, electricity or an exhaust vent. A larger room or hotter weather may need more than one cooler.")
            .Add(InfoStats.Solar("Cools to with active effect"), heatPushMinTemperature.ToStringTemperature("F0"), "Keeps cooling below its own limit, down to this temperature." + InfoStats.SolarRule);
        if (SolarRules.Enabled) rows.Add("Solar energy per day", GlaciteSolar.FreezerDrain.ToString("0.##"), "Spent only while the air is between the two limits.");
        foreach (var row in rows.Rows) yield return row;
    }
}

public class CompGlaciteCooler : CompHeatPusher, IActiveEffectDrain
{
    private CompSolarToggle toggle;
    private CompProperties_GlaciteCooler Cooler => (CompProperties_GlaciteCooler)props;
    private CompSolarToggle Toggle => toggle ??= parent.GetComp<CompSolarToggle>();

    private bool InBand(float temperature) => temperature <= Cooler.passiveCutoff && temperature > Cooler.heatPushMinTemperature;

    public float DrainPerTick => InBand(parent.AmbientTemperature) ? GlaciteSolar.FreezerDrain / GenDate.TicksPerDay : 0;

    private bool ActiveEffect => Toggle.Powered;

    public override bool ShouldPushHeatNow
    {
        get
        {
            if (!parent.SpawnedOrAnyParentSpawned || !enabled) return false;
            float temperature = parent.AmbientTemperature;
            if (temperature >= Cooler.heatPushMaxTemperature) return false;
            return temperature > Cooler.passiveCutoff || temperature > Cooler.heatPushMinTemperature && ActiveEffect;
        }
    }

    public override string CompInspectStringExtra() =>
        "Cooling to " + (ActiveEffect ? Cooler.heatPushMinTemperature : Cooler.passiveCutoff).ToStringTemperature("F0") + (ShouldPushHeatNow ? "" : ", idle");
}
