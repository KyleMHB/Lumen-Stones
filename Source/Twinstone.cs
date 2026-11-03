using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_Twinstone : CompProperties
{
    // Each pool holds this much. The default carries two implants for about ten days.
    public float capacity = 60f;
    // The Twinstone cell (Ideology) also has a Warp pool. The Sunstone cell is Solar only.
    public bool warp;
    public CompProperties_Twinstone() => compClass = typeof(CompTwinstone);
}

// A portable energy cell: the Solar-only Sunstone cell, or the Twinstone cell with separate Solar and Warp pools.
public class CompTwinstone : ThingComp
{
    public float solar, warp;
    public CompProperties_Twinstone Props => (CompProperties_Twinstone)props;
    public bool HasWarp => Props.warp;

    public override void PostExposeData()
    {
        Scribe_Values.Look(ref solar, "twinSolar");
        Scribe_Values.Look(ref warp, "twinWarp");
    }

    public bool TryTake(StoneSide side, float amount)
    {
        if (side == StoneSide.Light)
        {
            if (solar < amount) return false;
            solar -= amount;
            return true;
        }
        if (!HasWarp || warp < amount) return false;
        warp -= amount;
        return true;
    }

    public override string CompInspectStringExtra()
    {
        string text = "Solar energy: " + solar.ToString("0.#") + " / " + Props.capacity.ToString("0");
        if (HasWarp) text += "\nWarp energy: " + warp.ToString("0.#") + " / " + Props.capacity.ToString("0");
        var map = parent.MapHeld;
        if (map != null && map.IsPlayerHome) return text + "\nCharging from this settlement's networks.";
        // Cells drain one after another, so the estimate pools every cell that feeds the same pawns.
        var (light, dark, solarPool, warpPool) = TwinstoneCell.Demand(parent);
        if (light == 0 && dark == 0) return text + "\nNo implanted pawns to feed here.";
        if (light > 0) text += "\nSolar lasts " + (solarPool / (light * ImplantRules.DrainPerDay)).ToString("0.#") + " days for " + light + " part" + (light == 1 ? "" : "s");
        if (dark > 0 && HasWarp) text += "\nWarp lasts " + (warpPool / (dark * ImplantRules.DrainPerDay)).ToString("0.#") + " days for " + dark + " part" + (dark == 1 ? "" : "s");
        return text;
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (!Prefs.DevMode) yield break;
        yield return new Command_Action { defaultLabel = "DEV: fill", action = () => { solar = Props.capacity; if (HasWarp) warp = Props.capacity; } };
        yield return new Command_Action { defaultLabel = "DEV: empty", action = () => solar = warp = 0 };
    }
}

public static class TwinstoneCell
{
    // Low priority: a cell only charges from the part of a network above this share of its capacity.
    public const float ChargeAbove = 0.5f;
    public const int ChargeOffset = 175;

    private static List<ThingDef> defs;
    // Every cell def: the Sunstone cell, and the Twinstone cell with Ideology.
    public static List<ThingDef> Defs => defs ??= DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.HasComp<CompTwinstone>()).ToList();

    // Spawned cells, cells inside storage, and cells in pawn inventories.
    public static IEnumerable<CompTwinstone> On(Map map)
    {
        foreach (var def in Defs)
            foreach (var thing in map.listerThings.ThingsOfDef(def))
                if (thing.TryGetComp<CompTwinstone>() is { } cell) yield return cell;
        foreach (var pawn in map.mapPawns.AllPawnsSpawned)
        {
            if (pawn.inventory == null) continue;
            foreach (var thing in pawn.inventory.innerContainer)
                if (thing.TryGetComp<CompTwinstone>() is { } cell) yield return cell;
        }
    }

    public static IEnumerable<CompTwinstone> In(Caravan caravan)
    {
        foreach (var thing in CaravanInventoryUtility.AllInventoryItems(caravan))
            if (thing.TryGetComp<CompTwinstone>() is { } cell) yield return cell;
    }

    public static bool TryFeed(Pawn pawn, StoneSide side, float amount)
    {
        var cells = pawn.GetCaravan() is { } caravan ? In(caravan) : pawn.MapHeld is { } map ? On(map) : Enumerable.Empty<CompTwinstone>();
        foreach (var cell in cells)
            if (cell.TryTake(side, amount)) return true;
        return false;
    }

    private static Caravan CaravanOf(Thing thing) => thing.ParentHolder is Pawn_InventoryTracker inventory ? inventory.pawn.GetCaravan() : null;

    // Drawing parts on the colony's pawns this cell would feed, by side, and the pools of every cell feeding them.
    public static (int light, int dark, float solar, float warp) Demand(Thing cell)
    {
        var caravan = CaravanOf(cell);
        var map = caravan == null ? cell.MapHeld : null;
        IEnumerable<Pawn> pawns = caravan != null ? caravan.PawnsListForReading : map != null ? map.mapPawns.AllPawns : Enumerable.Empty<Pawn>();
        IEnumerable<CompTwinstone> cells = caravan != null ? In(caravan) : map != null ? On(map) : Enumerable.Empty<CompTwinstone>();
        int light = 0, dark = 0;
        foreach (var pawn in pawns)
        {
            if (pawn.Faction != Faction.OfPlayer && pawn.HostFaction != Faction.OfPlayer) continue;
            foreach (var part in ImplantRules.Parts(pawn))
            {
                if (part.dormant || !part.HasDrawback) continue;
                // With Sunstone off, light parts are relieved for free and take nothing from the cell.
                if (part.Props.side == StoneSide.Light) { if (SolarRules.Enabled) light++; }
                else dark++;
            }
            // A sun-starved reserve draws for its implant, which has no drawback of its own.
            foreach (var hediff in pawn.health.hediffSet.hediffs)
                if (hediff is Hediff_SunlightReserve { Starved: true } && SolarRules.Enabled) light++;
        }
        return (light, dark, cells.Sum(c => c.solar), cells.Sum(c => c.warp));
    }
}

// Tops up every cell on a home map from that map's networks, after fixtures have had their share.
public class TwinstoneCharger : MapComponent
{
    public TwinstoneCharger(Map map) : base(map) { }

    public override void MapComponentTick()
    {
        if (Find.TickManager.TicksGame % ImplantRules.PassInterval != TwinstoneCell.ChargeOffset || !map.IsPlayerHome || TwinstoneCell.Defs.Count == 0) return;
        var solar = SolarNetwork.For(map);
        var warp = WarpEnergyNetwork.For(map);
        foreach (var cell in TwinstoneCell.On(map).ToList())
        {
            float rate = cell.Props.capacity * ImplantRules.PassInterval / GenDate.TicksPerDay;
            // With Sunstone off, Solar energy is free, so the pool simply stays full.
            if (!SolarRules.Enabled) cell.solar = cell.Props.capacity;
            else cell.solar += Charge(Mathf.Min(rate, cell.Props.capacity - cell.solar), solar.TotalStored - solar.TotalCapacity * TwinstoneCell.ChargeAbove, solar.TryConsume);
            if (cell.HasWarp)
                cell.warp += Charge(Mathf.Min(rate, cell.Props.capacity - cell.warp), warp.TotalStored - warp.TotalCapacity * TwinstoneCell.ChargeAbove, warp.TryConsume);
        }
    }

    private static float Charge(float want, float surplus, System.Func<float, bool> consume)
    {
        float amount = Mathf.Min(want, surplus);
        return amount > 0.0001f && consume(amount) ? amount : 0f;
    }
}
