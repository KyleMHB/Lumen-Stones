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
        s.Add(name + ".radius", "Ward radius", radius, v => radius = v, Limit.Number(max: 100));
        s.Add(name + ".interval", "Ticks between ward pulses", interval, v => interval = (int)v, Limit.Integer(1));
        s.Add(name + ".fireCount", "Maximum fires per pulse", fireCount, v => fireCount = (int)v, Limit.Integer());
        s.Add(name + ".fireReduction", "Fire size reduction per service", fireReduction, v => fireReduction = v, Limit.Number(max: 10));
        s.Add(name + ".filthLayers", "Maximum filth layers per pulse", filthLayers, v => filthLayers = (int)v, Limit.Integer());
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        string ward = "Only the strongest covering ward tier services each fire or filth target, and among equal tiers only one ward does. Overlapping wards add nothing.";
        string fire = "Each pulse suppresses owned fires instead of thinning filth." + InfoStats.SolarRule + " " + ward;
        var rows = new InfoStats()
            .Add("Ward radius", InfoStats.Tiles(radius), "Works indoors, outdoors and through walls. " + ward)
            .Add("Pulse interval", InfoStats.Seconds(interval), ward)
            .Add("Filth layers thinned per pulse", filthLayers.ToString(), "Used when there is no fire to suppress, or the Solar active effect can't run. " + ward)
            .Add(InfoStats.Solar("fireTargets suppressed per pulse"), fireCount.ToString(), fire)
            .Add(InfoStats.Solar("Fire size removed"), fireReduction.ToString("0.##"), fire);
        if (SolarRules.Enabled) rows.Add("Solar energy per fire", AegiteSolar.CostPerFire.ToString("0.##"), fire);
        return rows.Rows;
    }
}

public static class AegiteSolar
{
    public static float CostPerFire = 2f;

    internal static void DeclareTunables(TunableScope s) =>
        s.In("Solar energy").Add("Aegite.solarCostPerFire", "Solar energy per fire suppressed", CostPerFire, v => CostPerFire = v);
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

    public override void PostSpawnSetup(bool respawningAfterLoad) => parent.Map.GetComponent<AegiteField>().fixtures.Add(this);
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) => map.GetComponent<AegiteField>().fixtures.Remove(this);

    public override void CompTick()
    {
        if (!parent.Spawned) return;
        if (parent.IsHashIntervalTick(480)) Glint(parent.Position, 0.06f);
        if (!parent.IsHashIntervalTick(Props.interval)) return;
        var map = parent.Map;
        var field = map.GetComponent<AegiteField>();
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
                if (things[j] is Fire fire) { if (field.Owner(cell) == this) fireTargets.Add(fire); }
                else if (things[j] is Filth filth && field.Owner(cell) == this) filthTargets.Add(filth);
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
                var cell = fire.Position;
                fire.fireSize -= Props.fireReduction;
                if (fire.fireSize <= 0) fire.Destroy();
                Glint(cell, 0.09f);
                serviced++;
            }
            if (serviced > 0) return;
        }
        int remaining = Props.filthLayers;
        filthTargets.SortBy(f => f.thingIDNumber);
        foreach (var target in filthTargets)
        {
            var cell = target.Position;
            while (remaining > 0 && target.Spawned && !target.Destroyed)
            {
                target.ThinFilth();
                remaining--;
            }
            Glint(cell, 0.09f);
            if (remaining == 0) break;
        }
    }

    private void Glint(IntVec3 cell, float scale)
    {
        if (cell.Fogged(parent.Map) || !CompAnimationToggle.On(parent)) return;
        var mote = FixtureMotes.Make("KM_Mote_AegitePulse", parent.Map);
        if (mote == null) return;
        mote.exactPosition = cell.ToVector3Shifted();
        mote.exactPosition.y = AltitudeLayer.MoteOverhead.AltitudeFor();
        mote.Scale = scale;
        mote.instanceColor = new Color(1f, 0.94f, 0.72f);
        mote.airTimeLeft = 0.5f;
        mote.SetVelocity(0, 0.04f);
        GenSpawn.Spawn(mote, cell, parent.Map);
    }

}

public class PlaceWorker_AegiteRange : PlaceWorker
{
    public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null) =>
        GenDraw.DrawRadiusRing(center, def.GetCompProperties<CompProperties_Aegite>().radius, new Color(1f, 0.9f, 0.55f));
}
