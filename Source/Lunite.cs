using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LumenStones;

// Lunite: moonlit Sunstone. Converted at night at a Lumen ritual circle or Lumenstone altar (a ritual with Ideology,
// a long job at the circle without). Sunstone is set aside at the start and destroyed only when the conversion succeeds.
public static class LuniteRules
{
    public static float LinkRadius = 4f;
    public static readonly Color Ring = new(0.69f, 0.78f, 0.96f);

    public static int SunstoneCost = 25, Yield = 25;
    public static float CooldownDays = 5f;
    public const float NightStart = 20f, NightEnd = 4f;
    // The job without Ideology: one worker at the circle for this long.
    public const int JobTicks = 4 * GenDate.TicksPerHour;

    private static ResearchProjectDef research;
    private static ThingDef sunstone, lunite;
    public static ResearchProjectDef Research => research ??= DefDatabase<ResearchProjectDef>.GetNamed("KM_LuniteStabilization");
    public static ThingDef Sunstone => sunstone ??= DefDatabase<ThingDef>.GetNamed("KM_Sunstone");
    public static ThingDef Lunite => lunite ??= DefDatabase<ThingDef>.GetNamed("KM_Lunite");

    // A conversion can only start between 20:00 and 04:00 local time. Once started it may finish after dawn.
    public static bool NightNow(Map map)
    {
        float hour = GenLocalDate.HourFloat(map);
        return hour >= NightStart || hour < NightEnd;
    }

    public static List<Thing> SunstoneStacks(Thing host) => host.Map.listerThings.ThingsOfDef(Sunstone)
        .Where(t => !t.IsForbidden(Faction.OfPlayer) &&
            host.Map.reachability.CanReach(host.Position, t, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors))).ToList();

    // Takes the Sunstone out of the map until the conversion ends. Null when there is not enough.
    public static List<Thing> SetAside(Thing host)
    {
        var held = new List<Thing>();
        int remaining = SunstoneCost;
        foreach (var thing in SunstoneStacks(host))
        {
            if (remaining == 0) break;
            int count = Math.Min(remaining, thing.stackCount);
            held.Add(thing.SplitOff(count));
            remaining -= count;
        }
        if (remaining == 0) return held;
        Return(held, host);
        return null;
    }

    public static void Return(List<Thing> held, Thing host)
    {
        foreach (var thing in held) GenPlace.TryPlaceThing(thing, host.Position, host.Map, ThingPlaceMode.Near);
        held.Clear();
    }

    // Consumes the set-aside Sunstone and delivers the Lunite. The cooldown starts here and nowhere else.
    public static void Complete(List<Thing> held, Map map, IntVec3 cell)
    {
        foreach (var thing in held) thing.Destroy();
        held.Clear();
        for (int remaining = Yield; remaining > 0;)
        {
            var stack = ThingMaker.MakeThing(Lunite);
            stack.stackCount = Math.Min(remaining, Lunite.stackLimit);
            remaining -= stack.stackCount;
            GenPlace.TryPlaceThing(stack, cell, map, ThingPlaceMode.Near);
        }
        map.GetComponent<LuniteState>().nextTick = Find.TickManager.TicksGame + Mathf.RoundToInt(CooldownDays * GenDate.TicksPerDay);
    }

    // Shared by the ritual and the job. Null when a conversion can start.
    public static string Blocked(Thing host)
    {
        if (!SolarRules.Enabled) return "Requires Sunstone content to be enabled.";
        if (host is not Building_LumenstoneRitualHost || host.Faction != Faction.OfPlayer)
            return "Requires a player-owned Lumen ritual circle" + (ModsConfig.IdeologyActive ? " or Lumenstone altar." : ".");
        if (!Research.IsFinished) return "Requires the " + Research.label + " research.";
        if (host.Map.GetComponent<LuniteState>().CooldownReason is { } cooldown) return cooldown;
        if (!NightNow(host.Map)) return "Can only start between 20:00 and 04:00.";
        if (ModsConfig.IdeologyActive && Find.IdeoManager.GetActiveRituals(host.Map).Any(r => r is LordJob_LumenstoneRitual))
            return "A ritual is already underway in this settlement.";
        if (SunstoneStacks(host).Sum(t => t.stackCount) < SunstoneCost)
            return "Requires " + SunstoneCost + " accessible, unforbidden Sunstone.";
        return null;
    }

    public static string Summary => "Turns " + SunstoneCost + " Sunstone into " + Yield + " Lunite. Starts only between 20:00 and 04:00 and may finish after dawn. " +
        "The Sunstone is spent only on success. Each settlement recovers for " + CooldownDays + " days.";

    public static Command_Action RitualCommand(Building host, string precept)
    {
        var command = new Command_Action
        {
            defaultLabel = "Lunite ritual",
            defaultDesc = Summary + " A ceremony of about 1 hour: choose participants in the native ritual dialog.",
            icon = ContentFinder<Texture2D>.Get("UI/Commands/LumenstoneRitual"),
            action = () => AttunementRules.StartRitual(host, precept)
        };
        if (Blocked(host) is { } reason) command.Disable(reason);
        return command;
    }

    public static Command_Action JobCommand(Building_LumenCircle circle)
    {
        var command = new Command_Action
        {
            defaultLabel = "Convert Lunite",
            defaultDesc = Summary + " One colonist works at the circle for " + JobTicks / GenDate.TicksPerHour + " hours.",
            icon = ContentFinder<Texture2D>.Get("UI/Commands/LumenstoneRitual"),
            action = () =>
            {
                var options = circle.Map.mapPawns.FreeColonistsSpawned
                    .Where(p => !p.Downed && !p.WorkTagIsDisabled(WorkTags.Crafting) && p.CanReach(circle, PathEndMode.InteractionCell, Danger.Deadly))
                    .Select(p => new FloatMenuOption(p.LabelShortCap, () =>
                        p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("KM_ConvertLuniteAtCircle"), circle))))
                    .ToList();
                if (options.Count == 0) Messages.Message("No colonist can work at the circle.", circle, MessageTypeDefOf.RejectInput, false);
                else Find.WindowStack.Add(new FloatMenu(options));
            }
        };
        if (Blocked(circle) is { } reason) command.Disable(reason);
        return command;
    }
}

public class LuniteState : MapComponent
{
    public int nextTick;
    public LuniteState(Map map) : base(map) { }
    public override void ExposeData() => Scribe_Values.Look(ref nextTick, "nextLuniteTick");
    public string CooldownReason => nextTick > Find.TickManager.TicksGame
        ? "Lunite conversion is recharging: " + (nextTick - Find.TickManager.TicksGame).ToStringTicksToPeriod() + " remaining (shared by every circle and altar in this settlement)." : null;
}

// Without Ideology: one colonist stands at the circle for four hours. The Sunstone is set aside once the work starts.
public class JobDriver_ConvertLuniteAtCircle : JobDriver
{
    private List<Thing> held = new();
    private bool done;
    private Thing Circle => job.targetA.Thing;

    public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(Circle, job, 1, -1, null, errorOnFailed);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref held, "luniteHeld", LookMode.Deep);
        held ??= new List<Thing>();
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(TargetIndex.A);
        this.FailOn(() => !SolarRules.Enabled);
        // Anyone else finishing first starts the cooldown, which ends this attempt without spending anything.
        this.FailOn(() => !done && held.Count == 0 && Circle.Map.GetComponent<LuniteState>().CooldownReason != null);
        AddFinishAction(_ =>
        {
            if (!done && held.Count > 0 && Circle is { Spawned: true }) LuniteRules.Return(held, Circle);
        });
        yield return Toils_Goto.GotoCell(Circle.InteractionCell, PathEndMode.OnCell);
        yield return Toils_General.Do(() =>
        {
            var set = LuniteRules.Blocked(Circle) == null ? LuniteRules.SetAside(Circle) : null;
            if (set == null)
            {
                Messages.Message("The Lunite conversion could not start: " + (LuniteRules.Blocked(Circle) ?? "Sunstone is no longer available."), Circle, MessageTypeDefOf.RejectInput, false);
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            held = set;
        });
        yield return Toils_General.Wait(LuniteRules.JobTicks).WithProgressBarToilDelay(TargetIndex.A);
        yield return Toils_General.Do(() =>
        {
            done = true;
            LuniteRules.Complete(held, Circle.Map, Circle.Position);
            Messages.Message("Converted " + LuniteRules.SunstoneCost + " Sunstone into " + LuniteRules.Yield + " Lunite.", Circle, MessageTypeDefOf.PositiveEvent);
        });
    }
}

public class RitualBehaviorWorker_Lunite : RitualBehaviorWorker
{
    public RitualBehaviorWorker_Lunite() { }
    public RitualBehaviorWorker_Lunite(RitualBehaviorDef def) : base(def) { }

    public override string CanStartRitualNow(TargetInfo target, Precept_Ritual ritual, Pawn selectedPawn = null,
        Dictionary<string, Pawn> forcedForRole = null) =>
        LuniteRules.Blocked(target.Thing) ?? base.CanStartRitualNow(target, ritual, selectedPawn, forcedForRole);

    protected override LordJob CreateLordJob(TargetInfo target, Pawn organizer, Precept_Ritual ritual,
        RitualObligation obligation, RitualRoleAssignments assignments)
    {
        var job = new LordJob_LuniteRitual(target, ritual, obligation, def.stages, assignments, assignments.AssignedPawns("speaker").First());
        job.ReserveChunks();
        return job;
    }
}

// Holds the Sunstone for the ceremony and draws it around the host like the Lumenstone ritual's chunks.
public class LordJob_LuniteRitual : LordJob_LumenstoneRitual
{
    public LordJob_LuniteRitual() { }
    public LordJob_LuniteRitual(TargetInfo target, Precept_Ritual ritual, RitualObligation obligation,
        List<RitualStage> stages, RitualRoleAssignments assignments, Pawn organizer)
        : base(target, ritual, obligation, stages, assignments, organizer) { }

    protected override int Cost => LuniteRules.SunstoneCost;
    protected override IEnumerable<Thing> Candidates => LuniteRules.SunstoneStacks(selectedTarget.Thing);
}

public class RitualOutcomeEffectWorker_Lunite : RitualOutcomeEffectWorker_FromQuality
{
    public RitualOutcomeEffectWorker_Lunite() { }
    public RitualOutcomeEffectWorker_Lunite(RitualOutcomeEffectDef def) : base(def) { }
    public override bool SupportsAttachableOutcomeEffect => false;

    protected override void ApplyExtraOutcome(Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual,
        RitualOutcomePossibility outcome, out string extraOutcomeDesc, ref LookTargets letterLookTargets)
    {
        extraOutcomeDesc = null;
        if (jobRitual is not LordJob_LuniteRitual job) return;
        var held = job.Chunks.ToList();
        job.ClearReserved();
        LuniteRules.Complete(held, job.Map, job.selectedTarget.Cell);
        extraOutcomeDesc = "The ceremony produced " + LuniteRules.Yield + " " + LuniteRules.Lunite.label + ". Lunite conversion in this settlement now recovers for " + LuniteRules.CooldownDays + " days.";
    }
}
