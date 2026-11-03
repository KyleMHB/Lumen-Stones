using Verse;
using RimWorld;
using UnityEngine;

namespace LumenStones;

public static class HollowstoneRules
{
    public const int WardOffset = 20;
    public static float Radius = 8f;
    // Warp per covered pawn per day.
    public static float WardCostPerDay = 3f;
    // The ward outlasts a 250-tick pass by 50 ticks, so it clears soon after a pawn leaves reach.
    public const int WardLingerTicks = 300;

    private static HediffDef ward;
    public static HediffDef Ward => ward ??= DefDatabase<HediffDef>.GetNamed("KM_HollowstoneWard");

    // Husks feel no pain and aren't warded.
    public static bool IsHusk(Pawn pawn) => pawn.IsMutant && pawn.mutant.Def.defName == "KM_WorkHusk";
}

// Pain suppression on the Warp pass: friendly pawns in a covered room pay Warp and carry the ward hediff for the strongest tier.
public class HollowstoneWard : MapComponent
{
    private static readonly int[] WardTiers = { 1, 2, 3 };
    public HollowstoneWard(Map map) : base(map) { }

    public override void MapComponentTick()
    {
        if (Find.TickManager.TicksGame % WarpRules.PassInterval != HollowstoneRules.WardOffset) return;
        var cache = DarkFixtureCache.For(map);
        if (!cache.AnyActive(DarkStoneKind.Hollowstone)) return;
        var warp = WarpEnergyNetwork.For(map);
        Pass(map.mapPawns.FreeColonistsSpawned, cache, warp);
        Pass(map.mapPawns.SlavesOfColonySpawned, cache, warp);
    }

    private static void Pass(System.Collections.Generic.List<Pawn> pawns, DarkFixtureCache cache, WarpEnergyNetwork warp)
    {
        float cost = HollowstoneRules.WardCostPerDay * WarpRules.PassInterval / GenDate.TicksPerDay;
        foreach (var pawn in pawns)
        {
            if (HollowstoneRules.IsHusk(pawn)) continue;
            int tier = cache.Cover(DarkStoneKind.Hollowstone, pawn, HollowstoneRules.Radius, false, WardTiers);
            if (tier == 0 || !warp.TryConsumeContinuous(cost)) continue;
            // Severity picks the stage: 0 column, 1 cluster, 2 pylon. Set exactly so a lost pylon drops to the next tier.
            var hediff = Hediff_Status.Apply(pawn, HollowstoneRules.Ward, null, null, HollowstoneRules.WardLingerTicks, tier - 1 + 0.5f);
            hediff.bracket = hediff.CurStage?.label;
            hediff.note = Hediff_Status.Describe(hediff.CurStage) + "\nCosts " + HollowstoneRules.WardCostPerDay.ToString("0.##") + " Warp energy a day while covered.";
        }
    }
}
