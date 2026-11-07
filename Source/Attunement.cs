using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LumenStones;

// Attunement wakes light parts at a Lumen ritual circle or Lumenstone altar; Binding wakes dark parts at a Sacrificial altar.
public static class AttunementRules
{
    // The job without Ideology: an attuner and the recipient spend this long at the circle.
    public const int JobTicks = 4 * GenDate.TicksPerHour;

    private static HediffDef sickness;
    public static HediffDef Sickness => sickness ??= DefDatabase<HediffDef>.GetNamed("KM_AttunementSickness");

    public static string RitualName(StoneSide side) => side == StoneSide.Light ? "Attunement" : "Binding";

    public static ResearchProjectDef Research(StoneSide side) =>
        DefDatabase<ResearchProjectDef>.GetNamedSilentFail(side == StoneSide.Light ? "KM_LumenAttunement" : "KM_DarkAttunement");

    public static List<HediffComp_Attunement> Dormant(Pawn pawn, StoneSide side) =>
        ImplantRules.Parts(pawn).Where(p => p.dormant && p.Props.side == side).ToList();

    // A dormant Dreadstone seed can be bound without the research or the cooldown. Other parts need both.
    public static bool PartsAllowed(Map map, StoneSide side) =>
        map != null && Research(side)?.IsFinished == true && map.GetComponent<AttunementState>().CooldownReason(side) == null;

    private static Hediff_DreadstoneSeed Seed(Pawn pawn, StoneSide side) => side == StoneSide.Dark ? DarkSeedRules.Dormant(pawn) : null;

    public static bool CanReceive(Pawn pawn, StoneSide side) => pawn.RaceProps.Humanlike && !pawn.Dead &&
        (pawn.IsColonist || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony) &&
        (Seed(pawn, side) != null || PartsAllowed(pawn.MapHeld, side) && Dormant(pawn, side).Count > 0);

    public static IEnumerable<Pawn> Recipients(Map map, StoneSide side) => map.mapPawns.FreeColonistsSpawned
        .Concat(map.mapPawns.PrisonersOfColonySpawned).Concat(map.mapPawns.SlavesOfColonySpawned).Distinct().Where(p => CanReceive(p, side));

    // Shared by both rituals and the job. Null when it can start.
    public static string Blocked(Thing host, StoneSide side)
    {
        bool rightHost = side == StoneSide.Light ? host is Building_LumenstoneRitualHost : host is Building_SacrificialAltar;
        if (!rightHost || host.Faction != Faction.OfPlayer) return "Requires a player-owned " + (side == StoneSide.Light ? "Lumen ritual circle or Lumenstone altar." : "Sacrificial altar.");
        if (ModsConfig.IdeologyActive && Find.IdeoManager.GetActiveRituals(host.Map).Any(r => r.Ritual?.behavior is RitualBehaviorWorker_Attunement worker && worker.Side == side))
            return "Another " + RitualName(side) + " is already underway in this settlement.";
        // Recipients already account for the research and cooldown, so those are only the reason when nobody qualifies.
        if (Recipients(host.Map, side).Any()) return null;
        var research = Research(side);
        if (research == null || !research.IsFinished) return "Requires the " + (research?.label ?? "attunement") + " research.";
        return host.Map.GetComponent<AttunementState>().CooldownReason(side) ??
            "Requires a colonist, prisoner or slave with a dormant " + ImplantRules.Label(side) + " implant or prosthetic.";
    }

    // Wakes up to count random dormant parts of that side, with a dormant Dreadstone seed as one more candidate. Returns their labels.
    // counted is false when only the seed could be or was woken: that starts no cooldown and causes no sickness.
    public static List<string> Activate(Pawn recipient, StoneSide side, int count, bool sick, out bool counted)
    {
        var parts = PartsAllowed(recipient.MapHeld, side) ? Dormant(recipient, side) : new List<HediffComp_Attunement>();
        var seed = Seed(recipient, side);
        var candidates = parts.Select(p => (Hediff)p.parent).ToList();
        if (seed != null) candidates.Add(seed);
        var picked = candidates.InRandomOrder().Take(count).ToList();
        foreach (var hediff in picked)
        {
            if (hediff == seed) seed.Activate();
            else hediff.TryGetComp<HediffComp_Attunement>().Activate();
        }
        counted = parts.Count > 0 && !(picked.Count == 1 && picked[0] == seed);
        if (sick && counted) recipient.health.AddHediff(Sickness);
        return picked.Select(h => h.LabelBase).ToList();
    }

    public static string Describe(Pawn recipient, List<string> woken) => woken.Count == 0
        ? "Nothing woke in " + recipient.LabelShort + ". The parts stay dormant and can be tried again."
        : recipient.LabelShortCap + "'s " + woken.ToCommaList(true) + (woken.Count == 1 ? " is" : " are") + " now active.";

    public static Command_Action Command(Building host, StoneSide side, string precept)
    {
        var command = new Command_Action
        {
            defaultLabel = RitualName(side),
            defaultDesc = (side == StoneSide.Light
                    ? "Wake dormant light implants and prosthetics. "
                    : "Wake dormant dark implants and prosthetics. ") +
                "An attuner works on a colonist, prisoner or slave. The attuner's Medicine skill, participants and ritual support raise quality. " +
                "A poor outcome wakes nothing, a successful one wakes 1 part, good wakes 2 and exceptional wakes 3 without attunement sickness. " +
                "Each settlement recovers for " + (side == StoneSide.Light ? ImplantRules.AttunementCooldownDays : ImplantRules.BindingCooldownDays) + " days." +
                (side == StoneSide.Dark && DarkSeedRules.Seed != null
                    ? " A dormant dreadstone seed can be bound at any time. Waking only the seed starts no recovery and causes no sickness."
                    : ""),
            icon = ContentFinder<Texture2D>.Get(side == StoneSide.Light ? "UI/Icons/Attunement" : "UI/Icons/Binding"),
            action = () => StartRitual(host, precept)
        };
        string reason = Blocked(host, side);
        if (reason != null) command.Disable(reason);
        return command;
    }

    public static void StartRitual(Building host, string precept)
    {
        var ideo = Faction.OfPlayer.ideos.PrimaryIdeo;
        var def = DefDatabase<PreceptDef>.GetNamed(precept);
        var ritual = ideo.PreceptsListForReading.OfType<Precept_Ritual>().FirstOrDefault(p => p.def == def);
        if (ritual == null)
        {
            ritual = (Precept_Ritual)PreceptMaker.MakePrecept(def);
            ideo.AddPrecept(ritual, init: true, fillWith: def.ritualPatternBase);
        }
        string blocked = ritual.behavior.CanStartRitualNow(host, ritual);
        if (blocked != null) Messages.Message(blocked, host, MessageTypeDefOf.RejectInput, false);
        else ritual.ShowRitualBeginWindow(host);
    }

    // Without Ideology: pick a recipient, then an attuner, and order the job at the circle.
    public static Command_Action JobCommand(Building_LumenCircle circle)
    {
        var command = new Command_Action
        {
            defaultLabel = "Attune a part",
            defaultDesc = "An attuner and the recipient spend " + JobTicks / GenDate.TicksPerHour + " hours at this circle to wake one dormant light implant or prosthetic, " +
                "with attunement sickness afterwards. Each settlement recovers for " + ImplantRules.AttunementCooldownDays + " days.",
            icon = ContentFinder<Texture2D>.Get("UI/Icons/Attunement"),
            action = () => Find.WindowStack.Add(new FloatMenu(Recipients(circle.Map, StoneSide.Light).Select(recipient =>
                new FloatMenuOption(recipient.LabelShortCap, () => ChooseAttuner(circle, recipient))).ToList()))
        };
        string reason = Blocked(circle, StoneSide.Light);
        if (reason != null) command.Disable(reason);
        return command;
    }

    private static void ChooseAttuner(Building_LumenCircle circle, Pawn recipient)
    {
        var options = circle.Map.mapPawns.FreeColonistsSpawned
            .Where(p => p != recipient && !p.Downed && !p.WorkTagIsDisabled(WorkTags.Caring) && p.CanReach(recipient, PathEndMode.Touch, Danger.Deadly))
            .Select(attuner => new FloatMenuOption(attuner.LabelShortCap + " (Medicine " + attuner.skills.GetSkill(SkillDefOf.Medicine).Level + ")", () =>
                attuner.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("KM_AttuneAtCircle"), circle, recipient, circle.Position))))
            .ToList();
        if (options.Count == 0) Messages.Message("No colonist can attune " + recipient.LabelShort + ".", circle, MessageTypeDefOf.RejectInput, false);
        else Find.WindowStack.Add(new FloatMenu(options));
    }
}

// Per map and per ritual. Separate from the Lumenstone ritual and Crystallize.
public class AttunementState : MapComponent
{
    public int nextAttunementTick, nextBindingTick;
    public AttunementState(Map map) : base(map) { }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref nextAttunementTick, "nextAttunementTick");
        Scribe_Values.Look(ref nextBindingTick, "nextBindingTick");
    }

    public string CooldownReason(StoneSide side)
    {
        int remaining = (side == StoneSide.Light ? nextAttunementTick : nextBindingTick) - Find.TickManager.TicksGame;
        return remaining > 0 ? AttunementRules.RitualName(side) + " is recharging: " + remaining.ToStringTicksToPeriod() +
            " remaining (shared by every " + (side == StoneSide.Light ? "circle and altar" : "altar") + " in this settlement)." : null;
    }

    public void StartCooldown(StoneSide side)
    {
        float days = side == StoneSide.Light ? ImplantRules.AttunementCooldownDays : ImplantRules.BindingCooldownDays;
        int next = Find.TickManager.TicksGame + Mathf.RoundToInt(days * GenDate.TicksPerDay);
        if (side == StoneSide.Light) nextAttunementTick = next;
        else nextBindingTick = next;
    }
}

// A colonist, prisoner or slave with at least one dormant part of the ritual's side.
public class RitualRoleAttunementRecipient : RitualRole
{
    public StoneSide side;

    public override bool AppliesToRole(Precept_Role role, out string reason, Precept_Ritual ritual = null, Pawn p = null, bool skipReason = false)
    {
        reason = null;
        return false;
    }

    public override bool AppliesToPawn(Pawn p, out string reason, TargetInfo selectedTarget, LordJob_Ritual ritual = null,
        RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
    {
        reason = null;
        if (!AppliesIfChild(p, out reason, skipReason)) return false;
        if (!AttunementRules.CanReceive(p, side))
        {
            reason = skipReason ? null : "Must be a colonist, prisoner or slave with a dormant " + ImplantRules.Label(side) + " part.";
            return false;
        }
        var attuner = ritual != null ? ritual.PawnWithRole("attuner") : assignments?.FirstAssignedPawn("attuner");
        if (attuner == p)
        {
            reason = skipReason ? null : "Cannot be the attuner.";
            return false;
        }
        return true;
    }
}

public class RitualBehaviorWorker_Attunement : RitualBehaviorWorker
{
    public RitualBehaviorWorker_Attunement() { }
    public RitualBehaviorWorker_Attunement(RitualBehaviorDef def) : base(def) { }

    public StoneSide Side => def.roles.OfType<RitualRoleAttunementRecipient>().First().side;

    public override string CanStartRitualNow(TargetInfo target, Precept_Ritual ritual, Pawn selectedPawn = null,
        Dictionary<string, Pawn> forcedForRole = null) =>
        AttunementRules.Blocked(target.Thing, Side) ?? base.CanStartRitualNow(target, ritual, selectedPawn, forcedForRole);

    public override void Cleanup(LordJob_Ritual ritual)
    {
        base.Cleanup(ritual);
        // Lord.Cleanup releases pawns between Cleanup and PostCleanup, and a released prisoner picks its next job then.
        // The grace must already be set, or a prisoner near the map edge starts escaping (as vanilla's Trial avoids).
        var recipient = ritual.PawnWithRole("recipient");
        if (recipient != null && !recipient.Dead && recipient.IsPrisonerOfColony) recipient.guest.WaitInsteadOfEscapingFor(1250);
    }

    public override void PostCleanup(LordJob_Ritual ritual)
    {
        base.PostCleanup(ritual);
        // A prisoner left on the circle or altar goes back to bed.
        var recipient = ritual.PawnWithRole("recipient");
        var attuner = ritual.PawnWithRole("attuner");
        if (recipient == null || recipient.Dead || !recipient.IsPrisonerOfColony || attuner == null) return;
        WorkGiver_Warden_TakeToBed.TryTakePrisonerToBed(recipient, attuner);
    }
}

public class RitualOutcomeEffectWorker_Attunement : RitualOutcomeEffectWorker_FromQuality
{
    public RitualOutcomeEffectWorker_Attunement() { }
    public RitualOutcomeEffectWorker_Attunement(RitualOutcomeEffectDef def) : base(def) { }
    public override bool SupportsAttachableOutcomeEffect => false;

    protected override void ApplyExtraOutcome(Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual,
        RitualOutcomePossibility outcome, out string extraOutcomeDesc, ref LookTargets letterLookTargets)
    {
        extraOutcomeDesc = null;
        var recipient = jobRitual.PawnWithRole("recipient");
        if (recipient == null || jobRitual.Ritual?.behavior is not RitualBehaviorWorker_Attunement worker) return;
        var side = worker.Side;
        // positivityIndex is the number of parts woken: -1 poor, then 1, 2 and 3. Only exceptional spares the sickness.
        int count = Mathf.Max(0, outcome.positivityIndex);
        var woken = AttunementRules.Activate(recipient, side, count, outcome.positivityIndex < 3, out bool counted);
        extraOutcomeDesc = AttunementRules.Describe(recipient, woken);
        if (!counted) return;
        jobRitual.Map.GetComponent<AttunementState>().StartCooldown(side);
        float days = side == StoneSide.Light ? ImplantRules.AttunementCooldownDays : ImplantRules.BindingCooldownDays;
        extraOutcomeDesc += " " + AttunementRules.RitualName(side) + " in this settlement now recovers for " + days + " days.";
    }
}

// Without Ideology: bring the recipient onto the circle, keep them there and attune for four hours.
// A colonist or slave who can walk goes there themselves; prisoners and downed pawns are carried.
public class JobDriver_AttuneAtCircle : JobDriver
{
    private const TargetIndex CircleIndex = TargetIndex.A, RecipientIndex = TargetIndex.B, CellIndex = TargetIndex.C;
    private Thing Circle => job.GetTarget(CircleIndex).Thing;
    private Pawn Recipient => (Pawn)job.GetTarget(RecipientIndex).Thing;
    private AttunementState State => Circle.Map.GetComponent<AttunementState>();
    private bool Walks => !Recipient.Downed && !Recipient.IsPrisonerOfColony && Recipient.CanReach(Circle.Position, PathEndMode.OnCell, Danger.Deadly);

    public override bool TryMakePreToilReservations(bool errorOnFailed) =>
        pawn.Reserve(Recipient, job, 1, -1, null, errorOnFailed) && pawn.Reserve(Circle, job, 1, -1, null, errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(CircleIndex);
        this.FailOnDestroyedOrNull(RecipientIndex);
        this.FailOnAggroMentalStateAndHostile(RecipientIndex);
        this.FailOn(() => !AttunementRules.CanReceive(Recipient, StoneSide.Light));
        // Another attuner may finish first and start the cooldown while this job is under way.
        this.FailOn(() => State.CooldownReason(StoneSide.Light) != null);
        var attune = Toils_General.Wait(AttunementRules.JobTicks).WithProgressBarToilDelay(CircleIndex)
            .FailOn(() => Recipient.Position != Circle.Position);
        var carry = Toils_Goto.GotoThing(RecipientIndex, PathEndMode.OnCell).FailOnSomeonePhysicallyInteracting(RecipientIndex);
        var hold = Toils_General.Do(() => Recipient.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture, AttunementRules.JobTicks + 600), JobCondition.InterruptForced));
        yield return Toils_Jump.JumpIf(attune, () => Recipient.Position == Circle.Position && pawn.Position == Circle.InteractionCell);
        yield return Toils_Jump.JumpIf(carry, () => !Walks);
        yield return Toils_General.Do(() => Recipient.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, Circle.Position), JobCondition.InterruptForced));
        var arrive = ToilMaker.MakeToil("WaitForRecipient");
        arrive.defaultCompleteMode = ToilCompleteMode.Never;
        arrive.tickAction = () => { if (Recipient.Position == Circle.Position) ReadyForNextToil(); };
        yield return arrive.FailOn(() => Recipient.Position != Circle.Position && Recipient.CurJobDef != JobDefOf.Goto);
        yield return Toils_Jump.Jump(hold);
        yield return carry;
        yield return Toils_Haul.StartCarryThing(RecipientIndex);
        yield return Toils_Goto.GotoCell(CellIndex, PathEndMode.OnCell);
        yield return Toils_Haul.PlaceHauledThingInCell(CellIndex, null, storageMode: false);
        yield return hold;
        yield return Toils_Goto.GotoCell(Circle.InteractionCell, PathEndMode.OnCell);
        yield return attune;
        yield return Toils_General.Do(() =>
        {
            var woken = AttunementRules.Activate(Recipient, StoneSide.Light, 1, true, out _);
            State.StartCooldown(StoneSide.Light);
            Recipient.jobs.EndCurrentJob(JobCondition.Succeeded);
            Messages.Message(AttunementRules.Describe(Recipient, woken), Recipient, MessageTypeDefOf.PositiveEvent);
        });
    }
}
