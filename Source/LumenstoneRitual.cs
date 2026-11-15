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

    internal static void DeclareTunables(TunableScope s)
    {
        var stone = DefDatabase<ThingDef>.GetNamed("KM_Lumenstone");
        if (ModsConfig.IdeologyActive)
        {
            var ritual = s.In("Lumen_Ritual_Section".Translate());
            ritual.Add("Lumenstone.ritualPoor", "Lumen_Ritual_PoorYield".Translate(), PoorYield, v => PoorYield = (int)v, Limit.Integer(1));
            ritual.Add("Lumenstone.ritualSuccess", "Lumen_Ritual_SuccessYield".Translate(), SuccessYield, v => SuccessYield = (int)v, Limit.Integer(1));
            ritual.Add("Lumenstone.ritualExceptional", "Lumen_Ritual_ExceptionalYield".Translate(), ExceptionalYield, v => ExceptionalYield = (int)v, Limit.Integer(1));
            ritual.Add("Lumenstone.ritualCircleYield", "Lumen_Ritual_CircleYield".Translate(), CircleYieldFactor,
                v => CircleYieldFactor = v, Limit.Percent());
            ritual.Add("Lumenstone.ritualCooldown", "Lumen_Setting_CooldownDays".Translate(), CooldownDays, v => CooldownDays = v);
            ritual.Add("Lumenstone.ritualQualityCap", "Lumen_Setting_SupportQualityCap".Translate(), SupportStone.Lumenstone.Quality.Cap,
                v => SupportStone.Lumenstone.Quality.Cap = v, Limit.Percent(10));
            ritual.Add("Lumenstone.ritualYieldCap", "Lumen_Ritual_SupportYieldCap".Translate(), SupportStone.Lumenstone.Yield.Cap,
                v => SupportStone.Lumenstone.Yield.Cap = v, Limit.Percent(10));
            ritual.Add("Lumenstone.ritualSupportRadius", "Lumen_Setting_SupportRadius".Translate(), SupportStone.Lumenstone.Radius,
                v => SupportStone.Lumenstone.Radius = v, Limit.Number(1, 50));
            ritual.Add("Lumenstone.ritualChunks", "Lumen_Ritual_ChunkCost".Translate(), ChunkCost, v => ChunkCost = (int)v, Limit.Integer());

            var altar = DefDatabase<ThingDef>.GetNamed("KM_LumenstoneAltar");
            var altarCost = altar.costList.First(c => c.thingDef == stone);
            var altarGold = altar.costList.First(c => c.thingDef == ThingDefOf.Gold);
            var building = s.In("Lumen_Ritual_Altar".Translate());
            building.Add("Lumenstone.altarCost", "Lumen_Setting_LumenstoneQuantity".Translate(), altarCost.count, v => altarCost.count = (int)v, Limit.Integer());
            building.Add("Lumenstone.altarGold", "Lumen_Ritual_GoldQuantity".Translate(), altarGold.count, v => altarGold.count = (int)v, Limit.Integer());
            building.Add("Lumenstone.altarStuff", "Lumen_Setting_StoneBlockQuantity".Translate(), altar.costStuffCount, v => altar.costStuffCount = (int)v, Limit.Integer(1));
        }

        var circle = DefDatabase<ThingDef>.GetNamed("KM_LumenCircle");
        var circleCost = circle.costList.First(c => c.thingDef == stone);
        var circleRow = s.In("Lumen_Ritual_CircleSection".Translate());
        circleRow.Add("Lumenstone.circleCost", "Lumen_Setting_LumenstoneQuantity".Translate(), circleCost.count, v => circleCost.count = (int)v, Limit.Integer());
        circleRow.Add("Lumenstone.circleStuff", "Lumen_Setting_StoneBlockQuantity".Translate(), circle.costStuffCount, v => circle.costStuffCount = (int)v, Limit.Integer(1));
    }

    public static float YieldFactor(Thing host) => host is Building_LumenCircle ? CircleYieldFactor : 1f;

    // The nudge stops float error flooring an exact product one lower: 30 * 0.7f must give 21.
    public static int ScaledYield(int tierYield, float factor) => Mathf.FloorToInt(tierYield * factor + 0.001f);

    internal static List<Thing> Chunks(TargetInfo target) => target.Map.listerThings.ThingsInGroup(ThingRequestGroup.Chunk)
        .Where(t => t.def.IsWithinCategory(ThingCategoryDefOf.StoneChunks) && !t.IsForbidden(Faction.OfPlayer) &&
            target.Map.reachability.CanReach(target.Cell, t, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors))).ToList();

    internal static string Blocked(TargetInfo target)
    {
        if (!ModsConfig.IdeologyActive || target.Thing is not Building_LumenstoneRitualHost || target.Thing.Faction != Faction.OfPlayer)
            return "Lumen_Ritual_NeedsHost".Translate();
        var reason = MapState.Of<LumenstoneRitualState>(target.Map).Cooldown.Reason;
        if (reason != null) return reason;
        if (Find.IdeoManager.GetActiveRituals(target.Map).Any(r => r is LordJob_LumenstoneRitual))
            return "Lumen_Ritual_Underway".Translate();
        if (Chunks(target).Sum(t => t.stackCount) < ChunkCost)
            return "Lumen_Ritual_NeedsChunks".Translate(ChunkCost);
        return null;
    }
}

public class LumenstoneRitualState : MapComponent
{
    public readonly SettlementCooldown Cooldown = new("nextLumenstoneRitualTick", "Lumen_Cooldown_LumenstoneRitual", () => LumenstoneRitual.CooldownDays);
    public LumenstoneRitualState(Map map) : base(map) { }
    public override void ExposeData() => Cooldown.ExposeData();
}

// Re-lists IBillGiver because the base usability checks are not virtual.
public abstract class Building_LumenstoneRitualHost : Building_WorkTable, IBillGiver
{
    protected abstract string PreceptDefName { get; }
    protected abstract string RitualNote { get; }
    public string LumenstonePrecept => PreceptDefName;
    public string LunitePrecept => this is Building_LumenCircle ? "KM_LuniteRitual" : "KM_LuniteAltarRitual";
    // Auto-start toggles, set by right-clicking the ritual commands and read by AutoRitualState.
    public bool AutoLumenstone, AutoLunite;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref AutoLumenstone, "autoLumenstoneRitual");
        Scribe_Values.Look(ref AutoLunite, "autoLuniteRitual");
    }
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
        string status = MapState.Of<LumenstoneRitualState>(Map).Cooldown.Reason ?? "Lumen_Ritual_Ready".Translate();
        if (this is IRitualSupportHost) status += "\n" + "Lumen_Ritual_Fixtures".Translate(RitualSupport.Describe(this, false));
        return string.IsNullOrEmpty(text) ? status : text + "\n" + status;
    }

    private static int ShownYield(int tierYield, float factor) => LumenstoneRitual.ScaledYield(tierYield, factor);


    public override IEnumerable<Gizmo> GetGizmos()
    {
        // The native command only appears once the precept exists, and this one must not add it until it is used.
        foreach (var gizmo in base.GetGizmos())
            if (gizmo is not Command_Ritual) yield return gizmo;
        // With Ideology, attunement belongs to the altar. Without it there is no altar, so the circle keeps the job.
        if (Faction == Faction.OfPlayer && AttunementRules.Research(StoneSide.Light) != null)
        {
            if (!ModsConfig.IdeologyActive) { if (this is Building_LumenCircle circle) yield return AttunementRules.JobCommand(circle); }
            else if (this is not Building_LumenCircle) yield return AttunementRules.Command(this, StoneSide.Light, "KM_AttunementAltar");
        }
        if (Faction == Faction.OfPlayer && SolarRules.Enabled)
        {
            if (ModsConfig.IdeologyActive) yield return LuniteRules.RitualCommand(this);
            else if (this is Building_LumenCircle luniteCircle) yield return LuniteRules.JobCommand(luniteCircle);
        }
        if (!ModsConfig.IdeologyActive || Faction != Faction.OfPlayer) yield break;
        float factor = LumenstoneRitual.YieldFactor(this);
        var command = new Command_AutoRitual("Lumen_AutoRitual_Lumenstone".Translate(), () => AutoLumenstone, v => AutoLumenstone = v)
        {
            defaultLabel = "Lumen_Ritual_Label".Translate(),
            defaultDesc = "Lumen_Ritual_Desc".Translate(LumenstoneRitual.ChunkCost, ShownYield(LumenstoneRitual.PoorYield, factor),
                ShownYield(LumenstoneRitual.SuccessYield, factor), ShownYield(LumenstoneRitual.ExceptionalYield, factor), LumenstoneRitual.CooldownDays) + "\n" + RitualNote,
            icon = ContentFinder<Texture2D>.Get("UI/Commands/LumenstoneRitual"),
            action = () => AttunementRules.StartRitual(this, PreceptDefName)
        };
        string reason = LumenstoneRitual.Blocked(this);
        if (reason != null) command.Disable(reason);
        yield return command;
        if (Prefs.DevMode)
            yield return new Command_Action
            {
                defaultLabel = "DEV: reset cooldown", // not translated
                action = () => MapState.Of<LumenstoneRitualState>(Map).Cooldown.Reset()
            };
    }
}

public class Building_LumenCircle : Building_LumenstoneRitualHost
{
    protected override string PreceptDefName => "KM_LumenstoneRitual";
    protected override string RitualNote => "Lumen_Ritual_CircleNote".Translate(LumenstoneRitual.CircleYieldFactor.ToStringPercent());
    protected override Vector3 ChunkRingCenter => Vector3.zero;
    protected override float ChunkRingRadiusX => 1f;
    protected override float ChunkRingRadiusZ => 1f;
    protected override float ChunkScale => 0.75f;
}

public class Building_LumenstoneAltar : Building_LumenstoneRitualHost, IRitualSupportHost
{
    bool IRitualSupportHost.Dark => false;
    protected override string PreceptDefName => "KM_LumenstoneAltarRitual";
    protected override string RitualNote => "Lumen_Ritual_AltarNote".Translate(RitualSupport.Describe(this, false), SupportStone.Lumenstone.Radius);
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
    public override IEnumerable<string> GetTargetInfos(RitualObligation obligation) { yield return "Lumen_Ritual_Altar".Translate(); }
}

public class RitualObligationTargetFilter_LumenCircle : RitualObligationTargetFilter
{
    public RitualObligationTargetFilter_LumenCircle() { }
    public RitualObligationTargetFilter_LumenCircle(RitualObligationTargetFilterDef def) : base(def) { }
    protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation) =>
        target.Thing is Building_LumenCircle && target.Thing.Faction == Faction.OfPlayer;
    public override IEnumerable<TargetInfo> GetTargets(RitualObligation obligation, Map map) =>
        map.listerBuildings.allBuildingsColonist.OfType<Building_LumenCircle>().Select(b => new TargetInfo(b));
    public override IEnumerable<string> GetTargetInfos(RitualObligation obligation) { yield return "Lumen_Ritual_Circle".Translate(); }
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
        var reading = FixtureSupport.Of(SupportStone.Lumenstone, job.selectedTarget.Thing);
        float support = reading.Passive;
        float factor = LumenstoneRitual.YieldFactor(job.selectedTarget.Thing) * participation * (1f + reading.Bonus(SupportStone.Lumenstone.Yield));
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
        MapState.Of<LumenstoneRitualState>(job.Map).Cooldown.Start();
        extraOutcomeDesc = "Lumen_Ritual_Result".Translate(count, stone.label,
            support > 0 ? " " + "Lumen_Ritual_ResultSupport".Translate(reading.Bonus(SupportStone.Lumenstone.Yield).ToStringPercent()).ToString() : "", LumenstoneRitual.CooldownDays);
    }
}
