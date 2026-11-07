using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public enum StoneSide { Light, Dark }

public static class ImplantRules
{
    public const int PassInterval = 250;
    public static float DormantEfficiency = 0.8f;
    public static float AttunementCooldownDays = 3f, BindingCooldownDays = 3f;
    // About what a Hollowstone ward costs one pawn, so an implant weighs like a small fixture.
    public static float DrainPerDay = 3f, ReliefFactor = 0.25f, PairedBenefitFactor = 0.8f;

    public static ThingDef RawStone(StoneSide side) => DefDatabase<ThingDef>.GetNamed(side == StoneSide.Light ? "KM_Lumenstone" : "KM_DarkStone");
    public static string Label(StoneSide side) => side == StoneSide.Light ? "light" : "dark";

    public static IEnumerable<HediffComp_Attunement> Parts(Pawn pawn)
    {
        var hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
            if (hediffs[i].TryGetComp<HediffComp_Attunement>() is { } part) yield return part;
    }

    // Dormant or active: the slot is taken from installation.
    public static HediffComp_Attunement ImplantOn(Pawn pawn, StoneSide side) => Parts(pawn).FirstOrDefault(p => p.IsImplant && p.Props.side == side);

    public static bool Paired(Pawn pawn)
    {
        bool light = false, dark = false;
        foreach (var part in Parts(pawn))
        {
            if (!part.IsImplant || part.dormant) continue;
            if (part.Props.side == StoneSide.Light) light = true;
            else dark = true;
        }
        return light && dark;
    }

    // Activating or removing one implant changes the paired factor of the other.
    public static void Refresh(Pawn pawn)
    {
        foreach (var part in Parts(pawn).ToList()) part.Dirty();
    }

    // A part on this body part or any ancestor that suppresses pain there.
    public static bool Painless(Pawn pawn, BodyPartRecord part)
    {
        var hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            if (hediffs[i] is not Hediff_CrystalPart crystal || crystal.Attunement is not { dormant: false, Props.painless: true }) continue;
            for (var current = part; current != null; current = current.parent)
                if (current == crystal.Part) return true;
        }
        return false;
    }
}

// Energy relief for drawbacks. Benefits never wait on it.
public static class ImplantEnergy
{
    public static bool TryDraw(Pawn pawn, StoneSide side, float amount)
    {
        if (side == StoneSide.Light && !SolarRules.Enabled) return true;
        var map = pawn.MapHeld;
        if (map != null && map.IsPlayerHome)
            return side == StoneSide.Light ? SolarNetwork.For(map).TryConsumeContinuous(amount) : WarpEnergyNetwork.For(map).TryConsumeContinuous(amount);
        return TwinstoneCell.TryFeed(pawn, side, amount);
    }
}

public class HediffCompProperties_Attunement : HediffCompProperties
{
    public StoneSide side;
    public HediffStage benefit = new();
    // Null for light prosthetics, which have no drawback and draw nothing.
    public HediffStage drawback;
    // Used instead of drawback when Biotech is off, for drawbacks that only exist with Biotech.
    public HediffStage drawbackWithoutBiotech;
    // Vitalite with Biotech: always sterile. Energy can't relieve it, so it draws nothing for it.
    public bool sterile;
    // Verdite: a hediff the active part sustains, which carries the effects that change over time.
    public HediffDef reserve;
    // Hediffs an active part refuses, such as blinding flashes.
    public List<HediffDef> prevents;
    // An active part never causes pain on itself or the parts below it.
    public bool painless;

    public HediffCompProperties_Attunement() => compClass = typeof(HediffComp_Attunement);
}

// Shared by every implant and prosthetic: the dormant state, energy relief and the stage built from them.
public class HediffComp_Attunement : HediffComp
{
    public bool dormant = true, relieved;
    private bool paired;
    private HediffStage stage;

    public HediffCompProperties_Attunement Props => (HediffCompProperties_Attunement)props;
    public bool IsImplant => parent is not Hediff_AddedPart;
    // Only drawbacks energy can relieve count: they draw energy and can show as unrelieved.
    public bool HasDrawback => Drawback != null;
    public float BenefitFactor => paired ? ImplantRules.PairedBenefitFactor : 1f;
    private float DrawbackFactor => relieved ? 1f - ImplantRules.ReliefFactor : 1f;
    private HediffStage Drawback => ModsConfig.BiotechActive ? Props.drawback : Props.drawbackWithoutBiotech ?? Props.drawback;

    public HediffStage Stage => stage ??= Build();

    public void Dirty()
    {
        stage = null;
        // RimWorld's immutable-stat scan cannot see modifiers in our runtime-built stages.
        RecreationGain.Stat.Worker.ClearCacheForThing(Pawn);
        Pawn.health.Notify_HediffChanged(parent);
    }

    public void Activate()
    {
        dormant = false;
        relieved = false;
        if (Props.reserve != null) Hediff_SunlightReserve.Start(Pawn, Props.reserve);
        ImplantRules.Refresh(Pawn);
    }

    public void MakeDormant()
    {
        dormant = true;
        if (Props.reserve != null) Hediff_SunlightReserve.Stop(Pawn, Props.reserve);
        ImplantRules.Refresh(Pawn);
    }

    public override void CompExposeData()
    {
        Scribe_Values.Look(ref dormant, "dormant", true);
        Scribe_Values.Look(ref relieved, "relieved");
    }

    public override void CompPostPostRemoved()
    {
        RecreationGain.Stat.Worker.ClearCacheForThing(Pawn);
        if (Props.reserve != null) Hediff_SunlightReserve.Stop(Pawn, Props.reserve);
        ImplantRules.Refresh(Pawn);
    }

    public override void CompPostTickInterval(ref float severityAdjustment, int delta)
    {
        if (dormant || !Pawn.IsHashIntervalTick(ImplantRules.PassInterval, delta)) return;
        bool changed = false;
        if (HasDrawback)
        {
            bool paid = ImplantEnergy.TryDraw(Pawn, Props.side, ImplantRules.DrainPerDay * ImplantRules.PassInterval / GenDate.TicksPerDay);
            changed |= paid != relieved;
            relieved = paid;
        }
        changed |= IsImplant && ImplantRules.Paired(Pawn) != paired;
        if (changed) Dirty();
    }

    private HediffStage Build()
    {
        var built = new HediffStage();
        paired = IsImplant && !dormant && ImplantRules.Paired(Pawn);
        if (dormant)
        {
            // A dormant prosthetic works like a simple one. Parts below it are handled in CrystalPartEfficiency.
            if (!IsImplant) built.partEfficiencyOffset = ImplantRules.DormantEfficiency - parent.def.addedPartProps.partEfficiency;
            return built;
        }
        Merge(built, Props.benefit, BenefitFactor);
        var drawback = Drawback;
        if (drawback != null) Merge(built, drawback, DrawbackFactor);
        if (Props.sterile && ModsConfig.BiotechActive) built.fertilityFactor = 0f;
        return built;
    }

    private static float Scaled(float factor, float scale) => 1f + (factor - 1f) * scale;

    // Scales each effect's distance from neutral: a factor of 1.3 at 0.75 becomes 1.225, an offset of 20 becomes 15.
    private static void Merge(HediffStage into, HediffStage from, float scale)
    {
        into.painFactor *= Scaled(from.painFactor, scale);
        into.painOffset += from.painOffset * scale;
        into.totalBleedFactor *= Scaled(from.totalBleedFactor, scale);
        into.hungerRateFactor *= Scaled(from.hungerRateFactor, scale);
        into.restFallFactor *= Scaled(from.restFallFactor, scale);
        into.fertilityFactor *= Scaled(from.fertilityFactor, scale);
        into.foodPoisoningChanceFactor *= Scaled(from.foodPoisoningChanceFactor, scale);
        if (from.statOffsets != null)
            foreach (var mod in from.statOffsets) (into.statOffsets ??= new()).Add(new StatModifier { stat = mod.stat, value = mod.value * scale });
        if (from.statFactors != null)
            foreach (var mod in from.statFactors) (into.statFactors ??= new()).Add(new StatModifier { stat = mod.stat, value = Scaled(mod.value, scale) });
        if (from.capMods != null)
            foreach (var mod in from.capMods)
                into.capMods.Add(new PawnCapacityModifier { capacity = mod.capacity, offset = mod.offset * scale, postFactor = Scaled(mod.postFactor, scale) });
    }

    public override string CompLabelInBracketsExtra => dormant ? "dormant" : HasDrawback && !relieved ? "unrelieved" : null;

    public override string CompTipStringExtra
    {
        get
        {
            var text = new StringBuilder();
            string ritual = Props.side == StoneSide.Light ? "Attunement at a Lumen ritual circle or Lumenstone altar" : "Binding at a Sacrificial altar";
            if (dormant)
            {
                text.Append("Dormant: " + (IsImplant ? "no effect" : "works as a simple prosthetic") + " until woken by " + ritual + ".");
                return text.ToString();
            }
            if (HasDrawback)
            {
                string energy = Props.side == StoneSide.Light ? "Solar" : "Warp";
                text.Append(relieved
                    ? energy + " energy reduces the drawback by " + ImplantRules.ReliefFactor.ToStringPercent("0") + "."
                    : "Unrelieved: no " + energy + " energy reached it, so the drawback is at full strength.");
            }
            if (paired)
            {
                if (text.Length > 0) text.AppendLine();
                text.Append("Paired with an implant of the other side: benefits at " + ImplantRules.PairedBenefitFactor.ToStringPercent("0") + ".");
            }
            return text.Length > 0 ? text.ToString() : null;
        }
    }

    public override IEnumerable<Gizmo> CompGetGizmos()
    {
        if (!Prefs.DevMode) yield break;
        yield return new Command_Action
        {
            defaultLabel = "DEV: " + (dormant ? "activate " : "make dormant ") + parent.LabelBase,
            action = () => { if (dormant) Activate(); else MakeDormant(); }
        };
    }
}

public class Hediff_StoneImplant : Hediff_Implant
{
    private HediffComp_Attunement attunement;
    public HediffComp_Attunement Attunement => attunement ??= this.TryGetComp<HediffComp_Attunement>();
    public override HediffStage CurStage => Attunement?.Stage ?? base.CurStage;
}

public class Hediff_CrystalPart : Hediff_AddedPart
{
    private HediffComp_Attunement attunement;
    public HediffComp_Attunement Attunement => attunement ??= this.TryGetComp<HediffComp_Attunement>();
    public override HediffStage CurStage => Attunement?.Stage ?? base.CurStage;
}

// Marks crafted implant and prosthetic items, so shared settings can find their bills.
public class CrystalPartItem : DefModExtension
{
    public bool prosthetic;
}

// One light and one dark implant per pawn. The surgery shows why it can't be done instead of hiding.
public class Recipe_InstallStoneImplant : Recipe_InstallImplant
{
    private StoneSide Side => recipe.addsHediff.CompProps<HediffCompProperties_Attunement>().side;

    public override AcceptanceReport AvailableReport(Thing thing, BodyPartRecord part = null)
    {
        if (thing is Pawn pawn && ImplantRules.ImplantOn(pawn, Side) is { } taken)
            return "already has a " + ImplantRules.Label(Side) + " implant: " + taken.parent.LabelBase;
        return base.AvailableReport(thing, part);
    }

    public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
    {
        // A second queued bill can finish after the first took the slot. Return its implant rather than lose it.
        if (ImplantRules.ImplantOn(pawn, Side) != null)
        {
            if (pawn.MapHeld != null && recipe.addsHediff.spawnThingOnRemoved != null)
                GenPlace.TryPlaceThing(ThingMaker.MakeThing(recipe.addsHediff.spawnThingOnRemoved), billDoer?.Position ?? pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near);
            Messages.Message(pawn.LabelShortCap + " already has a " + ImplantRules.Label(Side) + " implant. The implant was set aside.", pawn, MessageTypeDefOf.RejectInput);
            return;
        }
        base.ApplyOnPawn(pawn, part, billDoer, ingredients, bill);
    }
}

// Removal and replacement both spawn spawnThingOnRemoved. An active part gives back one raw stone, a dormant one its item.
[HarmonyPatch]
public static class CrystalPartRemoval
{
    public static ThingDef RemovedThing(Hediff hediff) =>
        hediff.TryGetComp<HediffComp_Attunement>() is { dormant: false } part ? ImplantRules.RawStone(part.Props.side) : hediff.def.spawnThingOnRemoved;

    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(MedicalRecipesUtility), nameof(MedicalRecipesUtility.SpawnThingsFromHediffs));
        yield return AccessTools.Method(typeof(Recipe_RemoveImplant), nameof(Recipe_RemoveImplant.ApplyOnPawn));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var code = instructions.ToList();
        var def = AccessTools.Field(typeof(Hediff), nameof(Hediff.def));
        var spawn = AccessTools.Field(typeof(HediffDef), nameof(HediffDef.spawnThingOnRemoved));
        int replaced = 0;
        for (int i = 0; i + 1 < code.Count; i++)
        {
            if (!code[i].LoadsField(def) || !code[i + 1].LoadsField(spawn)) continue;
            code[i].opcode = OpCodes.Call;
            code[i].operand = AccessTools.Method(typeof(CrystalPartRemoval), nameof(RemovedThing));
            code[i + 1].opcode = OpCodes.Nop;
            code[i + 1].operand = null;
            replaced++;
        }
        if (replaced == 0)
            Log.Error("[Lumen Stones] " + original.DeclaringType?.Name + "." + original.Name + " changed; active crystal parts return their item on removal until this patch is updated.");
        return code;
    }
}

// Parts below a prosthetic take its efficiency directly, so a dormant part has to lower theirs here.
[HarmonyPatch(typeof(PawnCapacityUtility), nameof(PawnCapacityUtility.CalculatePartEfficiency))]
public static class CrystalPartEfficiency
{
    public static bool Prefix(HediffSet diffSet, BodyPartRecord part, List<PawnCapacityUtility.CapacityImpactor> impactors, ref float __result)
    {
        for (var parent = part.parent; parent != null; parent = parent.parent)
        {
            if (!diffSet.HasDirectlyAddedPartFor(parent)) continue;
            if (diffSet.GetFirstHediffMatchingPart<Hediff_AddedPart>(parent) is not Hediff_CrystalPart { Attunement.dormant: true } crystal) return true;
            impactors?.Add(new PawnCapacityUtility.CapacityImpactorHediff { hediff = crystal });
            __result = ImplantRules.DormantEfficiency;
            return false;
        }
        return true;
    }
}

[HarmonyPatch]
public static class CrystalPartPain
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(Hediff_Injury), nameof(Hediff.PainOffset));
        yield return AccessTools.PropertyGetter(typeof(Hediff_MissingPart), nameof(Hediff.PainOffset));
    }

    public static void Postfix(Hediff __instance, ref float __result)
    {
        if (__result > 0f && __instance.Part != null && ImplantRules.Painless(__instance.pawn, __instance.Part)) __result = 0f;
    }
}

[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff), typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult))]
public static class CrystalPartPrevents
{
    // Runs on every hediff added to any pawn, so it walks the list directly instead of through the Parts iterator.
    public static bool Prefix(Hediff hediff, Pawn ___pawn)
    {
        var hediffs = ___pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
            if (hediffs[i].TryGetComp<HediffComp_Attunement>() is { dormant: false, Props.prevents: { } prevents } && prevents.Contains(hediff.def)) return false;
        return true;
    }
}

// Pawns have no recreation gain stat, so implants scale joy as it is gained.
[HarmonyPatch(typeof(Need_Joy), nameof(Need_Joy.GainJoy))]
public static class RecreationGain
{
    private static StatDef stat;
    public static StatDef Stat => stat ??= DefDatabase<StatDef>.GetNamed("KM_RecreationGain");

    public static void Prefix(ref float amount, Pawn ___pawn)
    {
        // Joy is gained every tick during recreation, so the stat is read through the game's short cache.
        if (amount > 0f) amount *= ___pawn.GetStatValue(Stat, true, ImplantRules.PassInterval);
    }
}

public class ThoughtWorker_StoneImplant : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p) =>
        p.health.hediffSet.GetFirstHediffOfDef(def.hediff)?.TryGetComp<HediffComp_Attunement>() is { dormant: false } ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
}

// The mood is a benefit, so it shares the paired factor.
public class Thought_StoneImplant : Thought_Situational
{
    public override float MoodOffset() =>
        base.MoodOffset() * (pawn.health.hediffSet.GetFirstHediffOfDef(def.hediff)?.TryGetComp<HediffComp_Attunement>()?.BenefitFactor ?? 1f);
}

public class HediffCompProperties_StateIcon : HediffCompProperties
{
    public string texPath;
    public HediffCompProperties_StateIcon() => compClass = typeof(HediffComp_StateIcon);
}

// The health tab draws one state icon per row. Parts use their body-part glyph, dimmed while dormant.
public class HediffComp_StateIcon : HediffComp
{
    private static readonly Color Dormant = new(0.55f, 0.55f, 0.55f);
    private Texture2D icon;

    public override TextureAndColor CompStateIcon
    {
        get
        {
            icon ??= ContentFinder<Texture2D>.Get(((HediffCompProperties_StateIcon)props).texPath);
            bool dormant = parent.TryGetComp<HediffComp_Attunement>()?.dormant == true;
            return new TextureAndColor(icon, dormant ? Dormant : Color.white);
        }
    }
}

// Base for perks that only work while their part is active, on the shared pass.
public abstract class HediffComp_CrystalPerk : HediffComp
{
    private HediffComp_Attunement attunement;
    protected HediffComp_Attunement Attunement => attunement ??= parent.TryGetComp<HediffComp_Attunement>();
    protected static float PassDays => ImplantRules.PassInterval / (float)GenDate.TicksPerDay;

    public override void CompPostTickInterval(ref float severityAdjustment, int delta)
    {
        if (Pawn.IsHashIntervalTick(ImplantRules.PassInterval, delta) && Attunement is { dormant: false }) Pass(Attunement.BenefitFactor);
    }

    protected abstract void Pass(float benefit);
}

public class HediffCompProperties_DreadAura : HediffCompProperties
{
    public float radius = 10f;
    // The implant also works while drafted. The jaw only works in combat.
    public bool whileDrafted = true;
    public HediffCompProperties_DreadAura() => compClass = typeof(HediffComp_DreadAura);
}

// Reuses fixture Dread on hostile humanlikes near the bearer, without the Warp cost or the siphon mark.
public class HediffComp_DreadAura : HediffComp_CrystalPerk
{
    public HediffCompProperties_DreadAura Props => (HediffCompProperties_DreadAura)props;

    protected override void Pass(float benefit)
    {
        if (!Pawn.Spawned || !(Props.whileDrafted && Pawn.Drafted) && Pawn.mindState?.WasRecentlyCombatantTicks(600) != true) return;
        float range = Props.radius * benefit;
        foreach (var other in Pawn.Map.mapPawns.AllPawnsSpawned)
        {
            if (other.Dead || !other.RaceProps.Humanlike || !other.HostileTo(Pawn) || (other.Position - Pawn.Position).LengthHorizontal > range) continue;
            DreadstoneRules.ApplyDread(other, DreadstoneRules.ClusterSeverity, DreadstoneRules.DreadLingerTicks, false);
        }
    }
}

public class HediffCompProperties_ScarMending : HediffCompProperties
{
    public float daysPerScar = 5f;
    public HediffCompProperties_ScarMending() => compClass = typeof(HediffComp_ScarMending);
}

// Sanguinite regrowth's scar healing at a slow, free rate.
public class HediffComp_ScarMending : HediffComp_CrystalPerk
{
    private float progress;
    public HediffCompProperties_ScarMending Props => (HediffCompProperties_ScarMending)props;

    public override void CompExposeData() => Scribe_Values.Look(ref progress, "scarProgress");

    protected override void Pass(float benefit)
    {
        progress = Mathf.Min(1f, progress + PassDays * benefit / Props.daysPerScar);
        if (progress < 1f) return;
        // The same choice as SanguiniteTreatment.HealScar: visible permanent scars on natural parts, eyes only if blindness isn't revered.
        foreach (var hediff in Pawn.health.hediffSet.hediffs)
        {
            if (hediff is not Hediff_Injury scar || !scar.Visible || !scar.def.everCurableByItem || !scar.IsPermanent()) continue;
            if (scar.Part == null || Pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(scar.Part)) continue;
            if (scar.Part.def == BodyPartDefOf.Eye && SanguiniteRules.Blindness(Pawn)) continue;
            HealthUtility.Cure(scar);
            progress = 0f;
            return;
        }
    }
}

public class HediffCompProperties_ApparelMending : HediffCompProperties
{
    public float hitPointsPerDay = 10f;
    public HediffCompProperties_ApparelMending() => compClass = typeof(HediffComp_ApparelMending);
}

// Ferrite implant: worn apparel regains hit points, most damaged first.
public class HediffComp_ApparelMending : HediffComp_CrystalPerk
{
    private float progress;
    public HediffCompProperties_ApparelMending Props => (HediffCompProperties_ApparelMending)props;

    public override void CompExposeData() => Scribe_Values.Look(ref progress, "apparelProgress");

    protected override void Pass(float benefit)
    {
        var worn = Pawn.apparel?.WornApparel;
        Apparel target = null;
        if (worn != null)
            foreach (var apparel in worn)
                if (apparel.def.useHitPoints && apparel.HitPoints < apparel.MaxHitPoints && (target == null || apparel.HitPoints * target.MaxHitPoints < target.HitPoints * apparel.MaxHitPoints))
                    target = apparel;
        if (target == null) { progress = 0f; return; }
        progress += Props.hitPointsPerDay * PassDays * benefit;
        int whole = Mathf.FloorToInt(progress);
        if (whole <= 0) return;
        progress -= whole;
        target.HitPoints = Mathf.Min(target.MaxHitPoints, target.HitPoints + whole);
    }
}

public class HediffCompProperties_PartMending : HediffCompProperties
{
    public float healPerDay = 2f;
    public HediffCompProperties_PartMending() => compClass = typeof(HediffComp_PartMending);
}

// Ferrite limbs: fresh injuries on the prosthetic and the parts it carries close by themselves.
public class HediffComp_PartMending : HediffComp_CrystalPerk
{
    public HediffCompProperties_PartMending Props => (HediffCompProperties_PartMending)props;

    protected override void Pass(float benefit)
    {
        float amount = Props.healPerDay * PassDays * benefit;
        foreach (var hediff in Pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
        {
            if (hediff.IsPermanent() || !Under(hediff.Part)) continue;
            hediff.Heal(amount);
            return;
        }
    }

    private bool Under(BodyPartRecord part)
    {
        for (var current = part; current != null; current = current.parent)
            if (current == parent.Part) return true;
        return false;
    }
}
