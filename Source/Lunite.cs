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
    public static float NightStart = 20f, NightEnd = 4f;
    // The job without Ideology: one worker at the circle for this long.
    public const int JobTicks = 4 * GenDate.TicksPerHour;

    private static ResearchProjectDef research;
    private static ThingDef sunstone, lunite;
    internal static void DeclareTunables(TunableScope s)
    {
        var lunite = s.In("Lumen_Lunite_Section".Translate());
        lunite.Add("Lunite.sunstoneCost", "Lumen_Lunite_SunstoneCost".Translate(), SunstoneCost, v => SunstoneCost = (int)v, Limit.Integer(1));
        lunite.Add("Lunite.yield", "Lumen_Lunite_Yield".Translate(), Yield, v => Yield = (int)v, Limit.Integer(1));
        lunite.Add("Lunite.cooldownDays", "Lumen_Lunite_Cooldown".Translate(), CooldownDays, v => CooldownDays = v);
        lunite.Add("Lunite.nightStart", "Lumen_Lunite_NightStart".Translate(), NightStart, v => NightStart = v, Limit.Number(0, 24));
        lunite.Add("Lunite.nightEnd", "Lumen_Lunite_NightEnd".Translate(), NightEnd, v => NightEnd = v, Limit.Number(0, 24));
        s.In("Lumen_Lunite_StabilizerSection".Translate()).Add("Lunite.linkRadius", "Lumen_Lunite_LinkRadius".Translate(), LinkRadius, v => LinkRadius = v, Limit.Number(1, 30));
    }

    public static ResearchProjectDef Research => research ??= DefDatabase<ResearchProjectDef>.GetNamed("KM_LuniteStabilization");
    public static ThingDef Sunstone => sunstone ??= DefDatabase<ThingDef>.GetNamed("KM_Sunstone");
    public static ThingDef Lunite => lunite ??= DefDatabase<ThingDef>.GetNamed("KM_Lunite");

    // A conversion can only start inside the window, 20:00 to 04:00 local time by default. Once started it may finish after dawn.
    public static bool NightNow(Map map)
    {
        float hour = GenLocalDate.HourFloat(map);
        return NightStart <= NightEnd ? hour >= NightStart && hour < NightEnd : hour >= NightStart || hour < NightEnd;
    }

    public static string Window => "Lumen_Lunite_Window".Translate(Hour(NightStart), Hour(NightEnd));
    private static string Hour(float hour) => Mathf.FloorToInt(hour).ToString("00") + ":" + Mathf.RoundToInt(hour % 1f * 60f).ToString("00");

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
        MapState.Of<LuniteState>(map).Cooldown.Start();
    }

    // Shared by the ritual and the job. Null when a conversion can start.
    public static string Blocked(Thing host)
    {
        if (!SolarRules.Enabled) return "Lumen_Lunite_NeedsSunstone".Translate();
        if (host is not Building_LumenstoneRitualHost || host.Faction != Faction.OfPlayer)
            return (ModsConfig.IdeologyActive ? "Lumen_Lunite_NeedsHostIdeology" : "Lumen_Lunite_NeedsCircle").Translate();
        if (!Research.IsFinished) return "Lumen_NeedsResearch".Translate(Research.label);
        if (MapState.Of<LuniteState>(host.Map).Cooldown.Reason is { } cooldown) return cooldown;
        if (!NightNow(host.Map)) return "Lumen_Lunite_OnlyBetween".Translate(Window);
        if (ModsConfig.IdeologyActive && Find.IdeoManager.GetActiveRituals(host.Map).Any(r => r is LordJob_LumenstoneRitual))
            return "Lumen_Lunite_Underway".Translate();
        if (SunstoneStacks(host).Sum(t => t.stackCount) < SunstoneCost)
            return "Lumen_Lunite_NeedsSunstoneStock".Translate(SunstoneCost);
        return null;
    }

    public static string Summary => "Lumen_Lunite_Summary".Translate(SunstoneCost, Yield, Window, CooldownDays);

    public static Command_Action RitualCommand(Building_LumenstoneRitualHost host)
    {
        var command = new Command_AutoRitual("Lumen_AutoRitual_Lunite".Translate(), () => host.AutoLunite, v => host.AutoLunite = v)
        {
            defaultLabel = "Lumen_AutoRitual_Lunite".Translate().CapitalizeFirst(),
            defaultDesc = Summary + " " + "Lumen_Lunite_RitualDesc".Translate(),
            icon = ContentFinder<Texture2D>.Get("UI/Commands/LuniteRitual"),
            action = () => AttunementRules.StartRitual(host, host.LunitePrecept)
        };
        if (Blocked(host) is { } reason) command.Disable(reason);
        return command;
    }

    public static Command_Action JobCommand(Building_LumenCircle circle)
    {
        var command = new Command_Action
        {
            defaultLabel = "Lumen_Lunite_Convert".Translate(),
            defaultDesc = Summary + " " + "Lumen_Lunite_JobDesc".Translate(JobTicks / GenDate.TicksPerHour),
            icon = ContentFinder<Texture2D>.Get("UI/Commands/LuniteRitual"),
            action = () =>
            {
                var options = circle.Map.mapPawns.FreeColonistsSpawned
                    .Where(p => !p.Downed && !p.WorkTagIsDisabled(WorkTags.Crafting) && p.CanReach(circle, PathEndMode.InteractionCell, Danger.Deadly))
                    .Select(p => new FloatMenuOption(p.LabelShortCap, () =>
                        p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("KM_ConvertLuniteAtCircle"), circle))))
                    .ToList();
                if (options.Count == 0) Messages.Message("Lumen_Lunite_NoColonist".Translate(), circle, MessageTypeDefOf.RejectInput, false);
                else Find.WindowStack.Add(new FloatMenu(options));
            }
        };
        if (Blocked(circle) is { } reason) command.Disable(reason);
        return command;
    }
}

public class LuniteState : MapComponent
{
    public readonly SettlementCooldown Cooldown = new("nextLuniteTick", "Lumen_Cooldown_Lunite", () => LuniteRules.CooldownDays);
    public LuniteState(Map map) : base(map) { }
    public override void ExposeData() => Cooldown.ExposeData();
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
        this.FailOn(() => !done && held.Count == 0 && MapState.Of<LuniteState>(Circle.Map).Cooldown.Reason != null);
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
                Messages.Message("Lumen_Lunite_CouldNotStart".Translate(LuniteRules.Blocked(Circle) ?? "Lumen_Lunite_SunstoneGone".Translate()), Circle, MessageTypeDefOf.RejectInput, false);
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
            Messages.Message("Lumen_Lunite_Converted".Translate(LuniteRules.SunstoneCost, LuniteRules.Yield), Circle, MessageTypeDefOf.PositiveEvent);
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
        extraOutcomeDesc = "Lumen_Lunite_Result".Translate(LuniteRules.Yield, LuniteRules.Lunite.label, LuniteRules.CooldownDays);
    }
}
