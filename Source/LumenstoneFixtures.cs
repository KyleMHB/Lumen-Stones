using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace LumenStones;

public class CompProperties_LumenstoneFixture : CompProperties
{
    public int fixtureIndex;
    public float weight;
    public bool dark;
    public CompProperties_LumenstoneFixture() => compClass = typeof(CompLumenstoneFixture);

    // Ritual support only exists with Ideology. Without it these fixtures are lamps.
    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        if (!ModsConfig.IdeologyActive) yield break;
        string altar = dark ? "Sacrificial altar" : "Lumenstone altar";
        string rule = $"Fixtures in the same room as a {altar} and within reach raise its ritual's quality and yield. Support is full in an enclosed, roofed room and half outdoors. " +
            "Each fixture category counts once, at its highest weight, and floor and wall lamps share the lamp category.";
        foreach (var row in new InfoStats()
                     .Add("Ritual support weight", weight.ToStringPercent(), rule)
                     .Add("Ritual support reach", InfoStats.Tiles(RitualSupport.Radius(dark)), rule)
                     .Add("Ritual quality at full support", InfoStats.Plus(RitualSupport.QualityCap(dark)), rule)
                     .Add(dark ? "Conversion and harvest at full support" : "Ritual yield at full support", InfoStats.Plus(RitualSupport.YieldCap(dark)), rule)
                     .Rows)
            yield return row;
    }
}

public class CompLumenstoneFixture : ThingComp
{
    public CompProperties_LumenstoneFixture Props => (CompProperties_LumenstoneFixture)props;

    public override string CompInspectStringExtra()
    {
        if (!ModsConfig.IdeologyActive || !parent.Spawned) return null;
        var host = RitualSupport.NearestHost(parent, Props.dark);
        return host == null ? "No " + (Props.dark ? "Sacrificial" : "Lumenstone") + " altar in reach" : host.LabelCap + ": " + RitualSupport.Describe(host, Props.dark);
    }

    public override void CompTick()
    {
        if (!parent.Spawned || !parent.IsHashIntervalTick(300) || parent.Position.Fogged(parent.Map) || !CompAnimationToggle.On(parent)) return;
        var mote = FixtureMotes.Make(Props.dark ? "KM_Mote_DarkStonePulse" : "KM_Mote_LumenstonePulse", parent.Map);
        if (mote == null) return;
        mote.exactPosition = parent.DrawPos + parent.def.graphicData.drawOffset;
        mote.exactPosition.y = AltitudeLayer.MoteOverhead.AltitudeFor();
        mote.Scale = 0.08f;
        mote.instanceColor = Props.dark ? new Color(0.60f, 0.42f, 0.82f) : new Color(0.88f, 0.94f, 1f);
        mote.airTimeLeft = 1.8f;
        mote.SetVelocity(0, 0.12f);
        GenSpawn.Spawn(mote, parent.Position, parent.Map);
    }
}

public static class LumenstoneSupport
{
    public static float RitualQualityCap = 0.20f, RitualYieldCap = 0.40f, RitualSupportRadius = 8f;
}

public static class DarkStoneSupport
{
    public static float RitualQualityCap = 0.20f, RitualYieldCap = 0.40f, RitualSupportRadius = 8f;
}

// Implemented by the Lumenstone altar (Lumenstone fixtures) and the Sacrificial altar (Dark Stone fixtures).
public interface IRitualSupportHost
{
    bool Dark { get; }
}

public static class RitualSupport
{
    private static readonly Color LumenRing = new(0.82f, 0.90f, 0.98f), DarkRing = new(0.62f, 0.44f, 0.82f);

    public static float Radius(bool dark) => dark ? DarkStoneSupport.RitualSupportRadius : LumenstoneSupport.RitualSupportRadius;
    public static float QualityCap(bool dark) => dark ? DarkStoneSupport.RitualQualityCap : LumenstoneSupport.RitualQualityCap;
    public static float YieldCap(bool dark) => dark ? DarkStoneSupport.RitualYieldCap : LumenstoneSupport.RitualYieldCap;
    public static Color RingColor(bool dark) => dark ? DarkRing : LumenRing;
    public static bool Enclosed(Room room) => room != null && room.ProperRoom && room.OpenRoofCount == 0;

    // Fixtures must share the host's room. Enclosed roofed rooms give full weights and anything else half.
    public static float Fraction(Building host, bool dark)
    {
        if (host is not IRitualSupportHost || !host.Spawned) return 0;
        var room = host.GetRoom();
        if (room == null) return 0;
        float radius = Radius(dark);
        var center = host.TrueCenter();
        var weights = new float[4];
        // Padding covers the cells of a multi-cell host, whose position is not its centre.
        foreach (var cell in GenRadial.RadialCellsAround(host.Position, radius + 2f, true))
        {
            if (!cell.InBounds(host.Map)) continue;
            foreach (var thing in cell.GetThingList(host.Map))
            {
                var comp = thing.TryGetComp<CompLumenstoneFixture>();
                if (comp == null || comp.Props.dark != dark || thing.GetRoom() != room) continue;
                if ((center - thing.TrueCenter()).MagnitudeHorizontalSquared() > radius * radius) continue;
                weights[comp.Props.fixtureIndex] = Mathf.Max(weights[comp.Props.fixtureIndex], comp.Props.weight);
            }
        }
        float total = Mathf.Clamp01(weights.Sum());
        return Enclosed(room) ? total : total * 0.5f;
    }

    public static Building NearestHost(Thing fixture, bool dark)
    {
        float radius = Radius(dark);
        return fixture.Map.listerBuildings.allBuildingsColonist
            .Where(b => b is IRitualSupportHost host && host.Dark == dark &&
                (b.TrueCenter() - fixture.TrueCenter()).MagnitudeHorizontalSquared() <= radius * radius)
            .OrderBy(b => (b.TrueCenter() - fixture.TrueCenter()).MagnitudeHorizontalSquared()).FirstOrDefault();
    }

    public static string Describe(Building host, bool dark)
    {
        float support = Fraction(host, dark);
        string where = Enclosed(host.GetRoom()) ? "enclosed, roofed room" : "outdoors or unroofed, half strength";
        return "Ritual support " + support.ToStringPercent() + " (" + where + "): quality +" +
            (support * QualityCap(dark)).ToStringPercent() + ", yield +" + (support * YieldCap(dark)).ToStringPercent();
    }
}

public abstract class PlaceWorker_SupportRing : PlaceWorker
{
    protected abstract bool Dark { get; }

    public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
    {
        if (ModsConfig.IdeologyActive) GenDraw.DrawRadiusRing(center, RitualSupport.Radius(Dark), RitualSupport.RingColor(Dark));
    }
}

public class PlaceWorker_LumenstoneSupportRing : PlaceWorker_SupportRing { protected override bool Dark => false; }
public class PlaceWorker_DarkStoneSupportRing : PlaceWorker_SupportRing { protected override bool Dark => true; }

// Adds the nearby fixtures' support to the native ritual dialog and to the final quality roll.
public class RitualOutcomeComp_FixtureSupport : RitualOutcomeComp_Quality
{
    public bool dark;

    private float Support(TargetInfo target) => target.Thing is Building host ? RitualSupport.Fraction(host, dark) : 0;
    public override float Count(LordJob_Ritual ritual, RitualOutcomeComp_Data data) => Support(ritual.selectedTarget);
    public override float QualityOffset(LordJob_Ritual ritual, RitualOutcomeComp_Data data) => Support(ritual.selectedTarget) * RitualSupport.QualityCap(dark);

    // Vanilla's summary prints the maximum for comps that don't say otherwise. Show what this ceremony actually got.
    public override string GetDesc(LordJob_Ritual ritual = null, RitualOutcomeComp_Data data = null) =>
        ritual == null ? base.GetDesc(ritual, data) : label + ": +" + QualityOffset(ritual, data).ToStringPercent() + " quality";

    public override QualityFactor GetQualityFactor(Precept_Ritual ritual, TargetInfo ritualTarget, RitualObligation obligation,
        RitualRoleAssignments assignments, RitualOutcomeComp_Data data)
    {
        float support = Support(ritualTarget);
        float offset = support * RitualSupport.QualityCap(dark);
        return new QualityFactor
        {
            label = label,
            count = support.ToStringPercent() + " support",
            qualityChange = "+" + offset.ToStringPercent(),
            positive = true,
            quality = offset,
            priority = 3f,
            toolTip = "Fixtures of the matching stone in this room within " + RitualSupport.Radius(dark) + " tiles. Full strength in an enclosed, roofed room and half outdoors."
        };
    }
}
