using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public static class DarkSeedRules
{
    public static float Days = 4f, MaxPain = 0.72f, CutIntervalStart = 12f, CutIntervalEnd = 4f, CutSeverity = 1f, BaseYield = 12f;
    // Cuts come at CutIntervalEnd from the last stage on.
    private const float LastStage = 0.75f;
    private const int CutCheckInterval = 250;

    private static HediffDef seed;
    // Null without Ideology, where the seed doesn't exist.
    public static HediffDef Seed => seed ??= DefDatabase<HediffDef>.GetNamedSilentFail("KM_DreadstoneSeed");

    public static Hediff_DreadstoneSeed On(Pawn pawn) => Seed == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(Seed) as Hediff_DreadstoneSeed;

    public static Hediff_DreadstoneSeed Dormant(Pawn pawn) => On(pawn) is { dormant: true } found ? found : null;

    public static float CutIntervalHours(float severity) => Mathf.Lerp(CutIntervalStart, CutIntervalEnd, Mathf.InverseLerp(0f, LastStage, severity));

    public static bool CutDue(Pawn pawn, float severity, int delta) =>
        pawn.IsHashIntervalTick(CutCheckInterval, delta) && Rand.MTBEventOccurs(CutIntervalHours(severity), GenDate.TicksPerHour, CutCheckInterval);
}

// Dormant until Binding, then grows over DarkSeedRules.Days, cutting the body as it goes. At full growth it consumes the pawn for Dark Stone.
public class Hediff_DreadstoneSeed : HediffWithComps
{
    public bool dormant = true;
    private bool cutLetterSent;

    // Nothing removes the seed, and it holds at severity 0 while dormant.
    public override bool ShouldRemove => false;

    // Counted in DreadstoneSeedPain after every pain factor, so no drug or trait reduces it.
    public override float PainOffset => 0f;
    public float SeedPain => Mathf.Min(CurStage?.painOffset ?? 0f, DarkSeedRules.MaxPain);

    public override string LabelInBrackets => dormant ? "dormant" : base.LabelInBrackets;

    public override string TipStringExtra
    {
        get
        {
            string own = dormant
                ? "Dormant: it waits to be bound by Binding at a Sacrificial altar."
                : "Growth: " + Severity.ToStringPercent("0") + ". It consumes the body in " +
                  Mathf.CeilToInt((1f - Severity) * DarkSeedRules.Days * GenDate.TicksPerDay).ToStringTicksToPeriod() + " unless the pawn dies first.";
            string text = base.TipStringExtra;
            return string.IsNullOrEmpty(text) ? own : text + "\n" + own;
        }
    }

    public void Activate()
    {
        dormant = false;
        pawn.health.Notify_HediffChanged(this);
        if (pawn.IsColonist && PawnUtility.ShouldSendNotificationAbout(pawn))
            Find.LetterStack.ReceiveLetter("Dreadstone seed", pawn.LabelShortCap + " has a stake of dreadstone buried in " + pawn.Possessive() +
                " chest. It will keep " + pawn.gender.GetObjective() + " alive while it grows through " + pawn.gender.GetObjective() + ". Nothing will stop the pain.",
                LetterDefOf.NegativeEvent, pawn);
    }

    public override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        if (dormant) return;
        Severity += delta / (DarkSeedRules.Days * GenDate.TicksPerDay);
        if (Severity >= 1f)
        {
            Consume();
            return;
        }
        if (DarkSeedRules.CutDue(pawn, Severity, delta)) Cut();
    }

    private void Cut()
    {
        float damage = Rand.Range(1f, 4f) * DarkSeedRules.CutSeverity;
        var set = pawn.health.hediffSet;
        // Only parts that survive the cut: the seed never destroys a part or kills by itself.
        if (!set.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Outside)
                .Where(p => p.def != BodyPartDefOf.Eye && !p.def.tags.Contains(BodyPartTagDefOf.ConsciousnessSource) && set.GetPartHealth(p) > damage)
                .TryRandomElement(out var part)) return;
        // Added directly rather than through TakeDamage, so armor can't stop crystal growing from inside.
        var cut = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part);
        cut.Severity = damage;
        cut.sourceHediffDef = def;
        pawn.health.AddHediff(cut, part, new DamageInfo(DamageDefOf.Cut, cut.Severity, hitPart: part));
        if (cutLetterSent || !pawn.IsColonist || !PawnUtility.ShouldSendNotificationAbout(pawn)) return;
        cutLetterSent = true;
        Find.LetterStack.ReceiveLetter("Dreadstone cut", "The dreadstone seed tore through " + pawn.LabelShort + "'s " + part.Label +
            ". It will keep cutting as it grows. Tend the wounds or " + pawn.gender.GetObjective() + " will bleed out before it finishes.", LetterDefOf.NegativeEvent, pawn);
    }

    private void Consume()
    {
        var map = pawn.MapHeld;
        // Away from a map, such as in a caravan, the stone would have nowhere to fall. The seed waits at full growth until the pawn is on a map.
        if (map == null) return;
        var cell = pawn.PositionHeld;
        float factor = AltarRules.VictimFactor(pawn.GetStatValue(StatDefOf.MarketValue), AltarRules.StatusOf(pawn));
        int yield = Mathf.Max(1, Mathf.FloorToInt(DarkSeedRules.BaseYield * factor));
        bool notify = PawnUtility.ShouldSendNotificationAbout(pawn);
        string name = pawn.LabelShort;
        pawn.Strip(false);
        pawn.Kill(new DamageInfo(DefDatabase<DamageDef>.GetNamed("KM_DreadstoneSeedConsumed"), 99999f), this);
        pawn.Corpse?.Destroy();
        AltarRules.PlaceStacks(AltarRules.DarkStone, yield, cell, map);
        SacrificeThoughts.Give(map, pawn);
        if (notify)
            Find.LetterStack.ReceiveLetter("Consumed", "The dreadstone has taken everything it can from " + name + ". All that remains is " + yield + " Dark Stone.",
                LetterDefOf.NeutralEvent, new TargetInfo(cell, map));
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref dormant, "dormant", true);
        Scribe_Values.Look(ref cutLetterSent, "cutLetterSent");
    }
}

// The seed's pain is added after pain factors, so painkillers, anesthetic, Hollowstone and painless pawns all feel its full offset.
[HarmonyPatch(typeof(HediffSet), "CalculatePain")]
public static class DreadstoneSeedPain
{
    public static void Postfix(HediffSet __instance, ref float __result)
    {
        if (DarkSeedRules.Seed == null || __instance.pawn.Dead || !__instance.pawn.RaceProps.IsFlesh) return;
        if (__instance.GetFirstHediffOfDef(DarkSeedRules.Seed) is Hediff_DreadstoneSeed seed) __result = Mathf.Clamp01(__result + seed.SeedPain);
    }
}

// everCurableByItem keeps vanilla cures away. This also stops anything that calls Cure on it directly.
[HarmonyPatch(typeof(HealthUtility), nameof(HealthUtility.Cure), typeof(Hediff))]
public static class DreadstoneSeedNoCure
{
    public static bool Prefix(Hediff hediff, ref TaggedString __result)
    {
        if (hediff is not Hediff_DreadstoneSeed) return true;
        __result = "";
        return false;
    }
}
