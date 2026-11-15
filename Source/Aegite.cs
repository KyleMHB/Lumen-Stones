using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_Aegite : CompProperties, ITunableProps
{
    public int tier = 1, interval = 360, fireCount = 2, filthLayers = 1;
    public float radius = 8f, fireReduction = 0.5f;
    public CompProperties_Aegite() => compClass = typeof(CompAegite);

    public void DeclareTunables(TunableScope s, Def owner)
    {
        string name = owner.defName;
        s.Add(name + ".radius", "Lumen_Aegite_WardRadius".Translate(), radius, v => radius = v, Limit.Number(max: 100));
        s.Add(name + ".interval", "Lumen_Aegite_Interval".Translate(), interval, v => interval = (int)v, Limit.Integer(1));
        s.Add(name + ".fireCount", "Lumen_Aegite_FireCount".Translate(), fireCount, v => fireCount = (int)v, Limit.Integer());
        s.Add(name + ".fireReduction", "Lumen_Aegite_FireReduction".Translate(), fireReduction, v => fireReduction = v, Limit.Number(max: 10));
        s.Add(name + ".filthLayers", "Lumen_Aegite_FilthLayers".Translate(), filthLayers, v => filthLayers = (int)v, Limit.Integer());
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        string ward = "Lumen_Aegite_WardRule".Translate();
        string fire = "Lumen_Aegite_FireRule".Translate() + InfoStats.SolarRule + " " + ward;
        var rows = new InfoStats()
            .Add("Lumen_Aegite_WardRadius".Translate(), InfoStats.Tiles(radius), "Lumen_Aegite_WardRadiusDesc".Translate() + " " + ward)
            .Add("Lumen_Aegite_PulseInterval".Translate(), InfoStats.Seconds(interval), ward)
            .Add(Effects.AegiteFilth, "Lumen_Aegite_FilthThinned".Translate(), filthLayers.ToString(), "Lumen_Aegite_FilthThinnedDesc".Translate() + " " + ward)
            .Add(Effects.AegiteFire, InfoStats.Solar("Lumen_Aegite_FiresSuppressed".Translate()), fireCount.ToString(), fire)
            .Add(Effects.AegiteFire, InfoStats.Solar("Lumen_Aegite_FireSizeRemoved".Translate()), fireReduction.ToString("0.##"), fire);
        if (SolarRules.Enabled) rows.Add("Lumen_Aegite_SolarPerFire".Translate(), AegiteSolar.CostPerFire.ToString("0.##"), fire);
        return rows.Rows;
    }
}

public static class AegiteSolar
{
    public static float CostPerFire = 2f;

    internal static void DeclareTunables(TunableScope s) =>
        s.In("Lumen_Section_SolarEnergy".Translate()).Add("Aegite.solarCostPerFire", "Lumen_Aegite_SolarPerFireSetting".Translate(), CostPerFire, v => CostPerFire = v);
}

public class AegiteField : MapComponent
{
    public readonly List<CompAegite> fixtures = new();
    public AegiteField(Map map) : base(map) { }

    public CompAegite Owner(IntVec3 position)
    {
        CompAegite best = null;
        foreach (var fixture in fixtures)
        {
            if (!fixture.parent.Spawned || fixture.parent.Position.DistanceToSquared(position) > fixture.Props.radius * fixture.Props.radius) continue;
            if (best == null || fixture.Props.tier > best.Props.tier ||
                fixture.Props.tier == best.Props.tier && fixture.parent.thingIDNumber < best.parent.thingIDNumber)
                best = fixture;
        }
        return best;
    }
}

public class CompAegite : ThingComp
{
    public CompProperties_Aegite Props => (CompProperties_Aegite)props;
    private static readonly List<Fire> fireTargets = new();
    private static readonly List<Filth> filthTargets = new();

    public override void PostSpawnSetup(bool respawningAfterLoad) => MapState.Of<AegiteField>(parent.Map).fixtures.Add(this);
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) => MapState.Of<AegiteField>(map).fixtures.Remove(this);

    public override void CompTick()
    {
        if (!parent.Spawned) return;
        if (!parent.IsHashIntervalTick(Props.interval)) return;
        bool fires = Effects.AegiteFire.On, filth = Effects.AegiteFilth.On;
        if (!fires && !filth) return;
        var map = parent.Map;
        var field = MapState.Of<AegiteField>(map);
        // Ownership considers all covering fixtures, even those between pulses. A weaker
        // or equal-tier installation cannot fill the gaps in another ward's interval.
        // Only cells in the ward's radius can hold an owned target, so the map's fire and filth lists are never scanned.
        fireTargets.Clear();
        filthTargets.Clear();
        int cells = GenRadial.NumCellsInRadius(Props.radius);
        for (int i = 0; i < cells; i++)
        {
            var cell = parent.Position + GenRadial.RadialPattern[i];
            if (!cell.InBounds(map)) continue;
            var things = map.thingGrid.ThingsListAtFast(cell);
            for (int j = 0; j < things.Count; j++)
            {
                if (things[j] is Fire fire) { if (fires && field.Owner(cell) == this) fireTargets.Add(fire); }
                else if (filth && things[j] is Filth layer && field.Owner(cell) == this) filthTargets.Add(layer);
            }
        }
        // Each fire costs Solar energy. The ward services as many as it can pay for, and thins filth only if it paid for none.
        if (fireTargets.Count > 0)
        {
            fireTargets.SortBy(f => f.thingIDNumber);
            if (fireTargets.Count > Props.fireCount) fireTargets.RemoveRange(Props.fireCount, fireTargets.Count - Props.fireCount);
            var solar = parent.GetComp<CompSolarToggle>();
            int serviced = 0;
            foreach (var fire in fireTargets)
            {
                if (solar != null && !solar.TryPay(AegiteSolar.CostPerFire)) break;
                fire.fireSize -= Props.fireReduction;
                if (fire.fireSize <= 0) fire.Destroy();
                serviced++;
            }
            if (serviced > 0) return;
        }
        int remaining = Props.filthLayers;
        filthTargets.SortBy(f => f.thingIDNumber);
        foreach (var target in filthTargets)
        {
            while (remaining > 0 && target.Spawned && !target.Destroyed)
            {
                target.ThinFilth();
                remaining--;
            }
            if (remaining == 0) break;
        }
    }

}

public class PlaceWorker_AegiteRange : PlaceWorker
{
    public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null) =>
        GenDraw.DrawRadiusRing(center, def.GetCompProperties<CompProperties_Aegite>().radius, new Color(1f, 0.9f, 0.55f));
}
