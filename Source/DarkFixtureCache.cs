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
        string warp = " " + "Lumen_Info_WarpRule".Translate();
        var rows = new InfoStats();
        switch (stone)
        {
            case DarkStoneKind.Sanguinite:
            {
                string rule = "Lumen_DarkInfo_SanguiniteRule".Translate() + warp;
                rows.Add("Lumen_DarkInfo_TreatmentReach".Translate(), InfoStats.Tiles(SanguiniteRules.Radius), rule)
                    .Add(Effects.SanguiniteBleeding, "Lumen_DarkInfo_BleedingRate".Translate(), SanguiniteRules.BleedFactor(tier).ToStringPercent("0"), "Lumen_DarkInfo_BleedingRateDesc".Translate() + " " + rule)
                    .Add(Effects.SanguiniteBleeding, "Lumen_DarkInfo_BleedCost".Translate(), "Lumen_DarkInfo_Every".Translate(SanguiniteRules.BleedCost.ToString("0.##"), InfoStats.Seconds(WarpRules.PassInterval)), rule)
                    .Add(Effects.SanguiniteScars, "Lumen_DarkInfo_HealsScars".Translate(), (tier == 1 ? "Lumen_DarkInfo_SurfaceScars" : "Lumen_DarkInfo_EveryScar").Translate(),
                        "Lumen_DarkInfo_ScarDesc".Translate(SanguiniteRules.ScarCost.ToString("0.##")) + " " + rule)
                    .Add(Effects.SanguiniteDigits.On || Effects.SanguiniteSmallParts.On || Effects.SanguiniteOrgans.On ? null : Effects.SanguiniteDigits,
                        "Lumen_DarkInfo_Regrows".Translate(), (tier switch { 1 => "Lumen_DarkInfo_RegrowDigits", 2 => "Lumen_Sanguinite_SmallParts", _ => "Lumen_DarkInfo_RegrowAll" }).Translate(),
                        "Lumen_DarkInfo_RegrowDesc".Translate(SanguiniteRules.DigitDays.ToString("0.#"), SanguiniteRules.DigitCost.ToString("0.##"),
                            SanguiniteRules.SmallPartDays.ToString("0.#"), SanguiniteRules.SmallPartCost.ToString("0.##"),
                            SanguiniteRules.OrganDays.ToString("0.#"), SanguiniteRules.OrganCost.ToString("0.##")) + " " + rule);
                break;
            }
            case DarkStoneKind.Dreadstone:
            {
                string reach = "Lumen_DarkInfo_DreadstoneReach".Translate() + warp;
                rows.Add("Lumen_DarkInfo_Reach".Translate(), InfoStats.Tiles(DreadstoneRules.Radius), reach);
                if (tier != 2)
                {
                    float scale = tier == 3 ? DreadstoneRules.PylonErosionFactor : 1f;
                    string subjugation = "Lumen_DarkInfo_SubjugationRule".Translate() + " " + reach;
                    var on = Effects.DreadstoneSubjugation;
                    rows.Add(on, "Lumen_DarkInfo_Resistance".Translate(), (DreadstoneRules.ResistancePerDay * scale).ToString("0.##"), subjugation)
                        .Add(on, "Lumen_DarkInfo_Will".Translate(), (DreadstoneRules.WillPerDay * scale).ToString("0.##"), subjugation)
                        .Add(on, "Lumen_DarkInfo_Suppression".Translate(), InfoStats.Plus(DreadstoneRules.SuppressionPerDay * scale), subjugation)
                        .Add(on, "Lumen_DarkInfo_Breaks".Translate(), "Lumen_DarkInfo_Rarer".Translate(DreadstoneRules.BreakMtbFactor.ToString("0.#")), subjugation)
                        .Add(on, "Lumen_DarkInfo_PerPawnPerDay".Translate(), DreadstoneRules.SubjugationCost.ToString("0.##"), subjugation);
                }
                if (tier >= 2)
                {
                    string dread = "Lumen_DarkInfo_DreadRule".Translate() +
                        (tier == 3 ? " " + "Lumen_DarkInfo_SiphonRule".Translate(DreadstoneRules.SiphonFraction.ToStringPercent()).ToString() : "") + " " + reach;
                    var stage = StageAt(DreadstoneRules.Dread, tier == 3 ? DreadstoneRules.PylonSeverity : DreadstoneRules.ClusterSeverity);
                    if (stage != null) rows.Add(Effects.DreadstoneDread, "Lumen_DarkInfo_DreadOnHostiles".Translate(), InfoStats.Stage(stage), dread);
                    rows.Add(Effects.DreadstoneDread, "Lumen_DarkInfo_PerHostile".Translate(), "Lumen_DarkInfo_Every".Translate(DreadstoneRules.DreadCostPerPass.ToString("0.##"), InfoStats.Seconds(WarpRules.PassInterval)), dread);
                }
                break;
            }
            case DarkStoneKind.Hollowstone:
            {
                string rule = "Lumen_DarkInfo_HollowstoneRule".Translate() + warp;
                rows.Add("Lumen_DarkInfo_Reach".Translate(), InfoStats.Tiles(HollowstoneRules.Radius), rule);
                if (HollowstoneRules.Ward.stages is { } stages && tier - 1 < stages.Count)
                {
                    var ward = HollowstoneRules.WardStage(stages[tier - 1]);
                    string text = InfoStats.Stage(ward);
                    rows.Add("Lumen_DarkInfo_Ward".Translate(), text.NullOrEmpty() ? EffectSwitch.OffText : text, rule);
                }
                rows.Add("Lumen_DarkInfo_PerPawnPerDay".Translate(), HollowstoneRules.WardCostPerDay.ToString("0.##"), rule);
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

    public static DarkFixtureCache For(Map map) => MapState.Of<DarkFixtureCache>(map);

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
