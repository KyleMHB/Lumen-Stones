using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace LumenStones;

public class CompProperties_LumenstoneFixture : CompProperties_Fixture, ITunableProps
{
    public bool dark;
    public CompProperties_LumenstoneFixture() => compClass = typeof(CompLumenstoneFixture);

    public void DeclareTunables(TunableScope s, Def owner) =>
        s.Add(owner.defName + ".support", "Category ritual support contribution (%)", weight, v => weight = v, Limit.Percent());

    // Ritual support only exists with Ideology. Without it these fixtures are lamps.
    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        if (!ModsConfig.IdeologyActive) yield break;
        var stone = SupportStone.Ritual(dark);
        string altar = dark ? "Sacrificial altar" : "Lumenstone altar";
        string rule = $"Fixtures in the same room as a {altar} and within reach raise its ritual's quality and yield. Support is full in an enclosed, roofed room and half outdoors. " +
            "Each fixture category counts once, at its highest weight, and floor and wall lamps share the lamp category.";
        foreach (var row in new InfoStats()
                     .Add("Ritual support weight", weight.ToStringPercent(), rule)
                     .Add("Ritual support reach", InfoStats.Tiles(stone.Radius), rule)
                     .Add("Ritual quality at full support", InfoStats.Plus(stone.Quality.Cap), rule)
                     .Add(dark ? "Conversion and harvest at full support" : "Ritual yield at full support", InfoStats.Plus(stone.Yield.Cap), rule)
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

// Implemented by the Lumenstone altar (Lumenstone fixtures) and the Sacrificial altar (Dark Stone fixtures).
public interface IRitualSupportHost
{
    bool Dark { get; }
}

public static class RitualSupport
{
    private static readonly Color LumenRing = new(0.82f, 0.90f, 0.98f), DarkRing = new(0.62f, 0.44f, 0.82f);

    public static Color RingColor(bool dark) => dark ? DarkRing : LumenRing;

    public static Building NearestHost(Thing fixture, bool dark)
    {
        float radius = SupportStone.Ritual(dark).Radius;
        return fixture.Map.listerBuildings.allBuildingsColonist
            .Where(b => b is IRitualSupportHost host && host.Dark == dark &&
                (b.TrueCenter() - fixture.TrueCenter()).MagnitudeHorizontalSquared() <= radius * radius)
            .OrderBy(b => (b.TrueCenter() - fixture.TrueCenter()).MagnitudeHorizontalSquared()).FirstOrDefault();
    }

    public static string Describe(Building host, bool dark)
    {
        var stone = SupportStone.Ritual(dark);
        var support = FixtureSupport.Of(stone, host);
        string where = FixtureSupport.Enclosed(host.GetRoom()) ? "enclosed, roofed room" : "outdoors or unroofed, half strength";
        return "Ritual support " + support.Passive.ToStringPercent() + " (" + where + "): quality +" +
            support.Bonus(stone.Quality).ToStringPercent() + ", yield +" + support.Bonus(stone.Yield).ToStringPercent();
    }
}

public abstract class PlaceWorker_SupportRing : PlaceWorker
{
    protected abstract bool Dark { get; }

    public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
    {
        if (ModsConfig.IdeologyActive) GenDraw.DrawRadiusRing(center, SupportStone.Ritual(Dark).Radius, RitualSupport.RingColor(Dark));
    }
}

public class PlaceWorker_LumenstoneSupportRing : PlaceWorker_SupportRing { protected override bool Dark => false; }
public class PlaceWorker_DarkStoneSupportRing : PlaceWorker_SupportRing { protected override bool Dark => true; }

// Adds the nearby fixtures' support to the native ritual dialog and to the final quality roll.
public class RitualOutcomeComp_FixtureSupport : RitualOutcomeComp_Quality
{
    public bool dark;

    private SupportReading Reading(TargetInfo target) => FixtureSupport.Of(SupportStone.Ritual(dark), target.Thing);
    private float Support(TargetInfo target) => Reading(target).Passive;
    public override float Count(LordJob_Ritual ritual, RitualOutcomeComp_Data data) => Support(ritual.selectedTarget);
    public override float QualityOffset(LordJob_Ritual ritual, RitualOutcomeComp_Data data) => Reading(ritual.selectedTarget).Bonus(SupportStone.Ritual(dark).Quality);

    // Vanilla's summary prints the maximum for comps that don't say otherwise. Show what this ceremony actually got.
    public override string GetDesc(LordJob_Ritual ritual = null, RitualOutcomeComp_Data data = null) =>
        ritual == null ? base.GetDesc(ritual, data) : label + ": +" + QualityOffset(ritual, data).ToStringPercent() + " quality";

    public override QualityFactor GetQualityFactor(Precept_Ritual ritual, TargetInfo ritualTarget, RitualObligation obligation,
        RitualRoleAssignments assignments, RitualOutcomeComp_Data data)
    {
        var reading = Reading(ritualTarget);
        float support = reading.Passive;
        float offset = reading.Bonus(SupportStone.Ritual(dark).Quality);
        return new QualityFactor
        {
            label = label,
            count = support.ToStringPercent() + " support",
            qualityChange = "+" + offset.ToStringPercent(),
            positive = true,
            quality = offset,
            priority = 3f,
            toolTip = "Fixtures of the matching stone in this room within " + SupportStone.Ritual(dark).Radius + " tiles. Full strength in an enclosed, roofed room and half outdoors."
        };
    }
}
