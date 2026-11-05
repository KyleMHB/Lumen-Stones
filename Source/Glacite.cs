using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LumenStones;

public static class GlaciteSolar
{
    // Solar energy per day while the cooler is cooling between its passive and active limits.
    public static float FreezerDrain = 40f;
}

// The native heat pusher reads one cutoff per def. This one cools to passiveCutoff on its own, and down to
// heatPushMinTemperature (the active limit) while its Solar active effect is powered.
public class CompProperties_GlaciteCooler : CompProperties_HeatPusher
{
    public float passiveCutoff = 4f;
    public CompProperties_GlaciteCooler() => compClass = typeof(CompGlaciteCooler);

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

public class CompGlaciteCooler : CompHeatPusher, ISolarConsumer
{
    private CompSolarToggle toggle;
    private CompProperties_GlaciteCooler Cooler => (CompProperties_GlaciteCooler)props;
    private CompSolarToggle Toggle => toggle ??= parent.GetComp<CompSolarToggle>();

    public bool SolarPowered { get; set; }

    private bool InBand(float temperature) => temperature <= Cooler.passiveCutoff && temperature > Cooler.heatPushMinTemperature;

    public float SolarDrainPerTick => SolarRules.Enabled && parent.Spawned && Toggle.Running && InBand(parent.AmbientTemperature)
        ? GlaciteSolar.FreezerDrain / GenDate.TicksPerDay : 0;

    private bool ActiveEffect => !SolarRules.Enabled || Toggle.Running && SolarPowered;

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

    public override void PostSpawnSetup(bool respawningAfterLoad) => SolarNetwork.For(parent.Map).Register(this);
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) => SolarNetwork.For(map).Unregister(this);

    public override string CompInspectStringExtra() =>
        "Cooling to " + (ActiveEffect ? Cooler.heatPushMinTemperature : Cooler.passiveCutoff).ToStringTemperature("F0") + (ShouldPushHeatNow ? "" : ", idle");
}
