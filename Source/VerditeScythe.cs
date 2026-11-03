using System.Linq;
using HarmonyLib;
using Verse;
using UnityEngine;

namespace LumenStones;

public static class VerditeScythe
{
    public static float FarmingBonus = 0.15f;
    public static float LifeStealFraction = 0.03f;
    private static ThingDef def;
    public static ThingDef Def => def ??= DefDatabase<ThingDef>.GetNamed("KM_VerditeScythe");
    public static bool Equipped(Pawn pawn) => pawn.equipment?.Primary is { } weapon && weapon.def == Def;
}

[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
public static class VerditeLifeSteal
{
    public static void Prefix(Thing __instance, DamageInfo dinfo, out Pawn __state)
    {
        // Capture eligibility before damage: lethal strikes still hit a living target.
        __state = __instance is Pawn victim && !victim.Dead && victim.RaceProps.IsFlesh &&
            dinfo.Instigator is Pawn wielder && VerditeScythe.Equipped(wielder) &&
            dinfo.Weapon == wielder.equipment.Primary.def ? wielder : null;
    }

    public static void Postfix(Pawn __state, DamageWorker.DamageResult __result)
    {
        if (__state == null || __state.Dead) return;
        // Each native damage application has its own actual result, including extra melee damage.
        float remaining = __result.totalDamageDealt * VerditeScythe.LifeStealFraction;
        foreach (var injury in __state.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i => !i.IsPermanent()).ToList())
        {
            if (remaining <= 0) break;
            float amount = Mathf.Min(remaining, injury.Severity);
            injury.Heal(amount);
            remaining -= amount;
        }
    }
}
