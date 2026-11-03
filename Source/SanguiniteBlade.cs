using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LumenStones;

public static class SanguiniteBlade
{
    public static int MaxStacks = 3;
    public const int StackTicks = 600;
    // Bleed rate of one stack, a little under a fresh cut of the same size.
    public const float BleedPerStack = 0.04f;

    private static ThingDef def;
    public static ThingDef Def => def ??= DefDatabase<ThingDef>.GetNamed("KM_SanguiniteBlade");
    private static HediffDef hemorrhage;
    public static HediffDef Hemorrhage => hemorrhage ??= DefDatabase<HediffDef>.GetNamed("KM_Hemorrhage");
}

// Each stack has its own expiry, so a long fight keeps the bleed going and a single cut fades alone.
public class Hediff_Hemorrhage : HediffWithComps
{
    private List<int> expiries = new();

    public override float BleedRate => expiries.Count * SanguiniteBlade.BleedPerStack;
    public override string LabelInBrackets => expiries.Count > 1 ? "x" + expiries.Count : null;

    public void AddStack()
    {
        expiries.Add(Find.TickManager.TicksGame + SanguiniteBlade.StackTicks);
        while (expiries.Count > SanguiniteBlade.MaxStacks) expiries.RemoveAt(0);
        Severity = expiries.Count;
    }

    // 1.7 ticks hediffs through TickInterval; Tick is never called.
    public override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        int now = Find.TickManager.TicksGame;
        if (expiries.RemoveAll(t => t <= now) == 0) return;
        if (expiries.Count == 0) pawn.health.RemoveHediff(this);
        else Severity = expiries.Count;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref expiries, "expiries", LookMode.Value);
        expiries ??= new List<int>();
    }
}

// Hemorrhage applies from this weapon only. Every hit with another weapon leaves on the first reference comparison.
[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
public static class SanguiniteBladeHit
{
    public static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
    {
        if (dinfo.Weapon != SanguiniteBlade.Def || __result.totalDamageDealt <= 0 ||
            __instance is not Pawn { Dead: false } victim || !victim.RaceProps.IsFlesh || !victim.health.CanBleed) return;
        if (victim.health.hediffSet.GetFirstHediffOfDef(SanguiniteBlade.Hemorrhage) is not Hediff_Hemorrhage hediff)
        {
            hediff = (Hediff_Hemorrhage)HediffMaker.MakeHediff(SanguiniteBlade.Hemorrhage, victim);
            victim.health.AddHediff(hediff);
        }
        hediff.AddStack();
    }
}
