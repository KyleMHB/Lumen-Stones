using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_WarpBattery : CompProperties, ITunableProps
{
    public float capacity = 100f;
    // Gauge crystals are full-canvas layers named <texPath>Charge1..N, drawn over the base sprite as the charge reaches each Nth.
    public int chargeLayers = 4;
    public CompProperties_WarpBattery() => compClass = typeof(CompWarpBattery);

    public void DeclareTunables(TunableScope s, Def owner) =>
        s.Add(owner.defName + ".capacity", "Warp energy capacity", capacity, v => capacity = v, Limit.Number(1));

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        string rule = "Every Bloodstone battery on a map shares one Warp network. Stored energy is lost if the battery is destroyed or deconstructed. It cannot be moved, and loses no charge over time.";
        return new InfoStats()
            .Add("Warp energy capacity", capacity.ToString("0"), rule)
            .Add("Sacrificial altar reach", InfoStats.Tiles(AltarRules.LinkRadius), "A Harvest Warp Energy sacrifice fills only batteries within this distance of the altar. " + rule)
            .Rows;
    }
}

// Stored Warp energy is saved with the battery and lost with it. Batteries are not minifiable, so it never travels.
public class CompWarpBattery : ThingComp
{
    public float stored;
    public CompProperties_WarpBattery Props => (CompProperties_WarpBattery)props;
    public float Spare => Mathf.Max(0, Props.capacity - stored);

    public override void PostSpawnSetup(bool respawningAfterLoad) => parent.Map.GetComponent<WarpEnergyNetwork>().Register(this);
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) => map.GetComponent<WarpEnergyNetwork>().Unregister(this);
    public override void PostExposeData() => Scribe_Values.Look(ref stored, "warpStored");

    public override string CompInspectStringExtra()
    {
        var network = parent.Map.GetComponent<WarpEnergyNetwork>();
        bool altarInRange = parent.Map.listerBuildings.AllBuildingsColonistOfClass<Building_SacrificialAltar>()
            .Any(altar => WarpEnergyNetwork.InRange(altar, parent));
        return "Warp energy: " + stored.ToString("0") + " / " + Props.capacity.ToString("0") +
            "\nSacrificial altar in range: " + (altarInRange ? "yes" : "no") +
            "\nNetwork: " + network.TotalStored.ToString("0") + " / " + network.TotalCapacity.ToString("0") +
            (network.Depleted ? "\nDepleted. Active effects resume at " + WarpRules.Hysteresis.ToString("0") + " stored." : "");
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (!Prefs.DevMode) yield break;
        yield return new Command_Action { defaultLabel = "DEV: +25 Warp", action = () => stored = Mathf.Min(Props.capacity, stored + 25f) };
        yield return new Command_Action { defaultLabel = "DEV: empty", action = () => stored = 0 };
    }

    public override void PostDraw()
    {
        int lit = Mathf.FloorToInt(Mathf.Clamp01(stored / Props.capacity) * Props.chargeLayers + 1e-4f);
        ChargeLayers.Draw(parent, lit, Props.chargeLayers);
    }
}

public static class WarpRules
{
    public const float DepletedBelow = ActiveEffectCore.DepletedBelow;
    public const int PassInterval = 250;
    // Broadcast to a fixture's comps when its Warp active effect is toggled, so caches and lights can react at once.
    public const string ToggledSignal = "WarpToggled";

    public static float Hysteresis = 5f;

    internal static void DeclareTunables(TunableScope s) =>
        s.In("Warp network").Add("DarkStone.warpHysteresis", "Stored Warp energy needed to recover from depletion", Hysteresis,
            v => Hysteresis = v, Limit.Number(0.02f));
}

// One network per map, behaving like SolarNetwork: shared draws, depletion with hysteresis and a pass for continuous consumers.
public class WarpEnergyNetwork : MapComponent, IEnergyStore
{
    private readonly List<CompWarpBattery> batteries = new();
    public readonly EnergyAccount Account;

    public WarpEnergyNetwork(Map map) : base(map) => Account = new(ActiveEffectKind.Warp, this);

    public bool Depleted => Account.Depleted;
    public bool Available => Account.Available;
    public static WarpEnergyNetwork For(Map map) => map.GetComponent<WarpEnergyNetwork>();

    public float TotalStored
    {
        get
        {
            float total = 0;
            foreach (var b in batteries) total += b.stored;
            return total;
        }
    }

    public float TotalCapacity
    {
        get
        {
            float total = 0;
            foreach (var b in batteries) total += b.Props.capacity;
            return total;
        }
    }

    public void Register(CompWarpBattery battery)
    {
        if (!batteries.Contains(battery)) batteries.Add(battery);
    }

    public void Unregister(CompWarpBattery battery) => batteries.Remove(battery);

    public override void ExposeData() => Account.ExposeData("warpDepleted");

    public override void MapComponentTick()
    {
        if ((batteries.Count == 0 && !Account.HasDrains) || Find.TickManager.TicksGame % WarpRules.PassInterval != 0) return;
        Account.RefreshDepletion();
        Account.PollDrains(WarpRules.PassInterval);
    }

    // Centre to centre, so walls do not matter.
    public static bool InRange(Thing altar, Thing battery) =>
        (altar.TrueCenter() - battery.TrueCenter()).MagnitudeHorizontalSquared() <= AltarRules.LinkRadius * AltarRules.LinkRadius;

    public List<CompWarpBattery> InRangeOf(Thing altar) => batteries.Where(b => InRange(altar, b.parent)).ToList();
    public float SpareInRange(Thing altar) => InRangeOf(altar).Sum(b => b.Spare);

    // Returns the amount stored. Anything over the spare capacity in range is lost.
    public float Deposit(float amount, Building altar) => Fill(InRangeOf(altar), amount);

    // Fills any battery on the map, for energy that has no altar to link to.
    public float DepositAnywhere(float amount) => Fill(batteries, amount);

    private static float Fill(List<CompWarpBattery> targets, float amount)
    {
        float spare = 0;
        foreach (var b in targets) spare += b.Spare;
        if (amount <= 0 || spare <= 0) return 0;
        float deposited = Mathf.Min(amount, spare);
        foreach (var b in targets) b.stored = Mathf.Min(b.Props.capacity, b.stored + deposited * b.Spare / spare);
        return deposited;
    }

    public bool CanDraw(float amount) => Account.CanDraw(amount);
    public bool TryConsume(float amount) => Account.TryDraw(amount);
    public bool TryConsumeContinuous(float amount) => Account.TryDrawContinuous(amount);

    float IEnergyStore.Stored => TotalStored;

    // Fixtures draw from any battery on the map, in proportion to what each holds.
    void IEnergyStore.Withdraw(float amount)
    {
        float total = TotalStored;
        foreach (var b in batteries) b.stored = Mathf.Max(0, b.stored - amount * b.stored / total);
    }
}

public class PlaceWorker_WarpLinkRing : PlaceWorker
{
    private static readonly Color Ring = new(0.78f, 0.25f, 0.5f);

    public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
    {
        if (ModsConfig.IdeologyActive) GenDraw.DrawRadiusRing(center, AltarRules.LinkRadius, Ring);
    }
}
