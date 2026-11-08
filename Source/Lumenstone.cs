using System;
using System.Linq;
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

// The raw stone's acquisition: veins, surface stacks and traders.
public static class LumenstoneSupply
{
    // Registers its range repair to run once saved overrides have been applied.
    internal static void DeclareTunables(TunableScope s)
    {
        var stone = DefDatabase<ThingDef>.GetNamed("KM_Lumenstone");
        var rock = DefDatabase<ThingDef>.GetNamed("KM_MineableLumenstone").building;
        var veins = s.In("Veins");
        veins.Add("Lumenstone.veinFrequency", "Vein frequency", rock.mineableScatterCommonality, v => rock.mineableScatterCommonality = v);
        veins.Add("Lumenstone.veinSizeMin", "Minimum vein size", rock.mineableScatterLumpSizeRange.min,
            v => rock.mineableScatterLumpSizeRange.min = (int)v, Limit.Integer(1));
        veins.Add("Lumenstone.veinSizeMax", "Maximum vein size", rock.mineableScatterLumpSizeRange.max,
            v => rock.mineableScatterLumpSizeRange.max = (int)v, Limit.Integer(1));
        veins.Add("Lumenstone.veinYield", "Mining yield per cell", rock.mineableYield, v => rock.mineableYield = (int)v, Limit.Integer(1));

        var surface = (GenStep_LumenstoneSurface)DefDatabase<GenStepDef>.GetNamed("KM_LumenstoneSurface").genStep;
        var ground = s.In("Surface");
        ground.Add("Lumenstone.surfaceChance", "Chance per map (%)", surface.chance, v => surface.chance = v, Limit.Percent());
        ground.Add("Lumenstone.surfaceStacksMin", "Minimum stacks", surface.stacks.min, v => surface.stacks.min = (int)v, Limit.Integer());
        ground.Add("Lumenstone.surfaceStacksMax", "Maximum stacks", surface.stacks.max, v => surface.stacks.max = (int)v, Limit.Integer());
        ground.Add("Lumenstone.surfaceCountMin", "Minimum stack size", surface.stackSize.min, v => surface.stackSize.min = (int)v, Limit.Integer(1));
        ground.Add("Lumenstone.surfaceCountMax", "Maximum stack size", surface.stackSize.max, v => surface.stackSize.max = (int)v, Limit.Integer(1));

        var stocks = DefDatabase<TraderKindDef>.AllDefsListForReading.SelectMany(t => t.stockGenerators.OfType<StockGenerator_Lumenstone>()).ToList();
        var traders = s.In("Traders");
        if (stocks.Count > 0)
        {
            traders.Add("Lumenstone.traderChance", "Stock chance (%)", stocks[0].stockChance,
                v => stocks.ForEach(stock => stock.stockChance = v), Limit.Percent());
            traders.Add("Lumenstone.traderCountMin", "Minimum stock", stocks[0].countRange.min,
                v => stocks.ForEach(stock => stock.countRange.min = (int)v), Limit.Integer(1));
            traders.Add("Lumenstone.traderCountMax", "Maximum stock", stocks[0].countRange.max,
                v => stocks.ForEach(stock => stock.countRange.max = (int)v), Limit.Integer(1));
        }
        traders.Add("Lumenstone.marketValue", "Market value (traders charge about seven times this)", stone.BaseMarketValue,
            v => stone.SetStatBaseValue(StatDefOf.MarketValue, v), Limit.Number(0.01f));

        s.AfterApply(() =>
        {
            rock.mineableScatterLumpSizeRange.max = Math.Max(rock.mineableScatterLumpSizeRange.min, rock.mineableScatterLumpSizeRange.max);
            surface.stacks.max = Math.Max(surface.stacks.min, surface.stacks.max);
            surface.stackSize.max = Math.Max(surface.stackSize.min, surface.stackSize.max);
            stocks.ForEach(stock => stock.countRange.max = Math.Max(stock.countRange.min, stock.countRange.max));
        });
    }
}
