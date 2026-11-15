using Verse;
using RimWorld;
using UnityEngine;
using System.Linq;

namespace LumenStones;

public static class HollowstoneRules
{
    public const int WardOffset = 20;
    public static float Radius = 8f;

    internal static void DeclareTunables(TunableScope s)
    {
        var ward = s.In("Lumen_Hollowstone_Section".Translate());
        ward.Add("Hollowstone.radius", "Lumen_Setting_FixtureRadius".Translate(), Radius, v => Radius = v, Limit.Number(1, 100));
        // The husk rows sit between the ward's, as they always did.
        HuskRules.DeclareTunables(s);
        ward.Add("Hollowstone.wardCostPerDay", "Lumen_Hollowstone_WardCost".Translate(), WardCostPerDay, v => WardCostPerDay = v);
    }
    // Warp per covered pawn per day.
    public static float WardCostPerDay = 1f;
    // The ward outlasts a 250-tick pass by 50 ticks, so it clears soon after a pawn leaves reach.
    public const int WardLingerTicks = 300;

    private static HediffDef ward;
    public static HediffDef Ward => ward ??= DefDatabase<HediffDef>.GetNamed("KM_HollowstoneWard");

    // The ward carries both pain relief and break steadiness. Each has its own switch, so the stage a pawn gets drops the
    // switched-off half. The def keeps its name, since saves hold it.
    private static readonly System.Collections.Generic.Dictionary<HediffStage, HediffStage[]> wardStages = new();
    private static int wardVersion = -1;

    public static HediffStage WardStage(HediffStage stage)
    {
        bool pain = Effects.HollowstonePain.On, breaks = Effects.HollowstoneBreaks.On;
        if (stage == null || pain && breaks) return stage;
        if (wardVersion != Tunables.Version) { wardStages.Clear(); wardVersion = Tunables.Version; }
        if (!wardStages.TryGetValue(stage, out var built)) wardStages[stage] = built = new HediffStage[4];
        int index = (pain ? 1 : 0) + (breaks ? 2 : 0);
        return built[index] ??= new HediffStage
        {
            label = stage.label, minSeverity = stage.minSeverity, becomeVisible = stage.becomeVisible,
            painFactor = pain ? stage.painFactor : 1f,
            statOffsets = breaks ? stage.statOffsets : stage.statOffsets?.Where(mod => mod.stat != StatDefOf.MentalBreakThreshold).ToList()
        };
    }

    // Husks feel no pain and aren't warded.
    public static bool IsHusk(Pawn pawn) => pawn.IsMutant && pawn.mutant.Def == HuskDef;
    // Null without Anomaly, where no pawn is a mutant anyway.
    private static MutantDef huskDef;
    private static bool huskLooked;
    private static MutantDef HuskDef
    {
        get
        {
            if (!huskLooked) { huskDef = DefDatabase<MutantDef>.GetNamedSilentFail("KM_WorkHusk"); huskLooked = true; }
            return huskDef;
        }
    }
}

// Pain suppression on the Warp pass: friendly pawns in a covered room pay Warp and carry the ward hediff for the strongest tier.
public class HollowstoneWard : MapComponent
{
    private static readonly int[] WardTiers = { 1, 2, 3 };
    // A column only dulls pain, so with pain relief switched off only clusters and pylons ward anyone.
    private static readonly int[] SteadyingTiers = { 2, 3 };
    private int lastVersion = -1;
    public HollowstoneWard(Map map) : base(map) { }

    public override void MapComponentTick()
    {
        if (Find.TickManager.TicksGame % WarpRules.PassInterval != HollowstoneRules.WardOffset) return;
        if (!Effects.HollowstonePain.On && !Effects.HollowstoneBreaks.On) return;
        var cache = DarkFixtureCache.For(map);
        if (!cache.AnyActive(DarkStoneKind.Hollowstone)) return;
        var warp = WarpEnergyNetwork.For(map);
        // A changed switch changes the stage, so pawns' cached pain and stats are refreshed once.
        bool refresh = lastVersion != Tunables.Version;
        lastVersion = Tunables.Version;
        // FreeColonistsSpawned also includes player-faction slaves in RimWorld 1.7.
        Pass(map.mapPawns.FreeColonistsSpawned.Concat(map.mapPawns.SlavesOfColonySpawned).Distinct(), cache, warp, refresh);
    }

    private static void Pass(System.Collections.Generic.IEnumerable<Pawn> pawns, DarkFixtureCache cache, WarpEnergyNetwork warp, bool refresh)
    {
        float cost = HollowstoneRules.WardCostPerDay * WarpRules.PassInterval / GenDate.TicksPerDay;
        var tiers = Effects.HollowstonePain.On ? WardTiers : SteadyingTiers;
        foreach (var pawn in pawns)
        {
            if (HollowstoneRules.IsHusk(pawn)) continue;
            int tier = cache.Cover(DarkStoneKind.Hollowstone, pawn, HollowstoneRules.Radius, false, tiers);
            if (tier == 0 || !warp.TryConsumeContinuous(cost)) continue;
            // Severity picks the stage: 0 column, 1 cluster, 2 pylon. Set exactly so a lost pylon drops to the next tier.
            var hediff = Hediff_Status.Apply(pawn, HollowstoneRules.Ward, null, null, HollowstoneRules.WardLingerTicks, tier - 1 + 0.5f);
            hediff.bracket = hediff.CurStage?.label;
            hediff.note = Hediff_Status.Describe(hediff.CurStage) + "\n" + "Lumen_Hollowstone_WardNote".Translate(HollowstoneRules.WardCostPerDay.ToString("0.##"));
            if (refresh) pawn.health.Notify_HediffChanged(hediff);
        }
    }
}
