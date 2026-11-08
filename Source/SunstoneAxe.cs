using HarmonyLib;
using RimWorld;
using Verse;

namespace LumenStones;

public static class SunstoneAxe
{
    public static float UnnaturalDamageMultiplier = 1.5f, ProcChance = 0.25f, UnnaturalProcChance = 0.5f;
    public static int BlindTicks = 300, StunTicks = 60;

    private static ThingDef def;
    public static ThingDef Def => def ??= DefDatabase<ThingDef>.GetNamed("KM_SunstoneAxe");

    public static bool Unnatural(Pawn pawn) => pawn.RaceProps.IsAnomalyEntity || pawn.IsMutant;

    internal static void DeclareTunables(TunableScope s)
    {
        var axe = s.In("Sunstone axe");
        axe.Add("KM_SunstoneAxe.baneMultiplier", "Melee damage multiplier against Anomaly entities and mutants", UnnaturalDamageMultiplier,
            v => UnnaturalDamageMultiplier = v, Limit.Number(1, 10));
        axe.Add("KM_SunstoneAxe.procChance", "Blind and stun chance on damaging melee hits (%)", ProcChance, v => ProcChance = v, Limit.Percent());
        axe.Add("KM_SunstoneAxe.procChanceUnnatural", "Blind and stun chance against Anomaly entities and mutants (%)", UnnaturalProcChance,
            v => UnnaturalProcChance = v, Limit.Percent());
        axe.Add("KM_SunstoneAxe.blindTicks", "Solar blindness duration (ticks)", BlindTicks, v => BlindTicks = (int)v, Limit.Integer(1, 60000));
        axe.Add("KM_SunstoneAxe.stunTicks", "Flash stun duration (ticks)", StunTicks, v => StunTicks = (int)v, Limit.Integer(1, 60000));
    }
}

// The axe's only patch. Every hit made with another weapon leaves on the first reference comparison.
[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
public static class SunstoneAxeHit
{
    public static void Prefix(Thing __instance, ref DamageInfo dinfo)
    {
        if (dinfo.Weapon != SunstoneAxe.Def || __instance is not Pawn victim || !SunstoneAxe.Unnatural(victim)) return;
        dinfo.SetAmount(dinfo.Amount * SunstoneAxe.UnnaturalDamageMultiplier);
    }

    public static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
    {
        if (dinfo.Weapon != SunstoneAxe.Def || __result.totalDamageDealt <= 0 || __instance is not Pawn { Dead: false } victim) return;
        if (!Rand.Chance(SunstoneAxe.Unnatural(victim) ? SunstoneAxe.UnnaturalProcChance : SunstoneAxe.ProcChance)) return;
        var blindness = DefDatabase<HediffDef>.GetNamed("KM_SolarBlindness");
        var existing = victim.health.hediffSet.GetFirstHediffOfDef(blindness);
        if (existing != null) victim.health.RemoveHediff(existing);
        var hediff = victim.health.AddHediff(blindness);
        if (hediff.TryGetComp<HediffComp_Disappears>() is { } timer) timer.ticksToDisappear = SunstoneAxe.BlindTicks;
        victim.stances?.stunner?.StunFor(SunstoneAxe.StunTicks, dinfo.Instigator);
    }
}
