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

    internal static void DeclareTunables(TunableScope s)
    {
        var husk = s.In("Lumen_Husk_Section".Translate());
        husk.Add("Hollowstone.raiseCost", "Lumen_Husk_RaiseCost".Translate(), RaiseCost, v => RaiseCost = v);
        husk.Add("Hollowstone.huskCostPerDay", "Lumen_Husk_CostPerDay".Translate(), HuskCostPerDay, v => HuskCostPerDay = v);
        husk.Add("Hollowstone.raiseCooldown", "Lumen_Husk_Cooldown".Translate(), RaiseCooldownDays, v => RaiseCooldownDays = v);
    }
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
            return "Lumen_Husk_NeedsAltar".Translate();
        if (!Available) return "Lumen_Husk_NeedsAnomaly".Translate();
        if (!Effects.HollowstoneHusks.On) return "Lumen_SwitchedOff".Translate();
        var reason = MapState.Of<SacrificeRitualState>(target.Map).Raise.Reason;
        if (reason != null) return reason;
        if (Find.IdeoManager.GetActiveRituals(target.Map).Any(r => r is LordJob_SacrificeRitual))
            return "Lumen_Husk_SacrificeUnderway".Translate();
        if (FreePylon(target.Map) == null) return "Lumen_Husk_NeedsPylon".Translate();
        if (!WarpEnergyNetwork.For(target.Map).CanDraw(RaiseCost)) return "Lumen_RequiresWarp".Translate(RaiseCost.ToString("0"));
        return null;
    }

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
public class CompHollowstonePylon : ThingComp, IActiveEffectDrain
{
    private Pawn husk;
    private CompWarpToggle toggle;

    public Pawn Husk => husk;
    private CompWarpToggle Toggle => toggle ??= parent.GetComp<CompWarpToggle>();
    public bool CanBind => HuskRules.Available && Toggle.Powered;

    public float DrainPerTick => husk != null ? HuskRules.HuskCostPerDay / GenDate.TicksPerDay : 0f;

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
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
        HuskRules.SetCollapsed(husk, !Toggle.Powered);
    }

    public override string CompInspectStringExtra() =>
        HuskRules.Available ? (husk == null ? "Lumen_Husk_None".Translate()
            : (HuskRules.Collapsed(husk) ? "Lumen_Husk_Collapsed" : "Lumen_Husk_Bound").Translate(husk.LabelShortCap)).ToString() : null;

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (!HuskRules.Available || husk != null || !parent.Spawned) yield break;
        var map = parent.Map;
        var command = new Command_Action
        {
            defaultLabel = "Lumen_Husk_Rebind".Translate(),
            defaultDesc = "Lumen_Husk_RebindDesc".Translate(),
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
                if (options.Count == 0) options.Add(new FloatMenuOption("Lumen_Husk_NoneToBind".Translate(), null));
                Find.WindowStack.Add(new FloatMenu(options));
            }
        };
        if (!CanBind) command.Disable("Lumen_Husk_PylonNeedsWarp".Translate());
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
        MapState.Of<SacrificeRitualState>(job.Map).Raise.Start();
        return raised ? "Lumen_Husk_Rose".Translate(victim.LabelShortCap).ToString()
            : "Lumen_Husk_DidNotRise".Translate(HuskRules.RaiseCost.ToString("0")).ToString();
    }
}
