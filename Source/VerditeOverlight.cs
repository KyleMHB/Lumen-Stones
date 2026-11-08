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
        if (solarDrain > 0 && string.IsNullOrEmpty(solarDrainKey)) yield return "a Solar drain needs a solarDrainKey";
    }

    // Declared by CompProperties_Verdite, after its own rows, so this is not an ITunableProps.
    internal void DeclareOverlight(TunableScope s, Def owner)
    {
        s.Add(owner.defName + ".plantLight", "Plant-capable lighting radius", overlightRadius,
            v => { overlightRadius = v; glowRadius = 2f * v; }, Limit.Number(max: 40));
        if (!string.IsNullOrEmpty(solarDrainKey))
            s.Add(solarDrainKey, "Solar energy per lit schedule", solarDrain, v => solarDrain = v);
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        foreach (var row in base.SpecialDisplayStats(req)) yield return row;
        string rule = "A separate gold ring of plant-capable light that follows the daily sun-lamp schedule. Plants keep their usual light, temperature and growing needs. The fixture's ordinary green light is always on." +
            InfoStats.SolarRule;
        var rows = new InfoStats(4000)
            .Add(InfoStats.Solar("Plant-capable light radius"), InfoStats.Tiles(overlightRadius), rule)
            .Add(InfoStats.Solar("Plant-capable light hours"), $"{startTime * 24:0}:00 to {endTime * 24:0}:00", rule);
        if (SolarRules.Enabled) rows.Add("Solar energy per day", solarDrain.ToString("0.##"), "Spent evenly while the light is lit. " + rule);
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

    public float DrainPerTick => !CompLumenDarklight.On(parent) && InSchedule
        ? Overlight.solarDrain / ((Overlight.endTime - Overlight.startTime) * GenDate.TicksPerDay) : 0;

    protected override bool ShouldBeLitNow => !CompLumenDarklight.On(parent) && base.ShouldBeLitNow && InSchedule && Toggle.Powered;

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

    public override string CompInspectStringExtra() => CompLumenDarklight.On(parent)
        ? "Darklight: always on; plant-capable light disabled"
        : "Plant-capable light: " + (ShouldBeLitNow ? "on" : !InSchedule ? "off until " + (Overlight.startTime * 24).ToString("0") + ":00" : "off, active effect not running");
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
