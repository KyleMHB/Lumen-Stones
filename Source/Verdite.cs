using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_Verdite : CompProperties, ITunableProps
{
    public float radius = 8f;
    public float growthBonus, workBonus, yieldBonus;
    public CompProperties_Verdite() => compClass = typeof(CompVerdite);

    public void DeclareTunables(TunableScope s, Def owner)
    {
        string name = owner.defName;
        s.Add(name + ".radius", "Lumen_Verdite_SupportRadius".Translate(), radius, v => radius = v, Limit.Number(max: 100));
        s.Add(name + ".growth", "Lumen_Verdite_GrowthBonus".Translate(), growthBonus, v => growthBonus = v, Limit.Percent(10));
        s.Add(name + ".work", "Lumen_Verdite_WorkBonus".Translate(), workBonus, v => workBonus = v, Limit.Percent(10));
        s.Add(name + ".yield", "Lumen_Verdite_YieldBonus".Translate(), yieldBonus, v => yieldBonus = v, Limit.Percent(10));
        // The overlight comes before this comp in the def, but its rows follow these, so it is declared from here rather than as a comp of its own.
        ((ThingDef)owner).GetCompProperties<CompProperties_Overlight>()?.DeclareOverlight(s, owner);
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        string rule = "Lumen_Verdite_Rule".Translate();
        return new InfoStats()
            .Add("Lumen_Verdite_FarmingRadius".Translate(), InfoStats.Tiles(radius), rule)
            .Add(Effects.VerditeGrowth, "Lumen_Verdite_PlantGrowth".Translate(), InfoStats.Plus(growthBonus), rule)
            .Add(Effects.VerditeWorkSpeed, "Lumen_Verdite_PlantWorkSpeed".Translate(), InfoStats.Plus(workBonus), rule)
            .Add(Effects.VerditeHarvestYield, "Lumen_Verdite_PlantHarvestYield".Translate(), InfoStats.Plus(yieldBonus), rule)
            .Rows;
    }
}

public class VerditeField : MapComponent
{
    public readonly List<CompVerdite> fixtures = new();
    public VerditeField(Map map) : base(map) { }
}

public class CompVerdite : ThingComp
{
    public CompProperties_Verdite Props => (CompProperties_Verdite)props;

    public override void PostSpawnSetup(bool respawningAfterLoad) => MapState.Of<VerditeField>(parent.Map).fixtures.Add(this);
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) => MapState.Of<VerditeField>(map).fixtures.Remove(this);

}

public static class VerditeSupport
{
    public static float FixtureBonus(Map map, IntVec3 position, bool growth, bool harvest = false)
    {
        if (map == null) return 0;
        if (!(growth ? Effects.VerditeGrowth : harvest ? Effects.VerditeHarvestYield : Effects.VerditeWorkSpeed).On) return 0;
        float strongest = 0;
        foreach (var fixture in MapState.Of<VerditeField>(map).fixtures)
        {
            if (!fixture.parent.Spawned || fixture.parent.Position.DistanceToSquared(position) > fixture.Props.radius * fixture.Props.radius) continue;
            strongest = Mathf.Max(strongest, growth ? fixture.Props.growthBonus : harvest ? fixture.Props.yieldBonus : fixture.Props.workBonus);
        }
        return strongest;
    }

    public static float FarmerBonus(Pawn pawn, bool harvest)
    {
        if (pawn.Dead) return 0;
        float fixture = pawn.Spawned ? FixtureBonus(pawn.Map, pawn.Position, false, harvest) : 0;
        return Mathf.Max(fixture, Effects.VerditeScytheFarming.On && VerditeScythe.Equipped(pawn) ? VerditeScythe.FarmingBonus : 0);
    }

    public static void DrawRanges(ThingDef def, IntVec3 center, Thing thing = null)
    {
        GenDraw.DrawRadiusRing(center, def.GetCompProperties<CompProperties_Verdite>().radius, new Color(0.35f, 0.9f, 0.4f));
        var overlight = def.GetCompProperties<CompProperties_Overlight>();
        if (overlight != null && !CompLumenDarklight.On(thing)) GenDraw.DrawRadiusRing(center, overlight.overlightRadius, new Color(1f, 0.85f, 0.25f));
    }
}

public class PlaceWorker_VerditeRanges : PlaceWorker
{
    public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null) => VerditeSupport.DrawRanges(def, center, thing);
}

public class StatPart_Verdite : StatPart
{
    public bool harvest;
    public override void TransformValue(StatRequest req, ref float val)
    {
        if (req.Thing is Pawn pawn) val += VerditeSupport.FarmerBonus(pawn, harvest);
    }
    public override string ExplanationPart(StatRequest req)
    {
        float bonus = req.Thing is Pawn pawn ? VerditeSupport.FarmerBonus(pawn, harvest) : 0;
        return bonus > 0 ? "Lumen_Verdite_StrongestSupport".Translate(bonus.ToStringPercent()).ToString() : null;
    }
}

[HarmonyPatch(typeof(Plant), "GrowthRate", MethodType.Getter)]
public static class VerditeGrowth
{
    public static void Postfix(Plant __instance, ref float __result)
    {
        if (__instance.Spawned) __result *= 1 + VerditeSupport.FixtureBonus(__instance.Map, __instance.Position, true);
    }
}

[HarmonyPatch(typeof(Plant), "GrowthRateCalcDesc", MethodType.Getter)]
public static class VerditeGrowthDescription
{
    public static void Postfix(Plant __instance, ref string __result)
    {
        float bonus = __instance.Spawned ? VerditeSupport.FixtureBonus(__instance.Map, __instance.Position, true) : 0;
        if (bonus > 0) __result += "\n" + "Lumen_Verdite_StrongestGrowth".Translate(bonus.ToStringPercent());
    }
}
