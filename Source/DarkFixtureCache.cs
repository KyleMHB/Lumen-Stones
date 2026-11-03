using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LumenStones;

public enum DarkStoneKind { Sanguinite, Dreadstone, Hollowstone }

public class CompProperties_DarkFixture : CompProperties
{
    public DarkStoneKind stone;
    // 0 is light only (lamps), then 1 column, 2 cluster, 3 pylon.
    public int tier;
    public CompProperties_DarkFixture() => compClass = typeof(CompDarkFixture);

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        if (tier == 0) yield break;
        string warp = " This is a Warp active effect: it runs only while the fixture's Warp active effect is toggled on and the map has Warp energy.";
        var rows = new InfoStats();
        switch (stone)
        {
            case DarkStoneKind.Sanguinite:
            {
                string rule = "Treats colonists, slaves and prisoners resting in a bed in the same room. Only the strongest Sanguinite fixture in reach treats a patient, and bionics are never touched." + warp;
                rows.Add("Treatment reach", InfoStats.Tiles(SanguiniteRules.Radius), rule)
                    .Add("Bleeding rate", SanguiniteRules.BleedFactor(tier).ToStringPercent("0"), "Wounds stay untended and keep bleeding, only slower. " + rule)
                    .Add("Warp energy per bleeding patient", SanguiniteRules.BleedCost.ToString("0.##") + " every " + InfoStats.Seconds(WarpRules.PassInterval), rule)
                    .Add("Heals scars", tier == 1 ? "surface scars" : "every scar", $"One scar per pass, for {SanguiniteRules.ScarCost:0.##} Warp energy each. " + rule)
                    .Add("Regrows", tier switch { 1 => "fingers and toes", 2 => "small parts", _ => "small parts and organs" },
                        $"Regrowth is paid gradually and pauses when the patient leaves bed or Warp energy runs out. Fingers and toes take {SanguiniteRules.DigitDays:0.#} day and {SanguiniteRules.DigitCost:0.##} Warp energy; " +
                        $"eyes, ears, noses and jaws {SanguiniteRules.SmallPartDays:0.#} days and {SanguiniteRules.SmallPartCost:0.##}; " +
                        $"internal organs {SanguiniteRules.OrganDays:0.#} days and {SanguiniteRules.OrganCost:0.##}. " + rule);
                break;
            }
            case DarkStoneKind.Dreadstone:
            {
                string reach = "Only the strongest covering Dreadstone fixture counts." + warp;
                rows.Add("Reach", InfoStats.Tiles(DreadstoneRules.Radius), reach);
                if (tier != 2)
                {
                    float scale = tier == 3 ? DreadstoneRules.PylonErosionFactor : 1f;
                    string subjugation = "Applies to the colony's prisoners and slaves in the same room. " + reach;
                    rows.Add("Resistance lost per day", (DreadstoneRules.ResistancePerDay * scale).ToString("0.##"), subjugation)
                        .Add("Will lost per day", (DreadstoneRules.WillPerDay * scale).ToString("0.##"), subjugation)
                        .Add("Slave suppression per day", InfoStats.Plus(DreadstoneRules.SuppressionPerDay * scale), subjugation)
                        .Add("Prison breaks and rebellions", DreadstoneRules.BreakMtbFactor.ToString("0.#") + "x rarer", subjugation)
                        .Add("Warp energy per pawn per day", DreadstoneRules.SubjugationCost.ToString("0.##"), subjugation);
                }
                if (tier >= 2)
                {
                    string dread = "Applies to hostile humanlikes in line of sight. Mechanoids and animals are immune. Nothing is spent with no hostiles in reach." +
                        (tier == 3 ? $" When a weakened enemy dies, {DreadstoneRules.SiphonFraction.ToStringPercent()} of its market value returns as Warp energy." : "") + " " + reach;
                    var stage = StageAt(DreadstoneRules.Dread, tier == 3 ? DreadstoneRules.PylonSeverity : DreadstoneRules.ClusterSeverity);
                    if (stage != null) rows.Add("Dread on hostiles", InfoStats.Stage(stage), dread);
                    rows.Add("Warp energy per hostile", DreadstoneRules.DreadCostPerPass.ToString("0.##") + " every " + InfoStats.Seconds(WarpRules.PassInterval), dread);
                }
                break;
            }
            case DarkStoneKind.Hollowstone:
            {
                string rule = "Applies to colonists and slaves in the same room. Work husks are not affected. Only the strongest Hollowstone fixture in reach counts." + warp;
                rows.Add("Reach", InfoStats.Tiles(HollowstoneRules.Radius), rule);
                if (HollowstoneRules.Ward.stages is { } stages && tier - 1 < stages.Count) rows.Add("Ward", InfoStats.Stage(stages[tier - 1]), rule);
                rows.Add("Warp energy per pawn per day", HollowstoneRules.WardCostPerDay.ToString("0.##"), rule);
                break;
            }
        }
        foreach (var row in rows.Rows) yield return row;
    }

    private static HediffStage StageAt(HediffDef def, float severity) =>
        def.stages?.LastOrDefault(stage => stage.minSeverity <= severity);
}

// Marks a Sanguinite, Dreadstone or Hollowstone fixture. These give no Dark Stone ritual support, so they are
// separate from CompLumenstoneFixture, but they still keep a room from being a Lumenstone shrine.
public class CompDarkFixture : ThingComp
{
    public CompProperties_DarkFixture Props => (CompProperties_DarkFixture)props;
    private CompWarpToggle toggle;

    // Lamps and anything without a toggle are never active. Read by the cache's refresh, not by hot paths.
    public bool Active => Props.tier > 0 && (toggle ??= parent.GetComp<CompWarpToggle>()) is { Active: true };

    public override void PostSpawnSetup(bool respawningAfterLoad) => DarkFixtureCache.For(parent.Map).Register(this);
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) => DarkFixtureCache.For(map).Unregister(this);


    public override void ReceiveCompSignal(string signal)
    {
        if (signal == WarpRules.ToggledSignal && parent.Spawned) DarkFixtureCache.For(parent.Map).Refresh();
    }
}

// Registered dark fixtures by stone and tier, and the strongest active tier per stone. Lookups are plain reads. The
// cache refreshes on the Warp pass, on a toggle, and when a fixture is added or removed.
public class DarkFixtureCache : MapComponent
{
    public const int Tiers = 4;
    private static readonly int Stones = Enum.GetValues(typeof(DarkStoneKind)).Length;
    private readonly List<CompDarkFixture>[,] fixtures;
    private readonly int[] strongestActive;
    private int count;

    public DarkFixtureCache(Map map) : base(map)
    {
        fixtures = new List<CompDarkFixture>[Stones, Tiers];
        for (int s = 0; s < Stones; s++)
            for (int t = 0; t < Tiers; t++) fixtures[s, t] = new List<CompDarkFixture>();
        strongestActive = new int[Stones];
    }

    public static DarkFixtureCache For(Map map) => map.GetComponent<DarkFixtureCache>();

    public List<CompDarkFixture> Fixtures(DarkStoneKind stone, int tier) => fixtures[(int)stone, tier];

    // 0 when no fixture of that stone is active, otherwise the highest active tier. Tiers don't stack.
    public int StrongestActiveTier(DarkStoneKind stone) => strongestActive[(int)stone];
    public bool AnyActive(DarkStoneKind stone) => strongestActive[(int)stone] > 0;

    public bool Any(DarkStoneKind stone)
    {
        for (int t = 0; t < Tiers; t++)
            if (fixtures[(int)stone, t].Count > 0) return true;
        return false;
    }

    // The strongest active fixture of the given tiers (ascending) that covers the pawn within reach, or 0.
    // Without line of sight a fixture must share the pawn's room. With it, the fixture only has to see the pawn.
    public int Cover(DarkStoneKind stone, Pawn pawn, float radius, bool lineOfSight, int[] tiers)
    {
        float range = radius * radius;
        Room room = lineOfSight ? null : pawn.GetRoom();
        if (!lineOfSight && room == null) return 0;
        for (int i = tiers.Length - 1; i >= 0; i--)
            foreach (var fixture in Fixtures(stone, tiers[i]))
            {
                if (!fixture.Active || (fixture.parent.Position - pawn.Position).LengthHorizontalSquared > range) continue;
                if (lineOfSight ? GenSight.LineOfSight(fixture.parent.Position, pawn.Position, pawn.Map) : fixture.parent.GetRoom() == room)
                    return tiers[i];
            }
        return 0;
    }

    public void Register(CompDarkFixture comp)
    {
        var list = fixtures[(int)comp.Props.stone, comp.Props.tier];
        if (list.Contains(comp)) return;
        list.Add(comp);
        count++;
        Refresh();
    }

    public void Unregister(CompDarkFixture comp)
    {
        if (!fixtures[(int)comp.Props.stone, comp.Props.tier].Remove(comp)) return;
        count--;
        Refresh();
    }

    public override void MapComponentTick()
    {
        if (count > 0 && Find.TickManager.TicksGame % WarpRules.PassInterval == 0) Refresh();
    }

    public void Refresh()
    {
        for (int s = 0; s < Stones; s++)
        {
            int best = 0;
            for (int t = Tiers - 1; t > 0 && best == 0; t--)
                foreach (var comp in fixtures[s, t])
                    if (comp.Active) { best = t; break; }
            strongestActive[s] = best;
        }
    }
}
