using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_WarpBattery : CompProperties
{
    public float capacity = 100f;
    // Gauge crystals are full-canvas layers named <texPath>Charge1..N, drawn over the base sprite as the charge reaches each Nth.
    public int chargeLayers = 4;
    public CompProperties_WarpBattery() => compClass = typeof(CompWarpBattery);

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
    public const float DepletedBelow = 0.01f;
    public const int PassInterval = 250;
    // Broadcast to a fixture's comps when its Warp active effect is toggled, so caches and lights can react at once.
    public const string ToggledSignal = "WarpToggled";

    public static float Hysteresis = 5f;
}

// A continuous drain. The network asks once per pass for the rate and charges the elapsed interval, then caches whether it was paid.
public interface IWarpConsumer
{
    // Warp energy wanted per tick right now, or 0 when idle.
    float WarpDrainPerTick { get; }
    // Set by the network each pass. False until the first pass, and when a draw is refused.
    bool WarpPowered { get; set; }
}

// One network per map, behaving like SolarNetwork: shared draws, depletion with hysteresis and a pass for continuous consumers.
public class WarpEnergyNetwork : MapComponent
{
    private readonly List<CompWarpBattery> batteries = new();
    private readonly List<IWarpConsumer> consumers = new();
    private bool depleted;

    public WarpEnergyNetwork(Map map) : base(map) { }

    public bool Depleted => depleted;
    public bool Available => !depleted && TotalStored >= WarpRules.DepletedBelow;
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

    public void Register(IWarpConsumer consumer)
    {
        if (!consumers.Contains(consumer)) consumers.Add(consumer);
    }

    public void Unregister(IWarpConsumer consumer) => consumers.Remove(consumer);

    public override void ExposeData() => Scribe_Values.Look(ref depleted, "warpDepleted");

    public override void MapComponentTick()
    {
        if ((batteries.Count == 0 && consumers.Count == 0) || Find.TickManager.TicksGame % WarpRules.PassInterval != 0) return;
        UpdateDepletion();
        // Idle consumers are powered when the network is. A refused draw leaves that consumer unpowered until the next pass.
        foreach (var consumer in consumers)
        {
            float want = consumer.WarpDrainPerTick * WarpRules.PassInterval;
            consumer.WarpPowered = want > 0 ? TryConsumeContinuous(want) : Available;
        }
    }

    private void UpdateDepletion()
    {
        float stored = TotalStored;
        if (depleted) { if (stored >= WarpRules.Hysteresis) depleted = false; }
        else if (stored < WarpRules.DepletedBelow) depleted = true;
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

    // Read-only check for ownership decisions.
    public bool CanDraw(float amount) => amount <= 0 || (!depleted && amount <= TotalStored);

    // All or nothing, for per-event costs. A refusal does not deplete the network.
    public bool TryConsume(float amount) => Draw(amount, false);

    // For costs charged over time. A refusal marks the network depleted until it recovers.
    public bool TryConsumeContinuous(float amount) => Draw(amount, true);

    // Fixtures draw from any battery on the map, in proportion to what each holds.
    private bool Draw(float amount, bool continuous)
    {
        if (amount <= 0) return true;
        float total = TotalStored;
        if (depleted || amount > total)
        {
            if (continuous) depleted = true;
            return false;
        }
        foreach (var b in batteries) b.stored = Mathf.Max(0, b.stored - amount * b.stored / total);
        UpdateDepletion();
        return true;
    }
}

public class CompProperties_WarpToggle : CompProperties
{
    public string effect = "its active effect";
    // Replaces the default description when an effect needs a warning.
    public string description;
    public CompProperties_WarpToggle() => compClass = typeof(CompWarpToggle);
}

// Attached to every dark fixture that has an active effect. Consumers ask Active before spending Warp energy.
[StaticConstructorOnStartup]
public class CompWarpToggle : ThingComp
{
    private bool on = true;
    private static Texture2D icon;
    public CompProperties_WarpToggle Props => (CompProperties_WarpToggle)props;

    public override void PostExposeData() => Scribe_Values.Look(ref on, "warpActiveOn", true);

    public bool ToggledOn => on;

    // Toggled on and not paused by an interruption the fixture's stabilizer cannot ride out. Energy is checked separately.
    public bool Running => on && !ActiveEffectInterruption.Blocked(parent, ActiveEffectNetworks.Warp);

    // True when the active effect may run: running and the network has Warp energy.
    public bool Active => Running && WarpEnergyNetwork.For(parent.Map).Available;

    // Pays a per-event cost. Refused when paused or toggled off or when the network cannot cover it.
    public bool TryPay(float amount) => Running && WarpEnergyNetwork.For(parent.Map).TryConsume(amount);

    public string Status
    {
        get
        {
            if (!on) return "Toggled off";
            if (ActiveEffectInterruption.BlockedReason(parent, ActiveEffectNetworks.Warp) is { } reason) return "Interrupted (" + reason + ")";
            return WarpEnergyNetwork.For(parent.Map).Available ? "Powered" : "No Warp energy";
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad) => LuniteLinkTracker.For(parent.Map).RegisterFixture(parent, true);
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) => LuniteLinkTracker.For(map).RegisterFixture(parent, false);

    public override string CompInspectStringExtra()
    {
        if (!parent.Spawned) return null;
        string text = "Warp active effect: " + Status;
        return LuniteLinkTracker.For(parent.Map).StabilizerOf(parent) != null ? text + "\nStabilized by: Lunite stabilizer" : text;
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        yield return new Command_Toggle
        {
            defaultLabel = "Warp active effect",
            defaultDesc = Props.description ?? "Spend Warp energy on " + Props.effect + ". When off, the fixture only gives light.",
            icon = icon ??= ContentFinder<Texture2D>.Get("UI/Commands/WarpActiveEffect"),
            isActive = () => on,
            toggleAction = () => { on = !on; parent.BroadcastCompSignal(WarpRules.ToggledSignal); }
        };
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
