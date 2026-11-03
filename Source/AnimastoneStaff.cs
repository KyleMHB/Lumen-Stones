using HarmonyLib;
using RimWorld;
using Verse;

namespace LumenStones;

public static class AnimastoneStaff
{
    public static float StunChance = 0.15f;
    public static int StunTicks = 60;

    // Null without Royalty, so the hit check below never matches.
    private static ThingDef def;
    private static bool looked;
    public static ThingDef Def
    {
        get
        {
            if (!looked) { def = DefDatabase<ThingDef>.GetNamedSilentFail("KM_AnimastoneStaff"); looked = true; }
            return def;
        }
    }
}

// The staff's only patch. Every hit made with another weapon leaves on the first reference comparison.
[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
public static class AnimastoneStaffHit
{
    public static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
    {
        if (dinfo.Weapon == null || dinfo.Weapon != AnimastoneStaff.Def || __result.totalDamageDealt <= 0) return;
        if (__instance is not Pawn { Dead: false } victim || dinfo.Instigator is not Pawn wielder) return;
        if (!Rand.Chance(AnimastoneStaff.StunChance * wielder.GetStatValue(StatDefOf.PsychicSensitivity))) return;
        victim.stances?.stunner?.StunFor(AnimastoneStaff.StunTicks, wielder);
    }
}
