using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_Serenite : CompProperties_Fixture, ITunableProps
{
    public CompProperties_Serenite() => compClass = typeof(CompSerenite);

    public void DeclareTunables(TunableScope s, Def owner) =>
        s.Add(owner.defName + ".support", "Lumen_Setting_CategoryContribution".Translate(), weight, v => weight = v, Limit.Percent());

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        string room = "Lumen_Serenite_RoomRule".Translate();
        string drain = SolarRules.Enabled ? " " + "Lumen_Serenite_SleeperDrain".Translate(SupportStone.Serenite.OccupantDrain.ToString("0.##")) : "";
        return new InfoStats()
            .Add("Lumen_Info_SupportWeight".Translate(), weight.ToStringPercent(), "Lumen_Serenite_SupportWeightDesc".Translate() + " " + InfoStats.SupportRule)
            .Add(Effects.SereniteRoomComfort, "Lumen_Serenite_Comfort".Translate(), "+" + SupportEffect.SereniteComfort.Cap.ToString("0.00"), "Lumen_Serenite_ComfortDesc".Translate() + " " + room)
            .Add(Effects.SereniteRoomMood, "Lumen_Serenite_Mood".Translate(), "+" + SupportEffect.SereniteMood.Cap.ToString("0.#"), "Lumen_Serenite_MoodDesc".Translate() + " " + room)
            .Add(Effects.SereniteSleep, InfoStats.Solar("Lumen_Serenite_Sleep".Translate()), InfoStats.Plus(SupportEffect.SereniteSleep.Cap),
                "Lumen_Serenite_SleepDesc".Translate() + InfoStats.SolarRule + drain)
            .Rows;
    }
}

public class CompSerenite : CompRoomFixture
{
    public CompProperties_Serenite Props => (CompProperties_Serenite)props;

    protected override SupportStone Stone => SupportStone.Serenite;

    public override string CompInspectStringExtra()
    {
        var reading = FixtureSupport.Of(SupportStone.Serenite, parent);
        return "Lumen_Serenite_Inspect".Translate(reading.Passive.ToStringPercent(), reading.Bonus(SupportEffect.SereniteComfort).ToString("0.00"),
            reading.Bonus(SupportEffect.SereniteMood).ToString("0.0"), reading.Bonus(SupportEffect.SereniteSleep).ToStringPercent());
    }
}

public class StatPart_Serenite : StatPart
{
    public bool comfort;
    private float Bonus(StatRequest req)
    {
        if (comfort)
            return req.Thing is Building building && (building.def.IsBed || building.def.building.isSittable)
                ? FixtureSupport.Of(SupportStone.Serenite, building).Bonus(SupportEffect.SereniteComfort) : 0;
        return req.Thing is Pawn pawn ? FixtureSupport.Bonus(pawn, SupportEffect.SereniteSleep).Value : 0;
    }

    public override void TransformValue(StatRequest req, ref float val)
    {
        float bonus = Bonus(req);
        if (comfort) val += bonus;
        else val *= 1 + bonus;
    }

    public override string ExplanationPart(StatRequest req)
    {
        float bonus = Bonus(req);
        return bonus > 0 ? "Lumen_Serenite_RoomSupport".Translate(comfort ? bonus.ToString("0.00") : bonus.ToStringPercent()).ToString() : null;
    }
}

public class ThoughtWorker_SereniteRoom : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p) => FixtureSupport.Bonus(p, SupportEffect.SereniteMood).Support > 0;
    // Native thought offsets are floats. Scale once after summing weights, including half-point support.
    public override float MoodMultiplier(Pawn p) => FixtureSupport.Bonus(p, SupportEffect.SereniteMood).Value / 5f;
}

public class SereniteSleepState : MapComponent
{
    private List<Pawn> supportedSleepers = new();
    public SereniteSleepState(Map map) : base(map) { }
    public void ObserveSleep(Pawn pawn)
    {
        if (!supportedSleepers.Contains(pawn)) supportedSleepers.Add(pawn);
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref supportedSleepers, "sereniteSupportedSleepers", LookMode.Reference);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) supportedSleepers ??= new List<Pawn>();
    }

    public override void MapComponentTick()
    {
        if (supportedSleepers.Count == 0) return;
        for (int i = supportedSleepers.Count - 1; i >= 0; i--)
        {
            var pawn = supportedSleepers[i];
            if (pawn != null && !pawn.Dead && pawn.jobs?.curDriver?.asleep == true) continue;
            supportedSleepers.RemoveAt(i);
            if (pawn == null || pawn.Dead) continue;
            var memories = pawn.needs?.mood?.thoughts.memories;
            if (memories == null) continue;
            var thought = DefDatabase<ThoughtDef>.GetNamed("KM_SereniteRested");
            memories.RemoveMemoriesOfDef(thought);
            memories.TryGainMemory(thought);
        }
    }
}

[HarmonyPatch(typeof(Toils_LayDown), "ApplyBedRelatedEffects")]
public static class SereniteSupportedSleep
{
    // Runs for every sleeping pawn. A supported sleep only has to be noticed once before the pawn wakes, so it is
    // sampled once a second, and only on maps that have a Serenite fixture. __4 is the tick delta.
    public static void Postfix(Pawn p, Building_Bed bed, bool asleep, bool gainRest, int __4)
    {
        if (!asleep || !gainRest || bed == null || !p.IsHashIntervalTick(60, __4)) return;
        if (MapState.Of<RoomSupportCache>(p.Map)?.Any(SupportStone.Serenite) != true) return;
        if (FixtureSupport.Bonus(p, SupportEffect.SereniteSleep).Support > 0)
            MapState.Of<SereniteSleepState>(p.Map).ObserveSleep(p);
    }
}

public static class SereniteTunables
{
    internal static void DeclareSolarTunables(TunableScope s) =>
        s.In("Lumen_Section_SolarEnergy".Translate()).Add("Serenite.sleeperSolarDrain", "Lumen_Serenite_SleeperDrainSetting".Translate(), SupportStone.Serenite.OccupantDrain,
            v => SupportStone.Serenite.OccupantDrain = v);

    internal static void DeclareRoomTunables(TunableScope s)
    {
        var room = s.In("Lumen_Section_RoomSupport".Translate());
        room.Add("Serenite.sleepCap", "Lumen_Serenite_SleepCap".Translate(), SupportEffect.SereniteSleep.Cap, v => SupportEffect.SereniteSleep.Cap = v, Limit.Percent(10));
        room.Add("Serenite.comfortCap", "Lumen_Serenite_ComfortCap".Translate(), SupportEffect.SereniteComfort.Cap, v => SupportEffect.SereniteComfort.Cap = v, Limit.Number(max: 10));
        room.Add("Serenite.moodCap", "Lumen_Serenite_MoodCap".Translate(), SupportEffect.SereniteMood.Cap, v => SupportEffect.SereniteMood.Cap = v, Limit.Number(max: 100));
    }
}
