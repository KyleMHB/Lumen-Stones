using HarmonyLib;
using RimWorld;
using Verse;

namespace LumenStones;

public static class AegiteWarhammer
{
    public static float StunChance = 0.20f;
    public static int StunTicks = 120;
    private static ThingDef def;
    public static ThingDef Def => def ??= DefDatabase<ThingDef>.GetNamed("KM_AegiteWarhammer");
    // Read by the FlammableNow patch, so it compares a def reference, not a name.
    public static bool Equipped(Pawn pawn) => pawn.equipment?.Primary is { } weapon && weapon.def == Def;
}

[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
public static class AegiteHammerStun
{
    public static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
    {
        if (__result.totalDamageDealt > 0 && __instance is Pawn victim && !victim.Dead &&
            dinfo.Instigator is Pawn wielder && AegiteWarhammer.Equipped(wielder) &&
            dinfo.Weapon == wielder.equipment.Primary.def && Rand.Chance(AegiteWarhammer.StunChance))
            victim.stances.stunner.StunFor(AegiteWarhammer.StunTicks, wielder);
    }
}

public class StatPart_AegiteFireImmunity : StatPart
{
    public override void TransformValue(StatRequest req, ref float val)
    {
        if (req.Thing is Pawn pawn && AegiteWarhammer.Equipped(pawn)) val = 0;
    }

    public override string ExplanationPart(StatRequest req) =>
        req.Thing is Pawn pawn && AegiteWarhammer.Equipped(pawn) ? "Aegite warhammer: cannot catch fire while equipped." : null;
}

[HarmonyPatch(typeof(Thing), nameof(Thing.FlammableNow), MethodType.Getter)]
public static class AegiteHammerIgnitionProtection
{
    public static void Postfix(Thing __instance, ref bool __result)
    {
        // Native ignition and fire attachment consult this gate. Keep immunity absolute
        // even if later stat modifiers raise the displayed flammability above zero.
        if (__result && __instance is Pawn pawn && AegiteWarhammer.Equipped(pawn)) __result = false;
    }
}
