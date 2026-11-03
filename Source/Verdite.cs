using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_Verdite : CompProperties
{
    public float radius = 8f;
    public float growthBonus, workBonus, yieldBonus;
    public CompProperties_Verdite() => compClass = typeof(CompVerdite);

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        string rule = "Applies to plants and farmers within the green ring, indoors or outdoors. Only the strongest Verdite source applies to each benefit, and a Verdite scythe counts as a source.";
        return new InfoStats()
            .Add("Farming radius", InfoStats.Tiles(radius), rule)
            .Add("Plant growth", InfoStats.Plus(growthBonus), rule)
            .Add("Plant work speed", InfoStats.Plus(workBonus), rule)
            .Add("Plant harvest yield", InfoStats.Plus(yieldBonus), rule)
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

    public override void PostSpawnSetup(bool respawningAfterLoad) => parent.Map.GetComponent<VerditeField>().fixtures.Add(this);
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) => map.GetComponent<VerditeField>().fixtures.Remove(this);

    public override void CompTick()
    {
        if (!parent.Spawned) return;
        if (!parent.IsHashIntervalTick(360) || parent.Position.Fogged(parent.Map) || !CompAnimationToggle.On(parent)) return;
        var mote = FixtureMotes.Make("KM_Mote_VerditePulse", parent.Map);
        if (mote == null) return;
        mote.exactPosition = parent.DrawPos + parent.def.graphicData.drawOffset;
        mote.exactPosition.y = AltitudeLayer.MoteOverhead.AltitudeFor();
        mote.Scale = 0.07f;
        mote.instanceColor = new Color(0.76f, 0.95f, 0.32f);
        mote.airTimeLeft = 1.5f;
        mote.SetVelocity(parent.thingIDNumber % 2 == 0 ? 55 : 305, 0.22f);
        GenSpawn.Spawn(mote, parent.Position, parent.Map);
    }

}

public static class VerditeSupport
{
    public static float FixtureBonus(Map map, IntVec3 position, bool growth, bool harvest = false)
    {
        if (map == null) return 0;
        float strongest = 0;
        foreach (var fixture in map.GetComponent<VerditeField>().fixtures)
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
        return Mathf.Max(fixture, VerditeScythe.Equipped(pawn) ? VerditeScythe.FarmingBonus : 0);
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
        return bonus > 0 ? "Strongest Verdite support: +" + bonus.ToStringPercent() : null;
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
        if (bonus > 0) __result += "\nStrongest Verdite growth support: +" + bonus.ToStringPercent();
    }
}
