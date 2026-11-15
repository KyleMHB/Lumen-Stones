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
        s.Add(owner.defName + ".support", "Lumen_Fixtures_CategoryContribution".Translate(), weight, v => weight = v, Limit.Percent());

    // Ritual support only exists with Ideology. Without it these fixtures are lamps.
    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        if (!ModsConfig.IdeologyActive) yield break;
        var stone = SupportStone.Ritual(dark);
        string rule = (dark ? "Lumen_Fixtures_RuleDark" : "Lumen_Fixtures_RuleLight").Translate();
        foreach (var row in new InfoStats()
                     .Add("Lumen_Fixtures_Weight".Translate(), weight.ToStringPercent(), rule)
                     .Add("Lumen_Fixtures_Reach".Translate(), InfoStats.Tiles(stone.Radius), rule)
                     .Add(stone.Quality.Switch, "Lumen_Fixtures_Quality".Translate(), InfoStats.Plus(stone.Quality.Cap), rule)
                     .Add(stone.Yield.Switch, (dark ? "Lumen_Fixtures_YieldDark" : "Lumen_Fixtures_YieldLight").Translate(), InfoStats.Plus(stone.Yield.Cap), rule)
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
        return host == null ? (Props.dark ? "Lumen_Fixtures_NoAltarDark" : "Lumen_Fixtures_NoAltarLight").Translate().ToString()
            : host.LabelCap + ": " + RitualSupport.Describe(host, Props.dark);
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
        string where = (FixtureSupport.Enclosed(host.GetRoom()) ? "Lumen_Fixtures_Enclosed" : "Lumen_Fixtures_Outdoors").Translate();
        return "Lumen_Fixtures_Describe".Translate(support.Passive.ToStringPercent(), where,
            support.Bonus(stone.Quality).ToStringPercent(), support.Bonus(stone.Yield).ToStringPercent());
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
        ritual == null ? base.GetDesc(ritual, data) : "Lumen_Ritual_QualityDesc".Translate(label, QualityOffset(ritual, data).ToStringPercent()).ToString();

    public override QualityFactor GetQualityFactor(Precept_Ritual ritual, TargetInfo ritualTarget, RitualObligation obligation,
        RitualRoleAssignments assignments, RitualOutcomeComp_Data data)
    {
        var reading = Reading(ritualTarget);
        float support = reading.Passive;
        float offset = reading.Bonus(SupportStone.Ritual(dark).Quality);
        return new QualityFactor
        {
            label = label,
            count = "Lumen_Fixtures_SupportCount".Translate(support.ToStringPercent()),
            qualityChange = "+" + offset.ToStringPercent(),
            positive = true,
            quality = offset,
            priority = 3f,
            toolTip = "Lumen_Fixtures_Tooltip".Translate(SupportStone.Ritual(dark).Radius)
        };
    }
}
