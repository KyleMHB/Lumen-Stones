using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

// Everything here needs Ideology and Anomaly. The defs load only with both, so each entry point checks Available first.
public static class HuskRules
{
    public static float RaiseCost = 50f, HuskCostPerDay = 10f, RaiseCooldownDays = 0.25f;
    // Severity picks the hediff stage: below 1 the husk works, from 1 it has collapsed.
    public const float BoundSeverity = 0.5f, CollapsedSeverity = 1.5f;

    public static bool Available => ModsConfig.AnomalyActive && DefDatabase<MutantDef>.GetNamedSilentFail("KM_WorkHusk") != null;
    public static MutantDef Def => DefDatabase<MutantDef>.GetNamed("KM_WorkHusk");
    public static HediffDef Hediff => DefDatabase<HediffDef>.GetNamed("KM_WorkHusk");

    public static bool Collapsed(Pawn husk) => husk.health.hediffSet.GetFirstHediffOfDef(Hediff)?.Severity >= 1f;

    public static void SetCollapsed(Pawn husk, bool collapsed)
    {
        var hediff = husk.health.hediffSet.GetFirstHediffOfDef(Hediff);
        if (hediff == null) return;
        float severity = collapsed ? CollapsedSeverity : BoundSeverity;
        if (!Mathf.Approximately(hediff.Severity, severity)) hediff.Severity = severity;
    }

    public static IEnumerable<CompHollowstonePylon> Pylons(Map map)
    {
        foreach (var fixture in DarkFixtureCache.For(map).Fixtures(DarkStoneKind.Hollowstone, 3))
            if (fixture.parent.GetComp<CompHollowstonePylon>() is { } pylon) yield return pylon;
    }

    // A pylon that can take a husk now: unbound, switched on and powered.
    public static CompHollowstonePylon FreePylon(Map map)
    {
        foreach (var pylon in Pylons(map))
            if (pylon.Husk == null && pylon.CanBind) return pylon;
        return null;
    }

    public static bool Bound(Map map, Pawn husk)
    {
        foreach (var pylon in Pylons(map))
            if (pylon.Husk == husk) return true;
        return false;
    }

    // Returns why the raise ritual cannot start at this altar, or null.
    public static string Blocked(TargetInfo target)
    {
        if (!ModsConfig.IdeologyActive || target.Thing is not Building_SacrificialAltar altar || altar.Faction != Faction.OfPlayer)
            return "Requires a player-owned Sacrificial altar and Ideology.";
        if (!Available) return "Requires Anomaly.";
        var reason = target.Map.GetComponent<SacrificeRitualState>().RaiseCooldownReason();
        if (reason != null) return reason;
        if (Find.IdeoManager.GetActiveRituals(target.Map).Any(r => r is LordJob_SacrificeRitual))
            return "Another sacrifice is already underway in this settlement.";
        if (FreePylon(target.Map) == null) return "Requires a powered Hollowstone pylon switched on and with no husk.";
        if (!WarpEnergyNetwork.For(target.Map).CanDraw(RaiseCost)) return "Requires " + RaiseCost.ToString("0") + " Warp energy.";
        return null;
    }

    // The conversion order comes from the game's call lists and has not been run in game.
    public static bool Raise(Pawn victim)
    {
        var pylon = FreePylon(victim.MapHeld);
        if (pylon == null || !victim.Dead) return false;
        if (victim.Faction != Faction.OfPlayer) victim.SetFaction(Faction.OfPlayer);
        victim.guest?.SetGuestStatus(null, GuestStatus.Guest);
        if (!ResurrectionUtility.TryResurrect(victim, new ResurrectionParams { restoreMissingParts = true, noLord = true })) return false;
        MutantUtility.SetPawnAsMutantInstantly(victim, Def, RotStage.Fresh);
        MutantUtility.RegenerateHealth(victim);
        victim.workSettings?.EnableAndInitialize();
        pylon.Bind(victim);
        return true;
    }
}

public class CompProperties_HollowstonePylon : CompProperties
{
    public CompProperties_HollowstonePylon() => compClass = typeof(CompHollowstonePylon);
}

// Holds the pylon's one husk and charges its upkeep. A husk without power, or without a pylon, collapses alive.
public class CompHollowstonePylon : ThingComp, IWarpConsumer
{
    private Pawn husk;
    // True until the first pass, so a loaded save doesn't collapse a husk before the network has been asked.
    private bool warpPowered = true;
    private CompWarpToggle toggle;

    public Pawn Husk => husk;
    private CompWarpToggle Toggle => toggle ??= parent.GetComp<CompWarpToggle>();
    public bool CanBind => HuskRules.Available && Toggle.Running && warpPowered;

    public float WarpDrainPerTick => husk != null && parent.Spawned && Toggle.Running ? HuskRules.HuskCostPerDay / GenDate.TicksPerDay : 0f;
    public bool WarpPowered { get => warpPowered; set => warpPowered = value; }

    public override void PostSpawnSetup(bool respawningAfterLoad) => WarpEnergyNetwork.For(parent.Map).Register(this);

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        WarpEnergyNetwork.For(map).Unregister(this);
        if (husk == null) return;
        if (!husk.Dead) HuskRules.SetCollapsed(husk, true);
        husk = null;
    }

    public override void PostExposeData() => Scribe_References.Look(ref husk, "huskBound");

    public void Bind(Pawn pawn)
    {
        husk = pawn;
        HuskRules.SetCollapsed(pawn, false);
    }

    // Runs once a 250-tick pass: the pylon's ticker is Rare.
    public override void CompTickRare()
    {
        if (husk == null) return;
        if (husk.Dead || husk.Destroyed)
        {
            husk = null;
            return;
        }
        HuskRules.SetCollapsed(husk, !(Toggle.Running && warpPowered));
    }

    public override string CompInspectStringExtra() =>
        HuskRules.Available ? (husk == null ? "Work husk: none" : "Work husk: " + husk.LabelShortCap + (HuskRules.Collapsed(husk) ? " (collapsed)" : "")) : null;

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (!HuskRules.Available || husk != null || !parent.Spawned) yield break;
        var map = parent.Map;
        var command = new Command_Action
        {
            defaultLabel = "Rebind a husk",
            defaultDesc = "Bind a collapsed work husk with no pylon to this one. It wakes once the pylon is powered.",
            icon = parent.def.uiIcon,
            action = () =>
            {
                var options = new List<FloatMenuOption>();
                foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                    if (pawn.IsMutant && pawn.mutant.Def == HuskRules.Def && !pawn.Dead && HuskRules.Collapsed(pawn) && !HuskRules.Bound(map, pawn))
                    {
                        var husk = pawn;
                        options.Add(new FloatMenuOption(husk.LabelShortCap, () => Bind(husk)));
                    }
                if (options.Count == 0) options.Add(new FloatMenuOption("No collapsed husk without a pylon", null));
                Find.WindowStack.Add(new FloatMenu(options));
            }
        };
        if (!CanBind) command.Disable("This pylon needs Warp energy and its active effect switched on.");
        yield return command;
    }
}

public class RitualBehaviorWorker_Raise : RitualBehaviorWorker_Sacrifice
{
    public RitualBehaviorWorker_Raise() { }
    public RitualBehaviorWorker_Raise(RitualBehaviorDef def) : base(def) { }
    protected override bool Harvest => false;
    protected override bool Raise => true;
}

public class RitualOutcomeEffectWorker_Raise : RitualOutcomeEffectWorker_Sacrifice
{
    public RitualOutcomeEffectWorker_Raise() { }
    public RitualOutcomeEffectWorker_Raise(RitualOutcomeEffectDef def) : base(def) { }

    protected override string Resolve(LordJob_SacrificeRitual job, int tier)
    {
        var victim = job.PawnWithRole("victim");
        bool raised = victim != null && HuskRules.Raise(victim);
        job.raised = raised;
        job.Map.GetComponent<SacrificeRitualState>().StartRaiseCooldown();
        return raised ? victim.LabelShortCap + " rose as a work husk and was bound to a Hollowstone pylon."
            : "The body did not rise. " + HuskRules.RaiseCost.ToString("0") + " Warp energy was spent.";
    }
}
