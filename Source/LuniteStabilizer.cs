using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_LuniteStabilizer : CompProperties
{
    public CompProperties_LuniteStabilizer() => compClass = typeof(CompLuniteStabilizer);

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        if (!SolarRules.Enabled) return new List<StatDrawEntry>();
        return new InfoStats()
            .Add(Effects.LuniteStabilizer, "Lumen_LuniteStabilizer_Stabilizes".Translate(), "Lumen_LuniteStabilizer_StabilizesValue".Translate(), "Lumen_LuniteStabilizer_StabilizesDesc".Translate())
            .Add("Lumen_LuniteStabilizer_LinkRadius".Translate(), InfoStats.Tiles(LuniteRules.LinkRadius), "Lumen_LuniteStabilizer_LinkRadiusDesc".Translate())
            .Rows;
    }
}

public class CompLuniteStabilizer : ThingComp
{
    public override void PostSpawnSetup(bool respawningAfterLoad) => LuniteLinkTracker.For(parent.Map).Register(parent, true);
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) => LuniteLinkTracker.For(map).Register(parent, false);

    public override string CompInspectStringExtra()
    {
        if (!parent.Spawned) return null;
        if (!Effects.LuniteStabilizer.On) return "Lumen_LuniteStabilizer_Stabilizing".Translate(EffectSwitch.OffText.ToLower());
        var fixture = LuniteLinkTracker.For(parent.Map).FixtureOf(parent);
        return "Lumen_LuniteStabilizer_Stabilizing".Translate(fixture?.LabelCap ?? "Lumen_LuniteStabilizer_NothingInRange".Translate());
    }

    public override void PostDrawExtraSelectionOverlays()
    {
        GenDraw.DrawRadiusRing(parent.Position, LuniteRules.LinkRadius, LuniteRules.Ring);
        if (parent.Spawned && LuniteLinkTracker.For(parent.Map).FixtureOf(parent) is { } fixture)
            GenDraw.DrawLineBetween(parent.TrueCenter(), fixture.TrueCenter(), SimpleColor.White);
    }
}

public class PlaceWorker_LuniteRing : PlaceWorker
{
    public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null) =>
        GenDraw.DrawRadiusRing(center, LuniteRules.LinkRadius, LuniteRules.Ring);
}

// Pairs stabilizers with Solar and Warp toggle fixtures, one to one. Rebuilt lazily after any spawn or despawn.
public class LuniteLinkTracker : MapComponent
{
    private readonly HashSet<Thing> stabilizers = new(), fixtures = new();
    private readonly Dictionary<Thing, Thing> fixtureOf = new(), stabilizerOf = new();
    private bool dirty = true;

    public LuniteLinkTracker(Map map) : base(map) { }

    public static LuniteLinkTracker For(Map map) => MapState.Of<LuniteLinkTracker>(map);

    public void Register(Thing stabilizer, bool add) { if (add ? stabilizers.Add(stabilizer) : stabilizers.Remove(stabilizer)) dirty = true; }
    public void RegisterFixture(Thing fixture, bool add) { if (add ? fixtures.Add(fixture) : fixtures.Remove(fixture)) dirty = true; }

    public Thing FixtureOf(Thing stabilizer) { Rebuild(); return fixtureOf.GetValueOrDefault(stabilizer); }
    public Thing StabilizerOf(Thing fixture) { Rebuild(); return stabilizerOf.GetValueOrDefault(fixture); }

    public static bool IsStabilized(Thing fixture) => Effects.LuniteStabilizer.On && fixture.Map != null && For(fixture.Map).StabilizerOf(fixture) != null;

    private void Rebuild()
    {
        if (!dirty) return;
        dirty = false;
        fixtureOf.Clear();
        stabilizerOf.Clear();
        float limit = LuniteRules.LinkRadius * LuniteRules.LinkRadius;
        var pairs = new List<(int distance, Thing stabilizer, Thing fixture)>();
        foreach (var stabilizer in stabilizers)
            foreach (var fixture in fixtures)
            {
                int nearest = int.MaxValue;
                foreach (var cell in fixture.OccupiedRect()) nearest = Mathf.Min(nearest, stabilizer.Position.DistanceToSquared(cell));
                if (nearest <= limit) pairs.Add((nearest, stabilizer, fixture));
            }
        foreach (var (_, stabilizer, fixture) in pairs.OrderBy(p => p.distance).ThenBy(p => p.stabilizer.thingIDNumber).ThenBy(p => p.fixture.thingIDNumber))
        {
            if (fixtureOf.ContainsKey(stabilizer) || stabilizerOf.ContainsKey(fixture)) continue;
            fixtureOf[stabilizer] = fixture;
            stabilizerOf[fixture] = stabilizer;
        }
    }
}
