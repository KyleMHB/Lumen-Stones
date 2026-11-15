using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

// One named settlement-wide ritual cooldown. Its owner (a MapComponent) calls ExposeData from its own ExposeData.
public sealed class SettlementCooldown
{
    private readonly string saveKey, reasonKey;
    private readonly Func<float> days;
    private int endTick;

    // days is a Func because the tunables are mutable statics that settings write after construction.
    // reasonKey names a Keyed sentence whose {0} is the time remaining.
    public SettlementCooldown(string saveKey, string reasonKey, Func<float> days)
    {
        this.saveKey = saveKey;
        this.reasonKey = reasonKey;
        this.days = days;
    }

    public void ExposeData() => Scribe_Values.Look(ref endTick, saveKey);

    public int EndTick => endTick;
    public int RemainingTicks => Math.Max(0, endTick - Find.TickManager.TicksGame);
    public bool Ready => RemainingTicks == 0;

    public string Reason => RemainingTicks is var remaining and > 0
        ? reasonKey.Translate(remaining.ToStringTicksToPeriod()).ToString() : null;

    public void Start() => endTick = Find.TickManager.TicksGame + Mathf.RoundToInt(days() * GenDate.TicksPerDay);
    public void StartFor(int ticks) => endTick = Find.TickManager.TicksGame + ticks;
    public void Reset() => endTick = 0;
}
