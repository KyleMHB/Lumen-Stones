using HarmonyLib;
using RimWorld;
using Verse;

namespace LumenStones;

public static class FerriteWarhammer
{
    public static float StunChance = 0.20f;
    public static int StunTicks = 120;
    private static ThingDef def;
    public static ThingDef Def => def ??= DefDatabase<ThingDef>.GetNamed("KM_FerriteWarhammer");
    public static bool Equipped(Pawn pawn) => pawn.equipment?.Primary is { } weapon && weapon.def == Def;

    internal static void DeclareTunables(TunableScope s)
    {
        var hammer = s.In("Ferrite warhammer");
        hammer.Add("KM_FerriteWarhammer.stunChance", "Stun chance on damaging melee hits (%)", StunChance, v => StunChance = v, Limit.Percent());
        hammer.Add("KM_FerriteWarhammer.stunTicks", "Stun duration (ticks)", StunTicks, v => StunTicks = (int)v, Limit.Integer(1, 60000));
        var hammerMending = Def.GetCompProperties<CompProperties_Ferrite>();
        hammer.Add("KM_FerriteWarhammer.repairBudget", "Condition mended per pulse (% of maximum hit points)", hammerMending.repairBudget,
            v => hammerMending.repairBudget = v, Limit.Percent());
    }
}

[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
public static class FerriteHammerStun
{
    public static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
    {
        if (__result.totalDamageDealt > 0 && __instance is Pawn victim && !victim.Dead &&
            dinfo.Instigator is Pawn wielder && FerriteWarhammer.Equipped(wielder) &&
            dinfo.Weapon == wielder.equipment.Primary.def && Rand.Chance(FerriteWarhammer.StunChance))
            victim.stances.stunner.StunFor(FerriteWarhammer.StunTicks, wielder);
    }
}
