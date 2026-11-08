using System;

namespace LumenStones;

// Pure rules shared by the Solar and Warp active-effect gates. No game types, so a test project can compile this file alone.
public enum DrawMode { Peek, PerEvent, Continuous }

public enum GateState { ToggledOff, Interrupted, Powered, NoEnergy }

public readonly record struct Ledger(float Stored, bool Depleted);

// Paid: the draw is allowed. Withdraw: what to take from the store. DepleteOnRefusal: a refusal marks the account depleted.
public readonly record struct DrawResult(bool Paid, float Withdraw, bool DepleteOnRefusal);

public static class ActiveEffectCore
{
    public const float DepletedBelow = 0.01f;

    public static bool Available(Ledger l) => !l.Depleted && l.Stored >= DepletedBelow;

    // Hysteresis: once depleted, stay so until the store reaches resumeAt.
    public static bool NextDepleted(Ledger l, float resumeAt) => l.Depleted ? l.Stored < resumeAt : l.Stored < DepletedBelow;

    public static DrawResult Draw(bool metered, Ledger l, float amount, DrawMode mode)
    {
        if (!metered || amount <= 0) return new(true, 0, false);
        if (l.Depleted || amount > l.Stored) return new(false, 0, mode == DrawMode.Continuous);
        return new(true, mode == DrawMode.Peek ? 0 : amount, false);
    }

    public static bool Running(bool toggledOn, bool blocked) => toggledOn && !blocked;

    public static bool Permitted(bool metered, bool toggledOn, bool blocked) => !metered || Running(toggledOn, blocked);

    public static bool Active(bool metered, bool toggledOn, bool blocked, Ledger l) => !metered || Running(toggledOn, blocked) && Available(l);

    // Ignores metered: the status line only shows while the effect is metered.
    public static GateState Status(bool toggledOn, bool blocked, Ledger l) =>
        !toggledOn ? GateState.ToggledOff : blocked ? GateState.Interrupted : Available(l) ? GateState.Powered : GateState.NoEnergy;

    // What a drain host is told after a poll: metered wanting hosts follow their payment, everything else follows the account.
    // Unmetered hosts are free through Powered, not here, so turning Sunstone back on doesn't leave a stale free pass.
    public static bool Polled(bool metered, float want, bool paid, Ledger l) => metered && want > 0 ? paid : Available(l);
}
