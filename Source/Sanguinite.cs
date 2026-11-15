using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public static class SanguiniteRules
{
    public const int PassOffset = 125;
    public static float Radius = 8f;

    internal static void DeclareTunables(TunableScope s)
    {
        var reach = s.In("Lumen_Sanguinite_Treatment".Translate());
        var costs = s.In("Lumen_Sanguinite_Costs".Translate());
        var times = s.In("Lumen_Sanguinite_Times".Translate());
        reach.Add("Sanguinite.radius", "Lumen_Sanguinite_Radius".Translate(), Radius, v => Radius = v, Limit.Number(1, 100));
        costs.Add("Sanguinite.bleedCost", "Lumen_Sanguinite_BleedCost".Translate(), BleedCost, v => BleedCost = v);
        costs.Add("Sanguinite.scarCost", "Lumen_Sanguinite_Scar".Translate(), ScarCost, v => ScarCost = v);
        costs.Add("Sanguinite.digitCost", "Lumen_Sanguinite_Digit".Translate(), DigitCost, v => DigitCost = v, Limit.Number(0.01f));
        costs.Add("Sanguinite.smallPartCost", "Lumen_Sanguinite_SmallPart".Translate(), SmallPartCost, v => SmallPartCost = v, Limit.Number(0.01f));
        costs.Add("Sanguinite.organCost", "Lumen_Sanguinite_Organ".Translate(), OrganCost, v => OrganCost = v, Limit.Number(0.01f));
        times.Add("Sanguinite.digitDays", "Lumen_Sanguinite_DigitDays".Translate(), DigitDays, v => DigitDays = v, Limit.Number(0.1f));
        times.Add("Sanguinite.smallPartDays", "Lumen_Sanguinite_SmallPartDays".Translate(), SmallPartDays, v => SmallPartDays = v, Limit.Number(0.1f));
        // Declared between the regrowth times, as it always was, so the apply order is unchanged.
        SanguiniteBlade.DeclareTunables(s);
        times.Add("Sanguinite.organDays", "Lumen_Sanguinite_OrganDays".Translate(), OrganDays, v => OrganDays = v, Limit.Number(0.1f));
    }

    public static float BleedCost = 1f, ScarCost = 5f, DigitCost = 5f, SmallPartCost = 20f, OrganCost = 100f;
    public static float DigitDays = 1f, SmallPartDays = 3f, OrganDays = 7f;

    private static HediffDef regrowth;
    public static HediffDef Regrowth => regrowth ??= DefDatabase<HediffDef>.GetNamed("KM_SanguiniteRegrowth");

    private static HediffDef treatment;
    public static HediffDef Treatment => treatment ??= DefDatabase<HediffDef>.GetNamed("KM_SanguiniteTreatment");

    // The treatment's severity is the fixture tier, and its stage for that tier slows bleeding. Severity 0 means unpaid.
    public static float BleedFactor(int tier) => Treatment.stages?.LastOrDefault(stage => stage.minSeverity <= tier)?.totalBleedFactor ?? 1f;

    // Limbs are never regrown. The value stays so saved regrowths keep their kind.
    public enum PartClass { Digit, Small, Limb, Organ }

    public static PartClass Classify(BodyPartRecord part)
    {
        // Not all of these have a BodyPartDefOf entry.
        switch (part.def.defName)
        {
            case "Finger": case "Toe": return PartClass.Digit;
            case "Eye": case "Ear": case "Nose": case "Jaw": return PartClass.Small;
        }
        return part.depth == BodyPartDepth.Inside ? PartClass.Organ : PartClass.Limb;
    }

    // Column regrows digits, cluster adds the small parts, pylon adds internal organs. Nothing regrows limbs.
    public static bool Allows(int tier, PartClass kind) => kind != PartClass.Limb && (tier >= 3 || (tier == 2 ? kind <= PartClass.Small : tier == 1 && kind == PartClass.Digit));

    // Each class of regrowth has its own effect switch. Limbs never regrow.
    public static bool RegrowthOn(PartClass kind) => kind switch
    {
        PartClass.Digit => Effects.SanguiniteDigits.On,
        PartClass.Small => Effects.SanguiniteSmallParts.On,
        PartClass.Organ => Effects.SanguiniteOrgans.On,
        _ => false
    };

    public static float Cost(PartClass kind) => kind switch
    {
        PartClass.Digit => DigitCost, PartClass.Small => SmallPartCost, _ => OrganCost
    };

    public static float Days(PartClass kind) => kind switch
    {
        PartClass.Digit => DigitDays, PartClass.Small => SmallPartDays, _ => OrganDays
    };

    // Where a patient may be treated: the part is a natural one, with no bionic on it or an ancestor.
    public static bool Regrowable(Pawn pawn, BodyPartRecord part) =>
        part != null && !pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(part) &&
        !(part.def == BodyPartDefOf.Eye && Blindness(pawn));

    public static bool Blindness(Pawn pawn) => pawn.Ideo != null && IdeoUtility.IdeoApprovesOfBlindness(pawn.Ideo);
}

// Saved progress of one regrowth. Holds the target part, since the hediff itself can't sit on a missing part.
public class Hediff_SanguiniteRegrowth : Hediff
{
    public BodyPartRecord target;
    public float cost, paid;
    public SanguiniteRules.PartClass kind;

    public override string LabelInBrackets => target == null ? null : target.Label + ", " + (cost > 0 ? paid / cost : 0).ToStringPercent("0");

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_BodyParts.Look(ref target, "target");
        Scribe_Values.Look(ref cost, "cost");
        Scribe_Values.Look(ref paid, "paid");
        Scribe_Values.Look(ref kind, "kind");
    }

    // True for a limb from an older save, or once the part was replaced by a bionic or restored some other way. Warp already paid isn't refunded.
    public bool Stale
    {
        get
        {
            if (target == null || kind == SanguiniteRules.PartClass.Limb || !pawn.health.hediffSet.PartIsMissing(target)) return true;
            return !SanguiniteRules.Regrowable(pawn, target);
        }
    }

    // Pays one pass of the total, and restores the part once it's paid off.
    public void Advance(WarpEnergyNetwork warp)
    {
        float step = Mathf.Min(cost - paid, cost / (SanguiniteRules.Days(kind) * GenDate.TicksPerDay / WarpRules.PassInterval));
        if (step > 0 && !warp.TryConsumeContinuous(step)) return;
        paid += step;
        Severity = Mathf.Clamp01(paid / cost);
        if (paid < cost - 0.0001f) return;
        var part = target;
        pawn.health.RemoveHediff(this);
        pawn.health.RestorePart(part);
        Messages.Message("Lumen_Sanguinite_Regrown".Translate(pawn.LabelShortCap, part.Label), pawn, MessageTypeDefOf.PositiveEvent);
    }
}

// Treats patients in bed on the Warp pass: slows bleeding, then heals scars or advances regrowth, one at a time.
public class SanguiniteTreatment : MapComponent
{
    public SanguiniteTreatment(Map map) : base(map) { }

    public override void MapComponentTick()
    {
        if (Find.TickManager.TicksGame % WarpRules.PassInterval != SanguiniteRules.PassOffset) return;
        var cache = DarkFixtureCache.For(map);
        if (!cache.Any(DarkStoneKind.Sanguinite)) return;
        var warp = WarpEnergyNetwork.For(map);
        Pass(map.mapPawns.FreeColonistsSpawned, cache, warp);
        Pass(map.mapPawns.PrisonersOfColonySpawned, cache, warp);
        Pass(map.mapPawns.SlavesOfColonySpawned, cache, warp);
    }

    private static bool AnyRegrowthOn => Effects.SanguiniteDigits.On || Effects.SanguiniteSmallParts.On || Effects.SanguiniteOrgans.On;

    private void Pass(List<Pawn> pawns, DarkFixtureCache cache, WarpEnergyNetwork warp)
    {
        bool active = cache.AnyActive(DarkStoneKind.Sanguinite) && (Effects.SanguiniteBleeding.On || Effects.SanguiniteScars.On || AnyRegrowthOn);
        for (int i = pawns.Count - 1; i >= 0; i--)
        {
            var pawn = pawns[i];
            if (pawn.health.hediffSet.GetFirstHediffOfDef(SanguiniteRules.Regrowth) is Hediff_SanguiniteRegrowth stale && stale.Stale)
                pawn.health.RemoveHediff(stale);
            if (!active || !pawn.InBed()) continue;
            int tier = TierFor(pawn, cache);
            if (tier > 0) Treat(pawn, tier, warp);
        }
    }

    // The strongest active Sanguinite fixture in the patient's room within reach.
    private static int TierFor(Pawn pawn, DarkFixtureCache cache)
    {
        var room = pawn.CurrentBed().GetRoom();
        if (room == null) return 0;
        float range = SanguiniteRules.Radius * SanguiniteRules.Radius;
        for (int tier = DarkFixtureCache.Tiers - 1; tier > 0; tier--)
            foreach (var fixture in cache.Fixtures(DarkStoneKind.Sanguinite, tier))
                if (fixture.Active && fixture.parent.GetRoom() == room &&
                    (fixture.parent.Position - pawn.Position).LengthHorizontalSquared <= range)
                    return tier;
        return 0;
    }

    private static void Treat(Pawn pawn, int tier, WarpEnergyNetwork warp)
    {
        // Slowing a bleeding patient costs Warp energy each pass. Unpaid or switched off, the treatment drops to a stage with no bleed factor.
        bool bleeding = Effects.SanguiniteBleeding.On;
        bool paid = bleeding && (pawn.health.hediffSet.BleedRateTotal <= 0.001f || warp.TryConsumeContinuous(SanguiniteRules.BleedCost));
        Hediff_Status.Apply(pawn, SanguiniteRules.Treatment, (tier == 3 ? "Lumen_Category_Pylon" : tier == 2 ? "Lumen_Category_Cluster" : "Lumen_Category_Column").Translate(), Describe(tier),
            WarpRules.PassInterval + 50, paid ? tier : 0f);
        if (pawn.health.hediffSet.GetFirstHediffOfDef(SanguiniteRules.Regrowth) is Hediff_SanguiniteRegrowth regrowth)
        {
            // A switched-off class pauses its regrowth and keeps the progress.
            if (SanguiniteRules.Allows(tier, regrowth.kind) && SanguiniteRules.RegrowthOn(regrowth.kind)) regrowth.Advance(warp);
            return;
        }
        if (Effects.SanguiniteScars.On && HealScar(pawn, tier, warp)) return;
        if (AnyRegrowthOn) StartRegrowth(pawn, tier);
    }

    private static string Describe(int tier)
    {
        var text = new System.Text.StringBuilder();
        if (Effects.SanguiniteBleeding.On)
            text.AppendLine("Lumen_Sanguinite_BleedNote".Translate(SanguiniteRules.BleedFactor(tier).ToStringPercent("0"), SanguiniteRules.BleedCost.ToString("0.##")));
        if (Effects.SanguiniteScars.On)
            text.AppendLine((tier == 1 ? "Lumen_Sanguinite_ScarNoteSurface" : "Lumen_Sanguinite_ScarNote").Translate(SanguiniteRules.ScarCost.ToString("0.##")));
        var parts = new List<string>();
        if (Effects.SanguiniteDigits.On) parts.Add("Lumen_Sanguinite_Digits".Translate());
        if (tier >= 2 && Effects.SanguiniteSmallParts.On) parts.Add("Lumen_Sanguinite_SmallParts".Translate());
        if (tier >= 3 && Effects.SanguiniteOrgans.On) parts.Add("Lumen_Sanguinite_Organs".Translate());
        if (parts.Count > 0) text.Append("Lumen_Sanguinite_Regrows".Translate(parts.ToCommaList(true)));
        return text.ToString().TrimEndNewlines();
    }

    private static bool HealScar(Pawn pawn, int tier, WarpEnergyNetwork warp)
    {
        foreach (var hediff in pawn.health.hediffSet.hediffs)
        {
            if (hediff is not Hediff_Injury scar || !scar.Visible || !scar.def.everCurableByItem || !scar.IsPermanent()) continue;
            var part = scar.Part;
            if (part == null || pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(part)) continue;
            if (part.def == BodyPartDefOf.Eye && SanguiniteRules.Blindness(pawn)) continue;
            // A column only heals the surface: not eyes, and nothing under the skin.
            if (tier == 1 && (part.depth != BodyPartDepth.Outside || part.def == BodyPartDefOf.Eye)) continue;
            if (!warp.TryConsume(SanguiniteRules.ScarCost)) return true;
            // Never RestorePart here: it strips every bionic on the part's children.
            HealthUtility.Cure(scar);
            return true;
        }
        return false;
    }

    private static void StartRegrowth(Pawn pawn, int tier)
    {
        BodyPartRecord best = null;
        foreach (var missing in pawn.health.hediffSet.GetMissingPartsCommonAncestors())
        {
            var part = missing.Part;
            if (!SanguiniteRules.Regrowable(pawn, part) || !SanguiniteRules.Allows(tier, SanguiniteRules.Classify(part)) ||
                !SanguiniteRules.RegrowthOn(SanguiniteRules.Classify(part))) continue;
            if (best == null || SanguiniteRules.Classify(part) < SanguiniteRules.Classify(best)) best = part;
        }
        if (best == null) return;
        var kind = SanguiniteRules.Classify(best);
        var hediff = (Hediff_SanguiniteRegrowth)HediffMaker.MakeHediff(SanguiniteRules.Regrowth, pawn);
        hediff.target = best;
        hediff.kind = kind;
        hediff.cost = SanguiniteRules.Cost(kind);
        pawn.health.AddHediff(hediff);
    }
}
