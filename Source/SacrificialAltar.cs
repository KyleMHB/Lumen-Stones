using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LumenStones;

public static class AltarRules
{
    public static int CrystallizeInput = 20;
    public static float CrystallizePoor = 0.25f, CrystallizeSuccess = 0.50f, CrystallizeExceptional = 0.75f, CrystallizeCooldownDays = 3f;
    public static float HarvestPoor = 50f, HarvestSuccess = 100f, HarvestExceptional = 150f, HarvestCooldownDays = 0.25f;
    public static float LinkRadius = 15f, StatusSlave = 1.25f, StatusColonist = 1.5f;

    // First proposal: pawn market values were not measured, so these points need tuning against real prisoners.
    private static readonly SimpleCurve VictimCurve = new()
    {
        new CurvePoint(0f, 0.5f), new CurvePoint(1000f, 1f), new CurvePoint(2000f, 1.25f)
    };

    public static float VictimFactor(float marketValue, float status) => VictimCurve.Evaluate(marketValue * status);

    public static float StatusOf(Pawn pawn) => pawn.IsPrisonerOfColony ? 1f : pawn.IsSlaveOfColony ? StatusSlave : StatusColonist;

    public static ThingDef DarkStone => DefDatabase<ThingDef>.GetNamed("KM_DarkStone");

    public static void PlaceStacks(ThingDef def, int count, IntVec3 cell, Map map)
    {
        for (int remaining = count; remaining > 0;)
        {
            var stack = ThingMaker.MakeThing(def);
            stack.stackCount = Math.Min(remaining, def.stackLimit);
            remaining -= stack.stackCount;
            GenPlace.TryPlaceThing(stack, cell, map, ThingPlaceMode.Near);
        }
    }

    internal static List<Thing> Lumenstone(TargetInfo target) => target.Map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("KM_Lumenstone"))
        .Where(t => !t.IsForbidden(Faction.OfPlayer) &&
            target.Map.reachability.CanReach(target.Cell, t, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors))).ToList();

    // Returns why a sacrifice ritual cannot start at this altar, or null.
    internal static string Blocked(TargetInfo target, bool harvest)
    {
        if (!ModsConfig.IdeologyActive || target.Thing is not Building_SacrificialAltar altar || altar.Faction != Faction.OfPlayer)
            return "Requires a player-owned Sacrificial altar and Ideology.";
        var reason = target.Map.GetComponent<SacrificeRitualState>().CooldownReason(harvest);
        if (reason != null) return reason;
        if (Find.IdeoManager.GetActiveRituals(target.Map).Any(r => r is LordJob_SacrificeRitual))
            return "Another sacrifice is already underway in this settlement.";
        if (harvest)
        {
            if (target.Map.GetComponent<WarpEnergyNetwork>().InRangeOf(altar).Count == 0)
                return "Requires a Bloodstone battery within " + LinkRadius + " tiles of the altar.";
        }
        else if (Lumenstone(target).Sum(t => t.stackCount) < CrystallizeInput)
            return "Requires " + CrystallizeInput + " accessible, unforbidden Lumenstone.";
        return null;
    }
}

public class SacrificeRitualState : MapComponent
{
    public int nextCrystallizeTick, nextHarvestTick, nextRaiseTick;
    public SacrificeRitualState(Map map) : base(map) { }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref nextCrystallizeTick, "nextCrystallizeTick");
        Scribe_Values.Look(ref nextHarvestTick, "nextHarvestTick");
        Scribe_Values.Look(ref nextRaiseTick, "nextRaiseTick");
    }

    public string CooldownReason(bool harvest)
    {
        int remaining = (harvest ? nextHarvestTick : nextCrystallizeTick) - Find.TickManager.TicksGame;
        return remaining > 0 ? (harvest ? "Harvest Warp Energy" : "Crystallize Dark Stone") + " is recharging: " +
            remaining.ToStringTicksToPeriod() + " remaining (shared by every altar in this settlement)." : null;
    }

    public string RaiseCooldownReason()
    {
        int remaining = nextRaiseTick - Find.TickManager.TicksGame;
        return remaining > 0 ? "Raise a work husk is recharging: " + remaining.ToStringTicksToPeriod() + " remaining (shared by every altar in this settlement)." : null;
    }

    public void StartRaiseCooldown() => nextRaiseTick = Find.TickManager.TicksGame + Mathf.RoundToInt(HuskRules.RaiseCooldownDays * GenDate.TicksPerDay);

    public void StartCooldown(bool harvest)
    {
        int next = Find.TickManager.TicksGame + Mathf.RoundToInt((harvest ? AltarRules.HarvestCooldownDays : AltarRules.CrystallizeCooldownDays) * GenDate.TicksPerDay);
        if (harvest) nextHarvestTick = next;
        else nextCrystallizeTick = next;
    }
}

// Re-lists IBillGiver because the base usability checks are not virtual.
public class Building_SacrificialAltar : Building_WorkTable, IBillGiver, IRitualSupportHost
{
    bool IRitualSupportHost.Dark => true;

    private bool CeremonyRunning => ModsConfig.IdeologyActive && Spawned &&
        Find.IdeoManager.GetActiveRituals(Map).Any(r => r is LordJob_SacrificeRitual && r.selectedTarget.Thing == this);

    // Reserved Lumenstone is held off the map during a crystallize ceremony, so it is drawn on the slab instead.
    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        base.DrawAt(drawLoc, flip);
        if (!ModsConfig.IdeologyActive) return;
        var job = Find.IdeoManager.GetActiveRituals(Map).OfType<LordJob_SacrificeRitual>().FirstOrDefault(r => r.selectedTarget.Thing == this);
        if (job == null) return;
        var stacks = job.Reserved.ToList();
        var position = drawLoc + new Vector3(-0.25f * (stacks.Count - 1), 0f, 0.6f);
        position.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
        foreach (var thing in stacks)
        {
            var material = thing.Graphic.MatAt(Rot4.North, thing);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(position, Quaternion.identity, new Vector3(1.2f, 1f, 1.2f)), material, 0);
            position.x += 0.5f;
            position.y += 0.001f;
        }
    }

    bool IBillGiver.CurrentlyUsableForBills() => !CeremonyRunning && CurrentlyUsableForBills();
    bool IBillGiver.UsableForBillsAfterFueling() => !CeremonyRunning && UsableForBillsAfterFueling();

    public override string GetInspectString()
    {
        string text = base.GetInspectString();
        if (!ModsConfig.IdeologyActive) return text;
        var state = Map.GetComponent<SacrificeRitualState>();
        var network = Map.GetComponent<WarpEnergyNetwork>();
        string status = (state.CooldownReason(false) ?? "Crystallize Dark Stone ready.") + "\n" +
            (state.CooldownReason(true) ?? "Harvest Warp Energy ready.") + "\nBatteries in range: " +
            network.InRangeOf(this).Count + " (" + network.SpareInRange(this).ToString("0") + " spare capacity)\nDark Stone fixtures: " +
            RitualSupport.Describe(this, true);
        return string.IsNullOrEmpty(text) ? status : text + "\n" + status;
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (var gizmo in base.GetGizmos())
            if (gizmo is not Command_Ritual) yield return gizmo;
        if (!ModsConfig.IdeologyActive || Faction != Faction.OfPlayer) yield break;
        yield return RitualCommand(false);
        yield return RitualCommand(true);
        if (HuskRules.Available) yield return RaiseCommand();
        yield return AttunementRules.Command(this, StoneSide.Dark, "KM_Binding");
        if (Prefs.DevMode)
            yield return new Command_Action
            {
                defaultLabel = "DEV: reset cooldowns",
                action = () =>
                {
                    var state = Map.GetComponent<SacrificeRitualState>();
                    state.nextCrystallizeTick = state.nextHarvestTick = 0;
                }
            };
    }

    private Command_Action RitualCommand(bool harvest)
    {
        string precept = harvest ? "KM_HarvestWarpEnergy" : "KM_CrystallizeDarkStone";
        var command = new Command_Action
        {
            defaultLabel = harvest ? "Harvest Warp Energy" : "Crystallize Dark Stone",
            defaultDesc = (harvest
                ? "Sacrifice a prisoner, slave or colonist to charge Bloodstone batteries within " + AltarRules.LinkRadius + " tiles. Output is " +
                    AltarRules.HarvestPoor + "/" + AltarRules.HarvestSuccess + "/" + AltarRules.HarvestExceptional +
                    " Warp energy by outcome, scaled by the victim's value and status. Space in range: " +
                    Map.GetComponent<WarpEnergyNetwork>().SpareInRange(this).ToString("0") + "; any excess is lost. Recovers for " +
                    (AltarRules.HarvestCooldownDays * 24f).ToString("0.#") + " hours."
                : "Sacrifice a prisoner, slave or colonist to convert " + AltarRules.CrystallizeInput + " Lumenstone into Dark Stone by outcome and victim. " +
                    "Unconverted Lumenstone is returned. Recovers for " + AltarRules.CrystallizeCooldownDays + " days.") +
                "\n" + RitualSupport.Describe(this, true) + ". Dark Stone fixtures within " + DarkStoneSupport.RitualSupportRadius +
                " tiles in this room raise quality and output.",
            icon = ContentFinder<Texture2D>.Get(harvest ? "UI/Commands/HarvestWarpEnergy" : "UI/Commands/CrystallizeDarkStone"),
            action = () => StartRitual(precept)
        };
        string reason = AltarRules.Blocked(this, harvest);
        if (reason != null) command.Disable(reason);
        return command;
    }

    private Command_Action RaiseCommand()
    {
        var command = new Command_Action
        {
            defaultLabel = "Raise a work husk",
            defaultDesc = "Sacrifice a prisoner, slave or colonist to raise them as a work husk bound to a Hollowstone pylon. Costs " +
                HuskRules.RaiseCost.ToString("0") + " Warp energy, refunded if the ceremony is cancelled. The husk costs " +
                HuskRules.HuskCostPerDay.ToString("0") + " Warp energy a day. Recovers for " + (HuskRules.RaiseCooldownDays * 24f).ToString("0.#") + " hours.",
            icon = DefDatabase<ThingDef>.GetNamed("KM_HollowstonePylon").uiIcon,
            action = () => StartRitual("KM_RaiseWorkHusk")
        };
        string reason = HuskRules.Blocked(this);
        if (reason != null) command.Disable(reason);
        return command;
    }

    private void StartRitual(string precept)
    {
        var ideo = Faction.OfPlayer.ideos.PrimaryIdeo;
        var def = DefDatabase<PreceptDef>.GetNamed(precept);
        var ritual = ideo.PreceptsListForReading.OfType<Precept_Ritual>().FirstOrDefault(p => p.def == def);
        if (ritual == null)
        {
            ritual = (Precept_Ritual)PreceptMaker.MakePrecept(def);
            ideo.AddPrecept(ritual, init: true, fillWith: def.ritualPatternBase);
        }
        string blocked = ritual.behavior.CanStartRitualNow(this, ritual);
        if (blocked != null) Messages.Message(blocked, this, MessageTypeDefOf.RejectInput, false);
        else ritual.ShowRitualBeginWindow(this);
    }
}

public class RitualObligationTargetFilter_SacrificialAltar : RitualObligationTargetFilter
{
    public RitualObligationTargetFilter_SacrificialAltar() { }
    public RitualObligationTargetFilter_SacrificialAltar(RitualObligationTargetFilterDef def) : base(def) { }
    protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation) =>
        target.Thing is Building_SacrificialAltar && target.Thing.Faction == Faction.OfPlayer;
    public override IEnumerable<TargetInfo> GetTargets(RitualObligation obligation, Map map) =>
        map.listerBuildings.allBuildingsColonist.OfType<Building_SacrificialAltar>().Select(b => new TargetInfo(b));
    public override IEnumerable<string> GetTargetInfos(RitualObligation obligation) { yield return "Sacrificial altar"; }
}

// Vanilla roles split prisoners, slaves and free colonists, so none accepts all three.
public class RitualRoleSacrificeVictim : RitualRole
{
    public override bool AppliesToRole(Precept_Role role, out string reason, Precept_Ritual ritual = null, Pawn p = null, bool skipReason = false)
    {
        reason = null;
        return false;
    }

    public override bool AppliesToPawn(Pawn p, out string reason, TargetInfo selectedTarget, LordJob_Ritual ritual = null,
        RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
    {
        if (!AppliesIfChild(p, out reason, skipReason)) return false;
        if (!p.RaceProps.Humanlike || !(p.IsPrisonerOfColony || p.IsSlaveOfColony || p.IsColonist))
        {
            reason = skipReason ? null : "Must be a prisoner, slave or colonist.";
            return false;
        }
        var leader = ritual != null ? ritual.PawnWithRole("speaker") : assignments?.FirstAssignedPawn("speaker");
        if (leader == p)
        {
            reason = skipReason ? null : "Cannot be the ceremony leader.";
            return false;
        }
        reason = null;
        return true;
    }
}

public abstract class RitualBehaviorWorker_Sacrifice : RitualBehaviorWorker
{
    protected RitualBehaviorWorker_Sacrifice() { }
    protected RitualBehaviorWorker_Sacrifice(RitualBehaviorDef def) : base(def) { }
    protected abstract bool Harvest { get; }
    // The raise ritual takes Warp energy instead of Lumenstone, and has its own cooldown and requirements.
    protected virtual bool Raise => false;

    public override string CanStartRitualNow(TargetInfo target, Precept_Ritual ritual, Pawn selectedPawn = null,
        Dictionary<string, Pawn> forcedForRole = null) =>
        (Raise ? HuskRules.Blocked(target) : AltarRules.Blocked(target, Harvest)) ?? base.CanStartRitualNow(target, ritual, selectedPawn, forcedForRole);

    protected override LordJob CreateLordJob(TargetInfo target, Pawn organizer, Precept_Ritual ritual,
        RitualObligation obligation, RitualRoleAssignments assignments)
    {
        var job = new LordJob_SacrificeRitual(target, ritual, obligation, def.stages, assignments,
            assignments.AssignedPawns("speaker").First(), Harvest);
        job.Snapshot(assignments.AssignedPawns("victim").First());
        if (Raise) job.PayRaise();
        else if (!Harvest) job.ReserveLumenstone();
        return job;
    }

    public override void Cleanup(LordJob_Ritual ritual)
    {
        base.Cleanup(ritual);
        // Set the grace before Lord.Cleanup releases pawns; a released prisoner picks its next job before PostCleanup runs.
        var victim = ritual.PawnWithRole("victim");
        if (victim != null && !victim.Dead && victim.IsPrisonerOfColony) victim.guest.WaitInsteadOfEscapingFor(1250);
    }

    public override void PostCleanup(LordJob_Ritual ritual)
    {
        base.PostCleanup(ritual);
        // A cancelled ceremony must not leave a living prisoner on the altar.
        var victim = ritual.PawnWithRole("victim");
        var warden = ritual.PawnWithRole("speaker");
        if (victim == null || victim.Dead || !victim.IsPrisonerOfColony || warden == null) return;
        WorkGiver_Warden_TakeToBed.TryTakePrisonerToBed(victim, warden);
    }
}

public class RitualBehaviorWorker_Crystallize : RitualBehaviorWorker_Sacrifice
{
    public RitualBehaviorWorker_Crystallize() { }
    public RitualBehaviorWorker_Crystallize(RitualBehaviorDef def) : base(def) { }
    protected override bool Harvest => false;
}

public class RitualBehaviorWorker_Harvest : RitualBehaviorWorker_Sacrifice
{
    public RitualBehaviorWorker_Harvest() { }
    public RitualBehaviorWorker_Harvest(RitualBehaviorDef def) : base(def) { }
    protected override bool Harvest => true;
}

public class LordJob_SacrificeRitual : LordJob_Ritual
{
    public bool harvest;
    // The dead victim can't supply these later, so they are taken at ritual start.
    private float victimMarketValue, victimStatus = 1f;
    private List<Thing> reserved = new();
    private bool thoughtsGiven;
    // The raise ritual pays up front and refunds on any end that did not raise a husk.
    private float raisePaid;
    public bool raised;

    public LordJob_SacrificeRitual() { }
    public LordJob_SacrificeRitual(TargetInfo target, Precept_Ritual ritual, RitualObligation obligation,
        List<RitualStage> stages, RitualRoleAssignments assignments, Pawn organizer, bool harvest)
        : base(target, ritual, obligation, stages, assignments, organizer) => this.harvest = harvest;

    public float VictimFactor => AltarRules.VictimFactor(victimMarketValue, victimStatus);

    public void Snapshot(Pawn victim)
    {
        victimMarketValue = victim.GetStatValue(StatDefOf.MarketValue);
        victimStatus = AltarRules.StatusOf(victim);
    }

    public void ReserveLumenstone()
    {
        // Hold the payment for the ceremony; cancellation returns it to the altar.
        int remaining = AltarRules.CrystallizeInput;
        foreach (var thing in AltarRules.Lumenstone(selectedTarget))
        {
            if (remaining == 0) break;
            int count = Math.Min(remaining, thing.stackCount);
            reserved.Add(thing.SplitOff(count));
            remaining -= count;
        }
    }

    public void PayRaise()
    {
        if (WarpEnergyNetwork.For(selectedTarget.Map).TryConsume(HuskRules.RaiseCost)) raisePaid = HuskRules.RaiseCost;
    }

    public IEnumerable<Thing> Reserved => reserved;

    public void ConsumeReserved()
    {
        foreach (var thing in reserved) thing.Destroy();
        reserved.Clear();
    }

    public override void LordJobTick()
    {
        base.LordJobTick();
        var victim = PawnWithRole("victim");
        if (thoughtsGiven || victim == null || !victim.Dead) return;
        thoughtsGiven = true;
        SacrificeThoughts.Give(Map, victim);
    }

    public override void Cleanup()
    {
        foreach (var thing in reserved)
            GenPlace.TryPlaceThing(thing, selectedTarget.Cell, selectedTarget.Map, ThingPlaceMode.Near);
        reserved.Clear();
        if (raisePaid > 0 && !raised) Map.GetComponent<WarpEnergyNetwork>().DepositAnywhere(raisePaid);
        raisePaid = 0;
        base.Cleanup();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref harvest, "sacrificeHarvest");
        Scribe_Values.Look(ref victimMarketValue, "victimMarketValue");
        Scribe_Values.Look(ref victimStatus, "victimStatus", 1f);
        Scribe_Values.Look(ref thoughtsGiven, "sacrificeThoughtsGiven");
        Scribe_Values.Look(ref raisePaid, "raisePaid");
        Scribe_Values.Look(ref raised, "raised");
        Scribe_Collections.Look(ref reserved, "sacrificeReserved", LookMode.Deep);
    }
}

public static class SacrificeThoughts
{
    // Vanilla execution events already fire from JobDriver_Sacrifice. This adds the altar's own reaction on top.
    public static void Give(Map map, Pawn victim)
    {
        foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
        {
            if (pawn == victim) continue;
            string thought = ThoughtFor(pawn);
            if (thought != null) pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed(thought));
        }
    }

    private static string ThoughtFor(Pawn pawn)
    {
        if (pawn.story?.traits?.HasTrait(TraitDefOf.Psychopath) == true) return null;
        if (pawn.story?.traits?.HasTrait(TraitDefOf.Bloodlust) == true || HasPrecept(pawn, "Execution_Required")) return "KM_WarpSacrificeApproved";
        // A sacrifice victim may be innocent, so a precept that respects guilty executions is neither upset nor pleased.
        if (HasPrecept(pawn, "Execution_RespectedIfGuilty")) return null;
        return "KM_WarpSacrifice";
    }

    private static bool HasPrecept(Pawn pawn, string defName)
    {
        var def = DefDatabase<PreceptDef>.GetNamedSilentFail(defName);
        return def != null && pawn.Ideo?.HasPrecept(def) == true;
    }
}

public abstract class RitualOutcomeEffectWorker_Sacrifice : RitualOutcomeEffectWorker_FromQuality
{
    protected RitualOutcomeEffectWorker_Sacrifice() { }
    protected RitualOutcomeEffectWorker_Sacrifice(RitualOutcomeEffectDef def) : base(def) { }
    public override bool SupportsAttachableOutcomeEffect => false;

    protected static int Tier(RitualOutcomePossibility outcome) => outcome.positivityIndex < 0 ? 0 : outcome.positivityIndex > 1 ? 2 : 1;
    protected abstract string Resolve(LordJob_SacrificeRitual job, int tier);

    protected static string SupportNote(float support) =>
        support > 0 ? " (nearby Dark Stone fixtures: +" + (support * DarkStoneSupport.RitualYieldCap).ToStringPercent() + ")" : "";

    protected override void ApplyExtraOutcome(Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual,
        RitualOutcomePossibility outcome, out string extraOutcomeDesc, ref LookTargets letterLookTargets)
    {
        extraOutcomeDesc = jobRitual is LordJob_SacrificeRitual job ? Resolve(job, Tier(outcome)) : null;
    }
}

public class RitualOutcomeEffectWorker_Crystallize : RitualOutcomeEffectWorker_Sacrifice
{
    public RitualOutcomeEffectWorker_Crystallize() { }
    public RitualOutcomeEffectWorker_Crystallize(RitualOutcomeEffectDef def) : base(def) { }

    protected override string Resolve(LordJob_SacrificeRitual job, int tier)
    {
        int input = AltarRules.CrystallizeInput;
        float share = tier == 0 ? AltarRules.CrystallizePoor : tier == 2 ? AltarRules.CrystallizeExceptional : AltarRules.CrystallizeSuccess;
        int baseConverted = Math.Clamp(Mathf.FloorToInt(input * share * job.VictimFactor), 1, input);
        // Sampled once here, so a fixture removed before the ceremony ends no longer counts.
        float support = RitualSupport.Fraction(job.selectedTarget.Thing as Building, true);
        int converted = Math.Clamp(Mathf.FloorToInt(baseConverted * (1f + support * DarkStoneSupport.RitualYieldCap)), 1, input);
        job.ConsumeReserved();
        var cell = job.selectedTarget.Cell;
        AltarRules.PlaceStacks(AltarRules.DarkStone, converted, cell, job.Map);
        if (converted < input) AltarRules.PlaceStacks(DefDatabase<ThingDef>.GetNamed("KM_Lumenstone"), input - converted, cell, job.Map);
        job.Map.GetComponent<SacrificeRitualState>().StartCooldown(false);
        return "The ceremony turned " + converted + " of " + input + " Lumenstone into Dark Stone and returned " + (input - converted) +
            " Lumenstone" + SupportNote(support) + ". Crystallizing now recovers for " + AltarRules.CrystallizeCooldownDays + " days.";
    }
}

public class RitualOutcomeEffectWorker_Harvest : RitualOutcomeEffectWorker_Sacrifice
{
    public RitualOutcomeEffectWorker_Harvest() { }
    public RitualOutcomeEffectWorker_Harvest(RitualOutcomeEffectDef def) : base(def) { }

    protected override string Resolve(LordJob_SacrificeRitual job, int tier)
    {
        float baseWarp = tier == 0 ? AltarRules.HarvestPoor : tier == 2 ? AltarRules.HarvestExceptional : AltarRules.HarvestSuccess;
        float support = RitualSupport.Fraction(job.selectedTarget.Thing as Building, true);
        float knifeFactor = NewEquipment.Wields(job.PawnWithRole("speaker"), "KM_DarkStoneRitualKnife") ? 1.15f : 1f;
        float energy = baseWarp * job.VictimFactor * (1f + support * DarkStoneSupport.RitualYieldCap) * knifeFactor;
        float deposited = job.Map.GetComponent<WarpEnergyNetwork>().Deposit(energy, job.selectedTarget.Thing as Building);
        job.Map.GetComponent<SacrificeRitualState>().StartCooldown(true);
        return "The ceremony harvested " + energy.ToString("0") + " Warp energy: " + deposited.ToString("0") + " stored in batteries and " +
            (energy - deposited).ToString("0") + " lost" + SupportNote(support) + ". Harvesting now recovers for " + (AltarRules.HarvestCooldownDays * 24f).ToString("0.#") + " hours.";
    }
}

// Informational only: shows battery space in the native ritual dialog so overfilling is visible before the ceremony.
public class RitualOutcomeComp_WarpCapacity : RitualOutcomeComp_Quality
{
    public override float Count(LordJob_Ritual ritual, RitualOutcomeComp_Data data) => 0;
    public override float QualityOffset(LordJob_Ritual ritual, RitualOutcomeComp_Data data) => 0;

    public override QualityFactor GetQualityFactor(Precept_Ritual ritual, TargetInfo ritualTarget, RitualObligation obligation,
        RitualRoleAssignments assignments, RitualOutcomeComp_Data data)
    {
        float spare = ritualTarget.Thing != null && ritualTarget.Map != null ? ritualTarget.Map.GetComponent<WarpEnergyNetwork>().SpareInRange(ritualTarget.Thing) : 0;
        return new QualityFactor
        {
            label = label,
            count = spare.ToString("0") + " free",
            qualityChange = "",
            positive = spare > 0,
            quality = 0f,
            priority = 5f,
            toolTip = "Spare capacity in Bloodstone batteries within " + AltarRules.LinkRadius + " tiles. A harvest larger than this loses the excess, and still goes ahead."
        };
    }
}

// The victim lies on the altar's centre cell, which the 3x3 footprint now has.
public class RitualPosition_AltarCenter : RitualPosition
{
    public override PawnStagePosition GetCell(IntVec3 spot, Pawn p, LordJob_Ritual ritual) => new PawnStagePosition(spot, null, Rot4.Invalid, highlight);
}
