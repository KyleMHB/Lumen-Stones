using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public static class NewEquipment
{
    public static bool Wields(Pawn pawn, string name) => pawn?.equipment?.Primary?.def.defName == name;
    public static bool LivingHuman(Pawn pawn) => !pawn.Dead && pawn.RaceProps.Humanlike && pawn.RaceProps.IsFlesh && !SunstoneAxe.Unnatural(pawn);
    public static bool Collared(Pawn pawn) => pawn.IsSlaveOfColony && pawn.apparel?.WornApparel.Any(a => a.def.defName == "KM_DreadstoneCollar") == true;
    public static bool Subjugated(Pawn pawn) => pawn.MapHeld?.GetComponent<DreadstoneSubjugation>().Covers(pawn) == true;

    public static Hediff Stack(Pawn pawn, string name, int cap, int ticks)
    {
        var def = DefDatabase<HediffDef>.GetNamed(name);
        var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
        if (hediff == null)
        {
            hediff = HediffMaker.MakeHediff(def, pawn);
            hediff.Severity = 1f;
            pawn.health.AddHediff(hediff);
        }
        else hediff.Severity = Mathf.Min(cap, hediff.Severity + 1f);
        hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear = ticks;
        return hediff;
    }
}

[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
public static class CaptureAndBlightHit
{
    public static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
    {
        if (__result.totalDamageDealt <= 0 || dinfo.Weapon == null || __instance is not Pawn victim) return;
        string weapon = dinfo.Weapon.defName;
        if (weapon != "KM_DreadstoneCudgel" && weapon != "KM_DarkStoneRitualKnife") return;
        if (!NewEquipment.LivingHuman(victim)) return;
        if (weapon == "KM_DreadstoneCudgel")
        {
            var agony = (Hediff_DreadAgony)NewEquipment.Stack(victim, "KM_DreadAgony", 4, 1500);
            agony.RememberCapture();
        }
        else
        {
            NewEquipment.Stack(victim, "KM_SoulBlight", 3, 180000);
            victim.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("KM_SoulBlightMemory"));
        }
    }
}

public class Hediff_DreadAgony : HediffWithComps
{
    private bool remembered;

    public void RememberCapture()
    {
        if (remembered || !(pawn.Downed || pawn.IsPrisoner || pawn.IsSlave)) return;
        pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("KM_DreadstoneTorture"));
        remembered = true;
    }

    public override void PostTickInterval(int delta)
    {
        base.PostTickInterval(delta);
        RememberCapture();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref remembered, "dreadAgonyRemembered");
    }
}

public class ThoughtWorker_DreadstoneCollar : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p) => NewEquipment.Collared(p);
}

// Read the native fall-rate stat after apparel: coverage wins, otherwise the collar reduces decay by a quarter.
[HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue))]
public static class DreadstoneCollarSuppression
{
    public static void Postfix(Thing thing, StatDef stat, ref float __result)
    {
        if (stat == StatDefOf.SlaveSuppressionFallRate && thing is Pawn pawn && NewEquipment.Collared(pawn) && !NewEquipment.Subjugated(pawn))
            __result *= 0.75f;
    }
}

[HarmonyPatch(typeof(Apparel), nameof(Apparel.PawnCanWear))]
public static class DreadstoneCollarWearer
{
    public static void Postfix(Apparel __instance, Pawn pawn, ref bool __result)
    {
        if (__instance.def.defName == "KM_DreadstoneCollar" && !pawn.IsSlaveOfColony) __result = false;
    }
}

// The execution entry point also serves altar sacrifices. Only the normal prisoner execution job pays here.
[HarmonyPatch(typeof(ExecutionUtility), nameof(ExecutionUtility.DoExecutionByCut))]
public static class RitualKnifeExecution
{
    public static void Prefix(Pawn executioner, Pawn victim, out float __state)
    {
        __state = victim.IsPrisonerOfColony && NewEquipment.LivingHuman(victim) &&
            executioner.CurJobDef == JobDefOf.PrisonerExecution && NewEquipment.Wields(executioner, "KM_DarkStoneRitualKnife")
            ? AltarRules.HarvestSuccess * 0.1f * AltarRules.VictimFactor(victim.MarketValue, AltarRules.StatusOf(victim)) : 0f;
    }

    public static void Postfix(Pawn executioner, Pawn victim, float __state)
    {
        if (__state > 0 && victim.Dead && executioner.MapHeld != null)
            WarpEnergyNetwork.For(executioner.MapHeld).DepositAnywhere(__state);
    }
}

public class RitualOutcomeComp_StoneWeapon : RitualOutcomeComp_Quality
{
    public string weapon;
    private bool Equipped(Pawn pawn) => NewEquipment.Wields(pawn, weapon);
    public override float Count(LordJob_Ritual ritual, RitualOutcomeComp_Data data) => Equipped(ritual.PawnWithRole("speaker")) ? 1 : 0;
    public override float QualityOffset(LordJob_Ritual ritual, RitualOutcomeComp_Data data) => Count(ritual, data) * 0.1f;
    public override string GetDesc(LordJob_Ritual ritual = null, RitualOutcomeComp_Data data = null) =>
        ritual == null ? base.GetDesc(ritual, data) : label + ": +" + QualityOffset(ritual, data).ToStringPercent() + " quality";
    public override QualityFactor GetQualityFactor(Precept_Ritual ritual, TargetInfo ritualTarget, RitualObligation obligation,
        RitualRoleAssignments assignments, RitualOutcomeComp_Data data)
    {
        bool equipped = Equipped(assignments?.FirstAssignedPawn("speaker"));
        return new QualityFactor
        {
            label = label, count = equipped ? "equipped" : "not equipped", qualityChange = equipped ? "+10%" : "+0%",
            quality = equipped ? 0.1f : 0f, positive = true, priority = 3f,
            toolTip = "The ritual leader must wield the matching stone weapon."
        };
    }
}
