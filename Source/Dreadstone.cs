using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public static class DreadstoneRules
{
    public const int SubjugationOffset = 60, DreadOffset = 190;
    public static float Radius = 8f;

    internal static void DeclareTunables(TunableScope s)
    {
        var reach = s.In("Reach and cost");
        var subjugation = s.In("Subjugation");
        var dread = s.In("Dread and Death Siphon");
        reach.Add("Dreadstone.radius", "Fixture radius (tiles)", Radius, v => Radius = v, Limit.Number(1, 100));
        reach.Add("Dreadstone.subjugationCost", "Warp energy per covered prisoner or slave per day", SubjugationCost, v => SubjugationCost = v);
        subjugation.Add("Dreadstone.resistancePerDay", "Resistance lost per day (column)", ResistancePerDay, v => ResistancePerDay = v);
        subjugation.Add("Dreadstone.willPerDay", "Will lost per day (column)", WillPerDay, v => WillPerDay = v);
        subjugation.Add("Dreadstone.suppressionPerDay", "Slave suppression gained per day (column, %)", SuppressionPerDay,
            v => SuppressionPerDay = v, Limit.Percent());
        subjugation.Add("Dreadstone.pylonErosionFactor", "Pylon erosion multiplier", PylonErosionFactor, v => PylonErosionFactor = v, Limit.Number(1, 10));
        subjugation.Add("Dreadstone.breakMtbFactor", "Prison break and rebellion rarity multiplier", BreakMtbFactor,
            v => BreakMtbFactor = v, Limit.Number(1, 100));
        dread.Add("Dreadstone.dreadCostPerPass", "Warp energy per hostile per pass", DreadCostPerPass, v => DreadCostPerPass = v);
        dread.Add("Dreadstone.siphonFraction", "Market value returned as Warp energy on a marked death (%)", SiphonFraction,
            v => SiphonFraction = v, Limit.Percent());
        dread.Add("Dreadstone.arbalestDreadSeconds", "Arbalest Dread duration (seconds)", ArbalestDreadSeconds,
            v => ArbalestDreadSeconds = v, Limit.Number(1, 600));
    }
    // Warp per prisoner or slave per day.
    public static float SubjugationCost = 2f;
    // Per-day erosion from a column. A pylon multiplies each by PylonErosionFactor.
    public static float ResistancePerDay = 1f, WillPerDay = 0.05f, SuppressionPerDay = 0.1f, PylonErosionFactor = 2f;
    public static float BreakMtbFactor = 5f;
    public static float DreadCostPerPass = 1f, SiphonFraction = 0.01f, ArbalestDreadSeconds = 10f;
    // Dread stages by Hediff severity: below 1 is the cluster stage, from 1 the pylon stage.
    public const float ClusterSeverity = 0.5f, PylonSeverity = 1.5f;
    // How long Dread outlives its last refresh. It outlasts a 250-tick pass by 50 ticks, so a hostile that leaves reach clears on the next one.
    public const int DreadLingerTicks = 300;

    private static HediffDef dread;
    public static HediffDef Dread => dread ??= DefDatabase<HediffDef>.GetNamed("KM_Dread");

    // Marks a pawn with Dread. The stronger stage wins, and a siphon mark stays once set.
    public static void ApplyDread(Pawn pawn, float severity, int ticks, bool siphon)
    {
        var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(Dread) ?? pawn.health.AddHediff(Dread);
        hediff.Severity = Mathf.Max(hediff.Severity, severity);
        if (hediff.TryGetComp<HediffComp_Disappears>() is { } timer) timer.ticksToDisappear = Mathf.Max(timer.ticksToDisappear, ticks);
        if (siphon && hediff.TryGetComp<HediffComp_DreadSiphon>() is { } mark) mark.Mark(pawn.MarketValue);
        if (hediff is Hediff_Status status)
        {
            status.bracket = hediff.CurStage?.label;
            status.note = Hediff_Status.Describe(hediff.CurStage) + "\nCosts " + DreadCostPerPass.ToString("0.##") + " Warp energy per pass while in reach.";
            if (siphon) status.note += "\nIf it dies marked, " + SiphonFraction.ToStringPercent("0.#") + " of its value returns as Warp energy.";
        }
    }

    public static HediffDef Subjugation => subjugation ??= DefDatabase<HediffDef>.GetNamed("KM_DreadstoneSubjugated");
    private static HediffDef subjugation;
}

public class HediffCompProperties_DreadSiphon : HediffCompProperties
{
    public HediffCompProperties_DreadSiphon() => compClass = typeof(HediffComp_DreadSiphon);
}

// Pays a share of the pawn's value to the Warp network when it dies while marked. A pawn dies once, so it pays once.
public class HediffComp_DreadSiphon : HediffComp
{
    private bool marked;
    private float value;

    public void Mark(float marketValue)
    {
        marked = true;
        value = marketValue;
    }

    public override void CompExposeData()
    {
        Scribe_Values.Look(ref marked, "marked");
        Scribe_Values.Look(ref value, "value");
    }

    public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
    {
        if (!marked || Pawn.MapHeld == null) return;
        WarpEnergyNetwork.For(Pawn.MapHeld).DepositAnywhere(value * DreadstoneRules.SiphonFraction);
    }
}

// Column and pylon subjugation on the Warp pass: a covered prisoner or slave pays Warp, loses resistance and will, and breaks out less often.
public class DreadstoneSubjugation : MapComponent
{
    private static readonly int[] SubjugationTiers = { 1, 3 };
    private readonly HashSet<Pawn> covered = new();

    public DreadstoneSubjugation(Map map) : base(map) { }

    public bool Covers(Pawn pawn) => covered.Contains(pawn);

    public override void MapComponentTick()
    {
        if (Find.TickManager.TicksGame % WarpRules.PassInterval != DreadstoneRules.SubjugationOffset) return;
        covered.Clear();
        var cache = DarkFixtureCache.For(map);
        if (!cache.AnyActive(DarkStoneKind.Dreadstone)) return;
        var warp = WarpEnergyNetwork.For(map);
        Pass(map.mapPawns.PrisonersOfColonySpawned, cache, warp);
        Pass(map.mapPawns.SlavesOfColonySpawned, cache, warp);
    }

    private void Pass(List<Pawn> pawns, DarkFixtureCache cache, WarpEnergyNetwork warp)
    {
        float days = WarpRules.PassInterval / (float)GenDate.TicksPerDay;
        foreach (var pawn in pawns)
        {
            // A cluster doesn't subjugate, so only columns and pylons count.
            int tier = cache.Cover(DarkStoneKind.Dreadstone, pawn, DreadstoneRules.Radius, false, SubjugationTiers);
            if (tier == 0 || !warp.TryConsumeContinuous(DreadstoneRules.SubjugationCost * days)) continue;
            covered.Add(pawn);
            float scale = (tier == 3 ? DreadstoneRules.PylonErosionFactor : 1f) * days;
            if (pawn.guest != null)
            {
                pawn.guest.resistance = Mathf.Max(0f, pawn.guest.resistance - DreadstoneRules.ResistancePerDay * scale);
                pawn.guest.will = Mathf.Max(0f, pawn.guest.will - DreadstoneRules.WillPerDay * scale);
            }
            var suppression = pawn.needs?.TryGetNeed<Need_Suppression>();
            if (suppression != null) suppression.CurLevelPercentage = Mathf.Min(1f, suppression.CurLevelPercentage + DreadstoneRules.SuppressionPerDay * scale);
            float perDay = scale / days;
            var note = new System.Text.StringBuilder();
            note.AppendLine("Resistance: -" + (DreadstoneRules.ResistancePerDay * perDay).ToString("0.##") + " a day");
            note.AppendLine("Will: -" + (DreadstoneRules.WillPerDay * perDay).ToString("0.##") + " a day");
            if (suppression != null) note.AppendLine("Suppression: +" + (DreadstoneRules.SuppressionPerDay * perDay).ToStringPercent("0.#") + " a day");
            note.AppendLine("Prison break and rebellion are " + DreadstoneRules.BreakMtbFactor.ToString("0.#") + "x rarer (the prisoner tab shows the result).");
            note.Append("Costs " + DreadstoneRules.SubjugationCost.ToString("0.##") + " Warp energy a day.");
            Hediff_Status.Apply(pawn, DreadstoneRules.Subjugation, tier == 3 ? "pylon" : "column", note.ToString(), DreadstoneRules.DreadLingerTicks);
        }
    }
}

// Both mtb results are in days, and -1 means never. Only positive results scale, and the lookup is a set read.
public static class DreadstoneMtb
{
    public static void Scale(Pawn pawn, ref float result)
    {
        if (result <= 0f) return;
        if (NewEquipment.Subjugated(pawn)) result *= DreadstoneRules.BreakMtbFactor;
        else if (NewEquipment.Collared(pawn)) result *= 2f;
    }
}

[HarmonyPatch]
public static class DreadstonePrisonBreakPatch
{
    static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(typeof(PrisonBreakUtility), "InitiatePrisonBreakMtbDays");
    public static void Postfix(Pawn __0, ref float __result) => DreadstoneMtb.Scale(__0, ref __result);
}

// Runs after the game's own 60,000-tick cache, so a cached value is scaled each time it is read.
[HarmonyPatch]
public static class DreadstoneRebellionPatch
{
    static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(typeof(SlaveRebellionUtility), "InitiateSlaveRebellionMtbDays");
    public static void Postfix(Pawn __0, ref float __result) => DreadstoneMtb.Scale(__0, ref __result);
}

// Cluster and pylon Dread on hostile humanlikes in reach, paid per hostile per pass, so it only burns during fights.
public class DreadstoneDread : MapComponent
{
    private static readonly int[] DreadTiers = { 2, 3 };
    public DreadstoneDread(Map map) : base(map) { }

    public override void MapComponentTick()
    {
        if (Find.TickManager.TicksGame % WarpRules.PassInterval != DreadstoneRules.DreadOffset) return;
        var cache = DarkFixtureCache.For(map);
        if (cache.StrongestActiveTier(DarkStoneKind.Dreadstone) < 2) return;
        var warp = WarpEnergyNetwork.For(map);
        foreach (var target in map.attackTargetsCache.TargetsHostileToColony)
        {
            if (target.Thing is not Pawn { Dead: false, Spawned: true } pawn || !pawn.RaceProps.Humanlike) continue;
            int tier = cache.Cover(DarkStoneKind.Dreadstone, pawn, DreadstoneRules.Radius, true, DreadTiers);
            if (tier == 0 || !warp.TryConsumeContinuous(DreadstoneRules.DreadCostPerPass)) continue;
            DreadstoneRules.ApplyDread(pawn, tier == 3 ? DreadstoneRules.PylonSeverity : DreadstoneRules.ClusterSeverity,
                DreadstoneRules.DreadLingerTicks, tier == 3);
        }
    }
}

// The Arbalest bolt marks the target before the damage lands, so a killing hit still siphons.
public class Bullet_Dread : Bullet
{
    protected override void Impact(Thing hitThing, bool blockedByShield = false)
    {
        if (hitThing is Pawn { Dead: false } pawn && pawn.RaceProps.Humanlike && launcher != null && pawn.HostileTo(launcher))
            DreadstoneRules.ApplyDread(pawn, DreadstoneRules.ClusterSeverity, Mathf.RoundToInt(DreadstoneRules.ArbalestDreadSeconds * 60f), true);
        base.Impact(hitThing, blockedByShield);
    }
}
