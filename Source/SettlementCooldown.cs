using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

// One named settlement-wide ritual cooldown. Its owner (a MapComponent) calls ExposeData from its own ExposeData.
public sealed class SettlementCooldown
{
    private readonly string saveKey, activity, sharedBy;
    private readonly Func<float> days;
    private int endTick;

    // days is a Func because the tunables are mutable statics that settings write after construction.
    public SettlementCooldown(string saveKey, string activity, string sharedBy, Func<float> days)
    {
        this.saveKey = saveKey;
        this.activity = activity;
        this.sharedBy = sharedBy;
        this.days = days;
    }

    public void ExposeData() => Scribe_Values.Look(ref endTick, saveKey);

    public int EndTick => endTick;
    public int RemainingTicks => Math.Max(0, endTick - Find.TickManager.TicksGame);
    public bool Ready => RemainingTicks == 0;

    public string Reason => RemainingTicks is var remaining and > 0
        ? activity + " is recharging: " + remaining.ToStringTicksToPeriod() + " remaining (shared by every " + sharedBy + " in this settlement)." : null;

    public void Start() => endTick = Find.TickManager.TicksGame + Mathf.RoundToInt(days() * GenDate.TicksPerDay);
    public void StartFor(int ticks) => endTick = Find.TickManager.TicksGame + ticks;
    public void Reset() => endTick = 0;
}
