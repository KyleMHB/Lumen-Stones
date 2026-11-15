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
    // Ore lumps scatter earlier in map generation, so veins are already on the map here.
    public override void Generate(Map map, GenStepParams parms)
    {
        bool veins = map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("KM_MineableLumenstone")).Count > 0;
        int placed = PlaceSurfaceStacks(map, parms);
        if (veins || placed > 0) MapState.Of<LumenstoneHint>(map)?.Arm();
    }

    private int PlaceSurfaceStacks(Map map, GenStepParams parms)
    {
        if (ShouldSkipMap(map) || !Rand.Chance(chance)) return 0;
        int count = stacks.RandomInRange, placed = 0;
        for (int i = 0; i < count && TryFindScatterCell(map, out var cell); i++, placed++)
        {
            ScatterAt(cell, map, parms, stackSize.RandomInRange);
            usedSpots.Add(cell);
        }
        usedSpots.Clear();
        return placed;
    }
}

// Lumenstone is rare, so a map with veins or surface stacks says so once, an hour after play starts on it, without saying where.
public class LumenstoneHint : MapComponent
{
    private const int Delay = GenDate.TicksPerHour;
    private bool armed;
    private int sendAt = -1;

    public LumenstoneHint(Map map) : base(map) { }

    public void Arm() => armed = true;

    public override void MapComponentTick()
    {
        if (!armed) return;
        int now = Find.TickManager.TicksGame;
        if (sendAt < 0) { sendAt = now + Delay; return; }
        if (now < sendAt) return;
        armed = false;
        Find.LetterStack.ReceiveLetter("Lumen_FaintLight_Label".Translate(), "Lumen_FaintLight_Text".Translate(),
            LetterDefOf.NeutralEvent);
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref armed, "lumenstoneHintArmed");
        Scribe_Values.Look(ref sendAt, "lumenstoneHintAt", -1);
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
        var veins = s.In("Lumen_Lumenstone_Veins".Translate());
        veins.Add("Lumenstone.veinFrequency", "Lumen_Lumenstone_VeinFrequency".Translate(), rock.mineableScatterCommonality, v => rock.mineableScatterCommonality = v);
        veins.Add("Lumenstone.veinSizeMin", "Lumen_Lumenstone_VeinSizeMin".Translate(), rock.mineableScatterLumpSizeRange.min,
            v => rock.mineableScatterLumpSizeRange.min = (int)v, Limit.Integer(1));
        veins.Add("Lumenstone.veinSizeMax", "Lumen_Lumenstone_VeinSizeMax".Translate(), rock.mineableScatterLumpSizeRange.max,
            v => rock.mineableScatterLumpSizeRange.max = (int)v, Limit.Integer(1));
        veins.Add("Lumenstone.veinYield", "Lumen_Lumenstone_VeinYield".Translate(), rock.mineableYield, v => rock.mineableYield = (int)v, Limit.Integer(1));

        var surface = (GenStep_LumenstoneSurface)DefDatabase<GenStepDef>.GetNamed("KM_LumenstoneSurface").genStep;
        var ground = s.In("Lumen_Lumenstone_Surface".Translate());
        ground.Add("Lumenstone.surfaceChance", "Lumen_Lumenstone_SurfaceChance".Translate(), surface.chance, v => surface.chance = v, Limit.Percent());
        ground.Add("Lumenstone.surfaceStacksMin", "Lumen_Lumenstone_StacksMin".Translate(), surface.stacks.min, v => surface.stacks.min = (int)v, Limit.Integer());
        ground.Add("Lumenstone.surfaceStacksMax", "Lumen_Lumenstone_StacksMax".Translate(), surface.stacks.max, v => surface.stacks.max = (int)v, Limit.Integer());
        ground.Add("Lumenstone.surfaceCountMin", "Lumen_Lumenstone_StackSizeMin".Translate(), surface.stackSize.min, v => surface.stackSize.min = (int)v, Limit.Integer(1));
        ground.Add("Lumenstone.surfaceCountMax", "Lumen_Lumenstone_StackSizeMax".Translate(), surface.stackSize.max, v => surface.stackSize.max = (int)v, Limit.Integer(1));

        var stocks = DefDatabase<TraderKindDef>.AllDefsListForReading.SelectMany(t => t.stockGenerators.OfType<StockGenerator_Lumenstone>()).ToList();
        var traders = s.In("Lumen_Lumenstone_Traders".Translate());
        if (stocks.Count > 0)
        {
            traders.Add("Lumenstone.traderChance", "Lumen_Lumenstone_StockChance".Translate(), stocks[0].stockChance,
                v => stocks.ForEach(stock => stock.stockChance = v), Limit.Percent());
            traders.Add("Lumenstone.traderCountMin", "Lumen_Lumenstone_StockMin".Translate(), stocks[0].countRange.min,
                v => stocks.ForEach(stock => stock.countRange.min = (int)v), Limit.Integer(1));
            traders.Add("Lumenstone.traderCountMax", "Lumen_Lumenstone_StockMax".Translate(), stocks[0].countRange.max,
                v => stocks.ForEach(stock => stock.countRange.max = (int)v), Limit.Integer(1));
        }
        traders.Add("Lumenstone.marketValue", "Lumen_Lumenstone_MarketValue".Translate(), stone.BaseMarketValue,
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
