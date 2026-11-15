using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

// The Glacite greatsword (defName KM_GlaciteBlade, kept for saves). Each damaging hit on living flesh adds Glacite chill,
// a vanilla severity hediff whose stages slow Moving and Manipulation and whose SeverityPerDay comp makes it fade.
public static class GlaciteBlade
{
    // Three hits in quick succession reach the last stage (0.75). Creatures larger than a human take less per hit.
    public static float ChillPerHit = 0.35f;

    private static ThingDef def;
    public static ThingDef Def => def ??= DefDatabase<ThingDef>.GetNamed("KM_GlaciteBlade");
    private static HediffDef chill;
    public static HediffDef Chill => chill ??= DefDatabase<HediffDef>.GetNamed("KM_GlaciteChill");

    internal static void DeclareTunables(TunableScope s)
    {
        StageTunables.Hediff(s, "KM_GlaciteChill");
        var section = s.In(Chill.LabelCap).PerItem();
        section.Add("KM_GlaciteChill.severityPerHit", "Lumen_GlaciteBlade_ChillPerHit".Translate(), ChillPerHit, v => ChillPerHit = v, Limit.Number(0, 1));
        if (Chill.CompProps<HediffCompProperties_SeverityPerDay>() is { } decay)
            section.Add("KM_GlaciteChill.severityPerDay", "Lumen_Setting_WearsOffPerDay".Translate(), -decay.severityPerDay,
                v => decay.severityPerDay = -v, Limit.Number(0.01f, 1000));
    }

    public static void AddChill(Pawn victim)
    {
        float amount = ChillPerHit / Mathf.Max(1f, victim.BodySize);
        if (amount <= 0f) return;
        var hediff = victim.health.hediffSet.GetFirstHediffOfDef(Chill);
        if (hediff == null)
        {
            hediff = HediffMaker.MakeHediff(Chill, victim);
            hediff.Severity = amount;
            victim.health.AddHediff(hediff);
        }
        else hediff.Severity += amount;
    }
}

// The greatsword takes both hands, so it and the Aegite shield exclude each other. Vanilla equip and wear menus and
// opportunistic weapon pickup ask EquipmentUtility.CanEquip; the apparel optimizer scores instead, so it is patched too.
// A pawn that already holds both from an older save keeps them until one is removed.
public static class GlaciteBladeTwoHanded
{
    public static bool Blocked(ThingDef thing, Pawn pawn, out string reason)
    {
        reason = null;
        if (thing == GlaciteBlade.Def)
        {
            var worn = pawn.apparel?.WornApparel;
            if (worn != null)
                for (int i = 0; i < worn.Count; i++)
                    if (worn[i].def == AegiteShield.Def) { reason = "Lumen_GlaciteBlade_ShieldWorn".Translate(); return true; }
        }
        else if (thing == AegiteShield.Def && pawn.equipment?.Primary?.def == GlaciteBlade.Def)
        {
            reason = "Lumen_GlaciteBlade_HandsFull".Translate();
            return true;
        }
        return false;
    }
}

[HarmonyPatch(typeof(EquipmentUtility), nameof(EquipmentUtility.CanEquip), new[] { typeof(Thing), typeof(Pawn), typeof(string), typeof(bool) },
    new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })]
public static class GlaciteBladeCanEquip
{
    public static void Postfix(Thing thing, Pawn pawn, ref string cantReason, ref bool __result)
    {
        if (__result && pawn != null && GlaciteBladeTwoHanded.Blocked(thing.def, pawn, out string reason))
        {
            __result = false;
            cantReason = reason;
        }
    }
}

[HarmonyPatch(typeof(JobGiver_OptimizeApparel), nameof(JobGiver_OptimizeApparel.ApparelScoreGain))]
public static class GlaciteBladeApparelScore
{
    public static void Postfix(Pawn pawn, Apparel ap, ref float __result)
    {
        if (GlaciteBladeTwoHanded.Blocked(ap.def, pawn, out _)) __result = -1000f;
    }
}

// Chill applies from this weapon only. Every hit with another weapon leaves on the first reference comparison.
[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
public static class GlaciteBladeHit
{
    public static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
    {
        if (dinfo.Weapon != GlaciteBlade.Def || __result.totalDamageDealt <= 0 || !Effects.GlaciteBladeChill.On ||
            __instance is not Pawn { Dead: false } victim || !victim.RaceProps.IsFlesh) return;
        GlaciteBlade.AddChill(victim);
    }
}
