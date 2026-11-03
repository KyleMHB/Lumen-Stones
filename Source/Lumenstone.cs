using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LumenStones;

public class GenStep_LumenstoneSurface : GenStep_ScatterThings
{
    public float chance = 0.1f;
    public IntRange stacks = new(1, 2);
    public IntRange stackSize = new(5, 15);

    // The base step splits a total into near-full stacks, which would make these finds far too large.
    public override void Generate(Map map, GenStepParams parms)
    {
        if (ShouldSkipMap(map) || !Rand.Chance(chance)) return;
        int count = stacks.RandomInRange;
        for (int i = 0; i < count && TryFindScatterCell(map, out var cell); i++)
        {
            ScatterAt(cell, map, parms, stackSize.RandomInRange);
            usedSpots.Add(cell);
        }
        usedSpots.Clear();
    }
}

public class StockGenerator_Lumenstone : StockGenerator
{
    public float stockChance = 0.15f;
    private static ThingDef Stone => DefDatabase<ThingDef>.GetNamed("KM_Lumenstone");

    public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
    {
        if (!Rand.Chance(stockChance)) yield break;
        foreach (var thing in StockGeneratorUtility.TryMakeForStock(Stone, RandomCountOf(Stone), faction)) yield return thing;
    }

    public override bool HandlesThingDef(ThingDef thingDef) => thingDef == Stone;
}
