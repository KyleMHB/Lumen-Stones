using HarmonyLib;
using RimWorld;
using Verse;

namespace LumenStones;

public static class AegiteShield
{
    private static ThingDef def;
    public static ThingDef Def => def ??= DefDatabase<ThingDef>.GetNamed("KM_AegiteShield");
    // Read by the FlammableNow patch, so it compares a def reference, not a name.
    public static bool Worn(Pawn pawn)
    {
        var worn = pawn.apparel?.WornApparel;
        if (worn == null) return false;
        for (int i = 0; i < worn.Count; i++)
            if (worn[i].def == Def) return true;
        return false;
    }
}

public class StatPart_AegiteFireImmunity : StatPart
{
    public override void TransformValue(StatRequest req, ref float val)
    {
        if (req.Thing is Pawn pawn && AegiteShield.Worn(pawn)) val = 0;
    }

    public override string ExplanationPart(StatRequest req) =>
        req.Thing is Pawn pawn && AegiteShield.Worn(pawn) ? "Aegite shield: cannot catch fire while worn." : null;
}

[HarmonyPatch(typeof(Thing), nameof(Thing.FlammableNow), MethodType.Getter)]
public static class AegiteShieldIgnitionProtection
{
    public static void Postfix(Thing __instance, ref bool __result)
    {
        // Native ignition and fire attachment consult this gate. Keep immunity absolute
        // even if later stat modifiers raise the displayed flammability above zero.
        if (__result && __instance is Pawn pawn && AegiteShield.Worn(pawn)) __result = false;
    }
}
