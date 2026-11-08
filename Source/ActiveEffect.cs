using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LumenStones;

// What a network exposes to its account: the stored total, and a way to take from its buffers.
public interface IEnergyStore
{
    float Stored { get; }
    // Precondition: 0 < amount <= Stored.
    void Withdraw(float amount);
}

// A fixture's own domain condition for spending energy continuously. The toggle supplies on, interrupted and metered.
public interface IActiveEffectDrain
{
    float DrainPerTick { get; }
}

internal interface IDrainHost
{
    bool Running { get; }
    float DrainPerTick { get; }
    void Polled(bool powered);
}

// Per-network constants. Solar's metering follows the Sunstone setting live.
public sealed class ActiveEffectKind
{
    public static readonly ActiveEffectKind Solar = new(ActiveEffectNetworks.Solar, "Solar", SolarRules.ToggledSignal, "UI/Commands/SolarActiveEffect",
        "When off, only the passive effect works.", () => SolarRules.Enabled, () => SolarRules.Hysteresis, () => SolarRules.Visible,
        map => SolarNetwork.For(map).Account);

    public static readonly ActiveEffectKind Warp = new(ActiveEffectNetworks.Warp, "Warp", WarpRules.ToggledSignal, "UI/Commands/WarpActiveEffect",
        "When off, the fixture only gives light.", () => true, () => WarpRules.Hysteresis, () => true,
        map => WarpEnergyNetwork.For(map).Account);

    private readonly Func<bool> metered, visible;
    private readonly Func<float> resumeAt;
    private readonly Func<Map, EnergyAccount> account;
    private Texture2D icon;

    private ActiveEffectKind(ActiveEffectNetworks network, string noun, string toggledSignal, string iconPath, string offDescription,
        Func<bool> metered, Func<float> resumeAt, Func<bool> visible, Func<Map, EnergyAccount> account)
    {
        Network = network;
        Noun = noun;
        ToggledSignal = toggledSignal;
        IconPath = iconPath;
        OffDescription = offDescription;
        this.metered = metered;
        this.resumeAt = resumeAt;
        this.visible = visible;
        this.account = account;
    }

    public ActiveEffectNetworks Network { get; }
    public string Noun { get; }
    public string ToggledSignal { get; }
    public string IconPath { get; }
    public string OffDescription { get; }
    public bool Metered => metered();
    public float ResumeAt => resumeAt();
    public bool Visible => visible();
    public string PoweredSignal => Noun + "Powered";
    public Texture2D Icon => icon ??= ContentFinder<Texture2D>.Get(IconPath);
    public EnergyAccount For(Map map) => account(map);
}

// One per network. Owns depletion and the draw rules, and polls its registered drains once per network pass.
public sealed class EnergyAccount
{
    private readonly IEnergyStore store;
    private readonly List<IDrainHost> hosts = new();
    private bool depleted;

    public EnergyAccount(ActiveEffectKind kind, IEnergyStore store)
    {
        Kind = kind;
        this.store = store;
    }

    public ActiveEffectKind Kind { get; }
    public bool Depleted => depleted;
    public bool Available => ActiveEffectCore.Available(Ledger);
    public bool HasDrains => hosts.Count > 0;
    public Ledger Ledger => new(store.Stored, depleted);

    // Read-only check for ownership decisions.
    public bool CanDraw(float amount) => Draw(amount, DrawMode.Peek);

    // All or nothing, for per-event costs. A refusal does not deplete the account.
    public bool TryDraw(float amount) => Draw(amount, DrawMode.PerEvent);

    // For costs charged over time. A refusal marks the account depleted until it recovers.
    public bool TryDrawContinuous(float amount) => Draw(amount, DrawMode.Continuous);

    private bool Draw(float amount, DrawMode mode)
    {
        var result = ActiveEffectCore.Draw(Kind.Metered, Ledger, amount, mode);
        if (!result.Paid)
        {
            if (result.DepleteOnRefusal) depleted = true;
            return false;
        }
        if (result.Withdraw > 0)
        {
            store.Withdraw(result.Withdraw);
            RefreshDepletion();
        }
        return true;
    }

    public void RefreshDepletion() => depleted = ActiveEffectCore.NextDepleted(Ledger, Kind.ResumeAt);

    internal void Register(IDrainHost host)
    {
        if (!hosts.Contains(host)) hosts.Add(host);
    }

    internal void Unregister(IDrainHost host) => hosts.Remove(host);

    // Idle hosts are powered when the account is. A refused draw leaves that host unpowered until the next pass.
    public void PollDrains(int interval)
    {
        foreach (var host in hosts)
        {
            float want = host.Running ? host.DrainPerTick * interval : 0;
            bool paid = want > 0 && TryDrawContinuous(want);
            host.Polled(ActiveEffectCore.Polled(Kind.Metered, want, paid, Ledger));
        }
    }

    public void ExposeData(string depletedKey) => Scribe_Values.Look(ref depleted, depletedKey);
}

public abstract class CompProperties_ActiveEffect : CompProperties
{
    public string effect = "its active effect";
    // Replaces the default description when an effect needs a warning.
    public string description;
}

public class CompProperties_SolarToggle : CompProperties_ActiveEffect
{
    public CompProperties_SolarToggle() => compClass = typeof(CompSolarToggle);
}

public class CompProperties_WarpToggle : CompProperties_ActiveEffect
{
    public CompProperties_WarpToggle() => compClass = typeof(CompWarpToggle);
}

// Attached to every fixture that has an active effect. Fixtures ask it, never the network, whether their effect may run.
public abstract class CompActiveEffect : ThingComp, IDrainHost
{
    private bool on = true;
    // Result of the last drain poll. Null until the first poll, and never saved.
    private bool? polled;
    private IActiveEffectDrain drain;

    public abstract ActiveEffectKind Kind { get; }
    protected abstract string ScribeKey { get; }

    public CompProperties_ActiveEffect Props => (CompProperties_ActiveEffect)props;
    public EnergyAccount Account => Kind.For(parent.Map);
    public bool ToggledOn => on;

    private bool Blocked => ActiveEffectInterruption.Blocked(parent, Kind.Network);

    // Toggled on and not paused by an interruption the fixture's stabilizer cannot ride out. Energy is checked separately.
    public bool Running => ActiveEffectCore.Running(on, Blocked);

    // Free when unmetered, otherwise only while running.
    public bool Permitted => ActiveEffectCore.Permitted(Kind.Metered, on, Blocked);

    // True when the active effect may run: free when unmetered, otherwise running with energy in the account.
    public bool Active => ActiveEffectCore.Active(Kind.Metered, on, Blocked, Account.Ledger);

    public bool CanPay(float amount) => Permitted && Account.CanDraw(amount);

    // Pays a per-event cost. Free when unmetered, refused when paused or toggled off or when the account cannot cover it.
    public bool TryPay(float amount) => !Kind.Metered || Running && Account.TryDraw(amount);

    // A drain's effect state: the last poll, or the account's live state before the first poll.
    // Minified fixtures have no map, and their inspect string still asks.
    public bool Powered => !Kind.Metered || Running && (polled ?? (parent.Map is { } map && Kind.For(map).Available));

    public string Status => ActiveEffectCore.Status(on, Blocked, Account.Ledger) switch
    {
        GateState.ToggledOff => "Toggled off",
        GateState.Interrupted => "Interrupted (" + ActiveEffectInterruption.BlockedReason(parent, Kind.Network) + ")",
        GateState.Powered => "Powered",
        _ => "No " + Kind.Noun + " energy"
    };

    bool IDrainHost.Running => Running;
    float IDrainHost.DrainPerTick => drain?.DrainPerTick ?? 0;

    void IDrainHost.Polled(bool result)
    {
        bool before = Powered;
        polled = result;
        if (parent.Spawned && before != Powered) parent.BroadcastCompSignal(Kind.PoweredSignal);
    }

    public override void PostExposeData() => Scribe_Values.Look(ref on, ScribeKey, true);

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        LuniteLinkTracker.For(parent.Map).RegisterFixture(parent, true);
        drain = null;
        polled = null;
        foreach (var comp in parent.AllComps)
        {
            if (comp is not IActiveEffectDrain found) continue;
            if (drain != null) Log.ErrorOnce("Lumen Stones: " + parent.def.defName + " has more than one active-effect drain.", parent.def.shortHash ^ 0x4c53);
            else drain = found;
        }
        if (drain != null) Account.Register(this);
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        LuniteLinkTracker.For(map).RegisterFixture(parent, false);
        Kind.For(map).Unregister(this);
    }

    public override string CompInspectStringExtra()
    {
        if (!Kind.Visible || !parent.Spawned) return null;
        string text = Kind.Noun + " active effect: " + Status;
        return LuniteLinkTracker.For(parent.Map).StabilizerOf(parent) != null ? text + "\nStabilized by: Lunite stabilizer" : text;
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (!Kind.Visible) yield break;
        yield return new Command_Toggle
        {
            defaultLabel = Kind.Noun + " active effect",
            defaultDesc = Props.description ?? "Spend " + Kind.Noun + " energy on " + Props.effect + ". " + Kind.OffDescription,
            icon = Kind.Icon,
            isActive = () => on,
            toggleAction = () => { on = !on; parent.BroadcastCompSignal(Kind.ToggledSignal); }
        };
    }
}

public class CompSolarToggle : CompActiveEffect
{
    public override ActiveEffectKind Kind => ActiveEffectKind.Solar;
    protected override string ScribeKey => "solarActiveOn";
}

public class CompWarpToggle : CompActiveEffect
{
    public override ActiveEffectKind Kind => ActiveEffectKind.Warp;
    protected override string ScribeKey => "warpActiveOn";
}
