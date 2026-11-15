using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public static class NewEquipment
{
    public static int AgonyCap = 4, AgonyTicks = 1500, BlightCap = 3, BlightTicks = 180000;
    public static float CollarSuppression = 0.75f, CollarBreakFactor = 2f, KnifeHarvestShare = 0.1f, StaffQuality = 0.1f, KnifeQuality = 0.1f;

    internal static void DeclareDreadstoneTunables(TunableScope s)
    {
        var cudgel = s.In("Lumen_Cudgel_Section".Translate()).PerItem();
        cudgel.Add("KM_DreadstoneCudgel.agonyCap", "Lumen_Cudgel_AgonyCap".Translate(), AgonyCap, v => AgonyCap = (int)v, Limit.Integer(1, 10));
        cudgel.Add("KM_DreadstoneCudgel.agonyTicks", "Lumen_Cudgel_AgonyTicks".Translate(), AgonyTicks, v => AgonyTicks = (int)v, Limit.Integer(1, 600000));
        var collar = s.In("Lumen_Collar_Section".Translate()).PerItem();
        collar.Add("KM_DreadstoneCollar.suppression", "Lumen_Collar_Suppression".Translate(), CollarSuppression, v => CollarSuppression = v, Limit.Percent(10));
        collar.Add("KM_DreadstoneCollar.breakFactor", "Lumen_Collar_BreakFactor".Translate(), CollarBreakFactor, v => CollarBreakFactor = v, Limit.Number(1, 100));
    }

    internal static void DeclareKnifeTunables(TunableScope s)
    {
        var knife = s.In("Lumen_Knife_Section".Translate()).PerItem();
        knife.Add("KM_DarkStoneRitualKnife.blightCap", "Lumen_Knife_BlightCap".Translate(), BlightCap, v => BlightCap = (int)v, Limit.Integer(1, 10));
        knife.Add("KM_DarkStoneRitualKnife.blightTicks", "Lumen_Knife_BlightTicks".Translate(), BlightTicks, v => BlightTicks = (int)v, Limit.Integer(1, 6000000));
        knife.Add("KM_DarkStoneRitualKnife.harvestShare", "Lumen_Knife_HarvestShare".Translate(), KnifeHarvestShare,
            v => KnifeHarvestShare = v, Limit.Percent());
        knife.Add("KM_DarkStoneRitualKnife.quality", "Lumen_Setting_LeaderQuality".Translate(), KnifeQuality, v => KnifeQuality = v, Limit.Percent());
    }

    internal static void DeclareStaffTunables(TunableScope s) =>
        s.In("Lumen_Staff_Section".Translate()).PerItem().Add("KM_LumenstoneStaff.quality", "Lumen_Setting_LeaderQuality".Translate(), StaffQuality,
            v => StaffQuality = v, Limit.Percent());

    public static bool Wields(Pawn pawn, ThingDef weapon) => weapon != null && pawn?.equipment?.Primary?.def == weapon;
    public static bool LivingHuman(Pawn pawn) => !pawn.Dead && pawn.RaceProps.Humanlike && pawn.RaceProps.IsFlesh && !SunstoneAxe.Unnatural(pawn);
    // A switched-off collar is just apparel: no suppression, no rarer rebellions and no mood.
    public static bool Collared(Pawn pawn) => Effects.DreadstoneCollar.On && pawn.IsSlaveOfColony && Wears(pawn, Collar);
    public static bool Subjugated(Pawn pawn) => MapState.Of<DreadstoneSubjugation>(pawn.MapHeld)?.Covers(pawn) == true;

    // Null without Ideology, where the collar, cudgel and knife don't exist.
    private static ThingDef collar, cudgel, knife;
    private static bool looked;
    private static void Look()
    {
        if (looked) return;
        collar = DefDatabase<ThingDef>.GetNamedSilentFail("KM_DreadstoneCollar");
        cudgel = DefDatabase<ThingDef>.GetNamedSilentFail("KM_DreadstoneCudgel");
        knife = DefDatabase<ThingDef>.GetNamedSilentFail("KM_DarkStoneRitualKnife");
        looked = true;
    }
    public static ThingDef Collar { get { Look(); return collar; } }
    public static ThingDef Cudgel { get { Look(); return cudgel; } }
    public static ThingDef Knife { get { Look(); return knife; } }

    public static bool Wears(Pawn pawn, ThingDef apparel)
    {
        var worn = pawn.apparel?.WornApparel;
        if (worn == null || apparel == null) return false;
        for (int i = 0; i < worn.Count; i++)
            if (worn[i].def == apparel) return true;
        return false;
    }

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
        // Every hit with another weapon leaves on these reference comparisons.
        var weapon = dinfo.Weapon;
        if (weapon == null || weapon != NewEquipment.Cudgel && weapon != NewEquipment.Knife) return;
        if (__result.totalDamageDealt <= 0 || __instance is not Pawn victim || !NewEquipment.LivingHuman(victim)) return;
        if (weapon == NewEquipment.Cudgel)
        {
            if (!Effects.DreadstoneCudgel.On) return;
            var agony = (Hediff_DreadAgony)NewEquipment.Stack(victim, "KM_DreadAgony", NewEquipment.AgonyCap, NewEquipment.AgonyTicks);
            agony.RememberCapture();
        }
        else
        {
            if (!Effects.RitualKnife.On) return;
            NewEquipment.Stack(victim, "KM_SoulBlight", NewEquipment.BlightCap, NewEquipment.BlightTicks);
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

// A part of the native fall-rate stat, applied after apparel: coverage wins, otherwise the collar reduces decay by a quarter.
// Added to SlaveSuppressionFallRate by Integrations/Ideology/Patches/DreadstoneCollarStats.xml.
public class StatPart_DreadstoneCollar : StatPart
{
    private static bool Applies(StatRequest req) => req.Thing is Pawn pawn && NewEquipment.Collared(pawn) && !NewEquipment.Subjugated(pawn);

    public override void TransformValue(StatRequest req, ref float val)
    {
        if (Applies(req)) val *= NewEquipment.CollarSuppression;
    }

    public override string ExplanationPart(StatRequest req) =>
        Applies(req) ? "Lumen_Collar_Section".Translate().CapitalizeFirst() + ": x" + NewEquipment.CollarSuppression.ToStringPercent() : null;
}

[HarmonyPatch(typeof(Apparel), nameof(Apparel.PawnCanWear))]
public static class DreadstoneCollarWearer
{
    public static void Postfix(Apparel __instance, Pawn pawn, ref bool __result)
    {
        if (__instance.def == NewEquipment.Collar && !pawn.IsSlaveOfColony) __result = false;
    }
}

// The execution entry point also serves altar sacrifices. Only the normal prisoner execution job pays here.
[HarmonyPatch(typeof(ExecutionUtility), nameof(ExecutionUtility.DoExecutionByCut))]
public static class RitualKnifeExecution
{
    public static void Prefix(Pawn executioner, Pawn victim, out float __state)
    {
        __state = Effects.RitualKnife.On && victim.IsPrisonerOfColony && NewEquipment.LivingHuman(victim) &&
            executioner.CurJobDef == JobDefOf.PrisonerExecution && NewEquipment.Wields(executioner, NewEquipment.Knife)
            ? AltarRules.HarvestSuccess * NewEquipment.KnifeHarvestShare * AltarRules.VictimFactor(victim.MarketValue, AltarRules.StatusOf(victim)) : 0f;
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
    private bool Staff => weapon == "KM_LumenstoneStaff";
    private bool On => (Staff ? Effects.LumenstoneStaff : Effects.RitualKnife).On;
    private float Bonus => Staff ? NewEquipment.StaffQuality : NewEquipment.KnifeQuality;
    private ThingDef weaponDef;
    private bool Equipped(Pawn pawn) => On && NewEquipment.Wields(pawn, weaponDef ??= DefDatabase<ThingDef>.GetNamedSilentFail(weapon));
    public override float Count(LordJob_Ritual ritual, RitualOutcomeComp_Data data) => Equipped(ritual.PawnWithRole("speaker")) ? 1 : 0;
    public override float QualityOffset(LordJob_Ritual ritual, RitualOutcomeComp_Data data) => Count(ritual, data) * Bonus;
    public override string GetDesc(LordJob_Ritual ritual = null, RitualOutcomeComp_Data data = null) =>
        ritual == null ? base.GetDesc(ritual, data) : "Lumen_Ritual_QualityDesc".Translate(label, QualityOffset(ritual, data).ToStringPercent()).ToString();
    public override QualityFactor GetQualityFactor(Precept_Ritual ritual, TargetInfo ritualTarget, RitualObligation obligation,
        RitualRoleAssignments assignments, RitualOutcomeComp_Data data)
    {
        bool equipped = Equipped(assignments?.FirstAssignedPawn("speaker"));
        return new QualityFactor
        {
            label = label, count = (!On ? "Lumen_Weapon_Off" : equipped ? "Lumen_Weapon_Equipped" : "Lumen_Weapon_NotEquipped").Translate(),
            qualityChange = "+" + (equipped ? Bonus : 0f).ToStringPercent("0"),
            quality = equipped ? Bonus : 0f, positive = true, priority = 3f,
            toolTip = "Lumen_Weapon_Tooltip".Translate()
        };
    }
}
