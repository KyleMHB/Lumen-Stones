using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LumenStones;

public static class LumenstoneRitual
{
    public static int ChunkCost = 5, PoorYield = 18, SuccessYield = 25, ExceptionalYield = 30;
    public static float CooldownDays = 5, CircleYieldFactor = 0.7f;

    public static float YieldFactor(Thing host) => host is Building_LumenCircle ? CircleYieldFactor : 1f;

    // The nudge stops float error flooring an exact product one lower: 30 * 0.7f must give 21.
    public static int ScaledYield(int tierYield, float factor) => Mathf.FloorToInt(tierYield * factor + 0.001f);

    internal static List<Thing> Chunks(TargetInfo target) => target.Map.listerThings.ThingsInGroup(ThingRequestGroup.Chunk)
        .Where(t => t.def.IsWithinCategory(ThingCategoryDefOf.StoneChunks) && !t.IsForbidden(Faction.OfPlayer) &&
            target.Map.reachability.CanReach(target.Cell, t, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors))).ToList();

    internal static string Blocked(TargetInfo target)
    {
        if (!ModsConfig.IdeologyActive || target.Thing is not Building_LumenstoneRitualHost || target.Thing.Faction != Faction.OfPlayer)
            return "Requires a player-owned Lumen ritual circle or Lumenstone altar, and Ideology.";
        var reason = target.Map.GetComponent<LumenstoneRitualState>().CooldownReason;
        if (reason != null) return reason;
        if (Find.IdeoManager.GetActiveRituals(target.Map).Any(r => r is LordJob_LumenstoneRitual))
            return "Another Lumenstone ritual is already underway in this settlement.";
        if (Chunks(target).Sum(t => t.stackCount) < ChunkCost)
            return "Requires " + ChunkCost + " accessible, unforbidden stone chunks.";
        return null;
    }
}

public class LumenstoneRitualState : MapComponent
{
    public int nextRitualTick;
    public LumenstoneRitualState(Map map) : base(map) { }
    public override void ExposeData() => Scribe_Values.Look(ref nextRitualTick, "nextLumenstoneRitualTick");
    public string CooldownReason => nextRitualTick > Find.TickManager.TicksGame
        ? "The Lumenstone ritual is recharging: " + (nextRitualTick - Find.TickManager.TicksGame).ToStringTicksToPeriod() + " remaining (shared by every circle and altar in this settlement)." : null;
}

// Re-lists IBillGiver because the base usability checks are not virtual.
public abstract class Building_LumenstoneRitualHost : Building_WorkTable, IBillGiver
{
    protected abstract string PreceptDefName { get; }
    protected abstract string RitualNote { get; }
    // Reserved chunks are held off the map during a ceremony, so they are drawn on this ellipse around the host.
    protected abstract Vector3 ChunkRingCenter { get; }
    protected abstract float ChunkRingRadiusX { get; }
    protected abstract float ChunkRingRadiusZ { get; }
    protected abstract float ChunkScale { get; }

    private bool CeremonyRunning => ModsConfig.IdeologyActive && Spawned &&
        Find.IdeoManager.GetActiveRituals(Map).Any(r => r is LordJob_LumenstoneRitual && r.selectedTarget.Thing == this);

    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        base.DrawAt(drawLoc, flip);
        if (!ModsConfig.IdeologyActive) return;
        var job = Find.IdeoManager.GetActiveRituals(Map).OfType<LordJob_LumenstoneRitual>().FirstOrDefault(r => r.selectedTarget.Thing == this);
        if (job == null) return;
        var chunks = job.Chunks.ToList();
        var center = drawLoc + ChunkRingCenter;
        center.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
        for (int i = 0; i < chunks.Count; i++)
        {
            float angle = Mathf.PI * 2f * i / chunks.Count;
            var position = center + new Vector3(Mathf.Sin(angle) * ChunkRingRadiusX, i * 0.001f, Mathf.Cos(angle) * ChunkRingRadiusZ);
            var material = chunks[i].Graphic.MatAt(Rot4.North, chunks[i]);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(position, Quaternion.identity, new Vector3(ChunkScale, 1f, ChunkScale)), material, 0);
        }
    }

    bool IBillGiver.CurrentlyUsableForBills() => !CeremonyRunning && CurrentlyUsableForBills();
    bool IBillGiver.UsableForBillsAfterFueling() => !CeremonyRunning && UsableForBillsAfterFueling();

    public override string GetInspectString()
    {
        string text = base.GetInspectString();
        if (!ModsConfig.IdeologyActive) return text;
        string status = Map.GetComponent<LumenstoneRitualState>().CooldownReason ?? "Lumenstone ritual ready.";
        if (this is IRitualSupportHost) status += "\nLumenstone fixtures: " + RitualSupport.Describe(this, false);
        return string.IsNullOrEmpty(text) ? status : text + "\n" + status;
    }

    private static int ShownYield(int tierYield, float factor) => LumenstoneRitual.ScaledYield(tierYield, factor);


    public override IEnumerable<Gizmo> GetGizmos()
    {
        // The native command only appears once the precept exists, and this one must not add it until it is used.
        foreach (var gizmo in base.GetGizmos())
            if (gizmo is not Command_Ritual) yield return gizmo;
        if (Faction == Faction.OfPlayer && AttunementRules.Research(StoneSide.Light) != null)
        {
            if (ModsConfig.IdeologyActive)
                yield return AttunementRules.Command(this, StoneSide.Light, this is Building_LumenCircle ? "KM_AttunementCircle" : "KM_AttunementAltar");
            else if (this is Building_LumenCircle circle) yield return AttunementRules.JobCommand(circle);
        }
        if (Faction == Faction.OfPlayer && SolarRules.Enabled)
        {
            if (ModsConfig.IdeologyActive) yield return LuniteRules.RitualCommand(this, this is Building_LumenCircle ? "KM_LuniteRitual" : "KM_LuniteAltarRitual");
            else if (this is Building_LumenCircle luniteCircle) yield return LuniteRules.JobCommand(luniteCircle);
        }
        if (!ModsConfig.IdeologyActive || Faction != Faction.OfPlayer) yield break;
        float factor = LumenstoneRitual.YieldFactor(this);
        var command = new Command_Action
        {
            defaultLabel = "Lumenstone ritual",
            defaultDesc = "Choose participants in the native ritual dialog. The ceremony lasts 5000 ticks and uses " + LumenstoneRitual.ChunkCost +
                " stone chunks. Quality yields " + ShownYield(LumenstoneRitual.PoorYield, factor) + "/" + ShownYield(LumenstoneRitual.SuccessYield, factor) + "/" +
                ShownYield(LumenstoneRitual.ExceptionalYield, factor) +
                " Lumenstone, scaled by participation (one: 50%, two: 75%, leader plus two others: 100%). Each settlement recovers for " +
                LumenstoneRitual.CooldownDays + " days.\n" + RitualNote,
            icon = ContentFinder<Texture2D>.Get("UI/Commands/LumenstoneRitual"),
            action = () =>
            {
                var ideo = Faction.OfPlayer.ideos.PrimaryIdeo;
                var def = DefDatabase<PreceptDef>.GetNamed(PreceptDefName);
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
        };
        string reason = LumenstoneRitual.Blocked(this);
        if (reason != null) command.Disable(reason);
        yield return command;
        if (Prefs.DevMode)
            yield return new Command_Action
            {
                defaultLabel = "DEV: reset cooldown",
                action = () => Map.GetComponent<LumenstoneRitualState>().nextRitualTick = 0
            };
    }
}

public class Building_LumenCircle : Building_LumenstoneRitualHost
{
    protected override string PreceptDefName => "KM_LumenstoneRitual";
    protected override string RitualNote => "A circle yields " + LumenstoneRitual.CircleYieldFactor.ToStringPercent() +
        " of an altar's Lumenstone and gains nothing from its room or from Lumenstone fixtures.";
    protected override Vector3 ChunkRingCenter => Vector3.zero;
    protected override float ChunkRingRadiusX => 1f;
    protected override float ChunkRingRadiusZ => 1f;
    protected override float ChunkScale => 0.75f;
}

public class Building_LumenstoneAltar : Building_LumenstoneRitualHost, IRitualSupportHost
{
    bool IRitualSupportHost.Dark => false;
    protected override string PreceptDefName => "KM_LumenstoneAltarRitual";
    protected override string RitualNote => RitualSupport.Describe(this, false) + ". Lumenstone fixtures within " +
        LumenstoneSupport.RitualSupportRadius + " tiles in this room raise quality and yield.";
    // Between the central crystal and the gold inlay of the sprite's slab, which sits above the texture's centre.
    protected override Vector3 ChunkRingCenter => new(0f, 0f, 0.21f);
    protected override float ChunkRingRadiusX => 0.65f;
    protected override float ChunkRingRadiusZ => 0.49f;
    protected override float ChunkScale => 0.6f;
}

public class RitualObligationTargetFilter_LumenstoneAltar : RitualObligationTargetFilter
{
    public RitualObligationTargetFilter_LumenstoneAltar() { }
    public RitualObligationTargetFilter_LumenstoneAltar(RitualObligationTargetFilterDef def) : base(def) { }
    protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation) =>
        target.Thing is Building_LumenstoneAltar && target.Thing.Faction == Faction.OfPlayer;
    public override IEnumerable<TargetInfo> GetTargets(RitualObligation obligation, Map map) =>
        map.listerBuildings.allBuildingsColonist.OfType<Building_LumenstoneAltar>().Select(b => new TargetInfo(b));
    public override IEnumerable<string> GetTargetInfos(RitualObligation obligation) { yield return "Lumenstone altar"; }
}

public class RitualObligationTargetFilter_LumenCircle : RitualObligationTargetFilter
{
    public RitualObligationTargetFilter_LumenCircle() { }
    public RitualObligationTargetFilter_LumenCircle(RitualObligationTargetFilterDef def) : base(def) { }
    protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation) =>
        target.Thing is Building_LumenCircle && target.Thing.Faction == Faction.OfPlayer;
    public override IEnumerable<TargetInfo> GetTargets(RitualObligation obligation, Map map) =>
        map.listerBuildings.allBuildingsColonist.OfType<Building_LumenCircle>().Select(b => new TargetInfo(b));
    public override IEnumerable<string> GetTargetInfos(RitualObligation obligation) { yield return "Lumen ritual circle"; }
}

public class RitualBehaviorWorker_Lumenstone : RitualBehaviorWorker
{
    public RitualBehaviorWorker_Lumenstone() { }
    public RitualBehaviorWorker_Lumenstone(RitualBehaviorDef def) : base(def) { }

    public override string CanStartRitualNow(TargetInfo target, Precept_Ritual ritual, Pawn selectedPawn = null,
        Dictionary<string, Pawn> forcedForRole = null) =>
        LumenstoneRitual.Blocked(target) ?? base.CanStartRitualNow(target, ritual, selectedPawn, forcedForRole);

    protected override LordJob CreateLordJob(TargetInfo target, Pawn organizer, Precept_Ritual ritual,
        RitualObligation obligation, RitualRoleAssignments assignments)
    {
        var job = new LordJob_LumenstoneRitual(target, ritual, obligation, def.stages, assignments,
            assignments.AssignedPawns("speaker").First());
        job.ReserveChunks();
        return job;
    }
}

public class LordJob_LumenstoneRitual : LordJob_Ritual
{
    private List<Thing> chunks = new();
    public LordJob_LumenstoneRitual() { }
    public LordJob_LumenstoneRitual(TargetInfo target, Precept_Ritual ritual, RitualObligation obligation,
        List<RitualStage> stages, RitualRoleAssignments assignments, Pawn organizer)
        : base(target, ritual, obligation, stages, assignments, organizer) { }

    protected virtual int Cost => LumenstoneRitual.ChunkCost;
    protected virtual IEnumerable<Thing> Candidates => LumenstoneRitual.Chunks(selectedTarget);

    public void ReserveChunks()
    {
        // Hold the payment for the ceremony; cancellation returns it to the circle.
        int remaining = Cost;
        foreach (var thing in Candidates)
        {
            if (remaining == 0) break;
            int count = Math.Min(remaining, thing.stackCount);
            chunks.Add(thing.SplitOff(count));
            remaining -= count;
        }
    }

    public IEnumerable<Thing> Chunks => chunks;

    // Hands the held stacks to the caller, which destroys or uses them.
    public void ClearReserved() => chunks.Clear();

    public void ConsumeChunks()
    {
        foreach (var thing in chunks) thing.Destroy();
        chunks.Clear();
    }

    public override void Cleanup()
    {
        foreach (var thing in chunks)
            GenPlace.TryPlaceThing(thing, selectedTarget.Cell, selectedTarget.Map, ThingPlaceMode.Near);
        chunks.Clear();
        base.Cleanup();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref chunks, "lumenstoneChunks", LookMode.Deep);
    }
}

public class RitualOutcomeEffectWorker_Lumenstone : RitualOutcomeEffectWorker_FromQuality
{
    public RitualOutcomeEffectWorker_Lumenstone() { }
    public RitualOutcomeEffectWorker_Lumenstone(RitualOutcomeEffectDef def) : base(def) { }
    public override bool SupportsAttachableOutcomeEffect => false;

    protected override void ApplyExtraOutcome(Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual,
        RitualOutcomePossibility outcome, out string extraOutcomeDesc, ref LookTargets letterLookTargets)
    {
        extraOutcomeDesc = null;
        if (jobRitual is not LordJob_LumenstoneRitual job) return;
        int qualityYield = outcome.positivityIndex < 0 ? LumenstoneRitual.PoorYield :
            outcome.positivityIndex > 1 ? LumenstoneRitual.ExceptionalYield : LumenstoneRitual.SuccessYield;
        int participants = totalPresence.Count(p => p.Value > 0);
        var leader = job.PawnWithRole("speaker");
        bool leaderPresent = leader != null && totalPresence.ContainsKey(leader);
        float participation = participants == 1 ? 0.5f : participants == 2 || !leaderPresent ? 0.75f : 1f;
        // Sampled once here, so a fixture removed before the ceremony ends no longer counts.
        float support = RitualSupport.Fraction(job.selectedTarget.Thing as Building, false);
        float factor = LumenstoneRitual.YieldFactor(job.selectedTarget.Thing) * participation * (1f + support * LumenstoneSupport.RitualYieldCap);
        int count = Math.Max(1, LumenstoneRitual.ScaledYield(qualityYield, factor));
        job.ConsumeChunks();
        var stone = DefDatabase<ThingDef>.GetNamed("KM_Lumenstone");
        for (int remaining = count; remaining > 0;)
        {
            var stack = ThingMaker.MakeThing(stone);
            stack.stackCount = Math.Min(remaining, stone.stackLimit);
            remaining -= stack.stackCount;
            GenPlace.TryPlaceThing(stack, job.selectedTarget.Cell, job.Map, ThingPlaceMode.Near);
        }
        job.Map.GetComponent<LumenstoneRitualState>().nextRitualTick = Find.TickManager.TicksGame + Mathf.RoundToInt(LumenstoneRitual.CooldownDays * GenDate.TicksPerDay);
        extraOutcomeDesc = "The ceremony produced " + count + " " + stone.label + (support > 0 ? " (nearby fixtures: +" + (support * LumenstoneSupport.RitualYieldCap).ToStringPercent() + ")" : "") + ". Lumenstone rituals in this settlement now recover for " + LumenstoneRitual.CooldownDays + " days.";
    }
}
