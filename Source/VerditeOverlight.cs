using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LumenStones;

// A second glower that gives plant-capable light. The fixture's ordinary glower stays on, so the fixture must not carry a
// CompSchedule, which would switch every glower on it off. The overlight follows the daily schedule only while the Solar
// active effect is toggled on and powered.
public class CompProperties_Overlight : CompProperties_Glower
{
    public float startTime = 0.25f, endTime = 0.8f;
    // Solar energy per lit schedule, spread evenly over its length.
    public float solarDrain;
    public CompProperties_Overlight() => compClass = typeof(CompOverlight);

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

public class CompOverlight : CompGlower, ISolarConsumer
{
    private bool powered;
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

    private bool ActiveEffect => !SolarRules.Enabled || Toggle.Running && powered;

    public bool SolarPowered
    {
        get => powered;
        set
        {
            if (powered == value) return;
            powered = value;
            if (parent.Spawned) UpdateLit(parent.Map);
        }
    }

    public float SolarDrainPerTick => !CompLumenDarklight.On(parent) && SolarRules.Enabled && parent.Spawned && Toggle.Running && InSchedule
        ? Overlight.solarDrain / ((Overlight.endTime - Overlight.startTime) * GenDate.TicksPerDay) : 0;

    protected override bool ShouldBeLitNow => !CompLumenDarklight.On(parent) && base.ShouldBeLitNow && InSchedule && ActiveEffect;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        SolarNetwork.For(parent.Map).Register(this);
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        SolarNetwork.For(map).Unregister(this);
        base.PostDeSpawn(map, mode);
    }

    // Schedule edges are noticed within one interval. UpdateLit does nothing when the lit state is unchanged.
    public override void CompTick()
    {
        if (parent.Spawned && parent.IsHashIntervalTick(250)) UpdateLit(parent.Map);
    }

    public override void ReceiveCompSignal(string signal)
    {
        base.ReceiveCompSignal(signal);
        if (signal == SolarRules.ToggledSignal && parent.Spawned) UpdateLit(parent.Map);
    }

    public override string CompInspectStringExtra() => CompLumenDarklight.On(parent)
        ? "Darklight: always on; plant-capable light disabled"
        : "Plant-capable light: " + (ShouldBeLitNow ? "on" : !InSchedule ? "off until " + (Overlight.startTime * 24).ToString("0") + ":00" : "off, active effect not running");
}
