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
        s.Add(owner.defName + ".support", "Category room contribution (%)", weight, v => weight = v, Limit.Percent());

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        string room = "Scales with room support. Applies to pawns in the same enclosed room.";
        string drain = SolarRules.Enabled ? $" Costs {SupportStone.Serenite.OccupantDrain:0.##} Solar energy a day for each sleeper." : "";
        return new InfoStats()
            .Add("Support weight", weight.ToStringPercent(), "This fixture's share of Serenite room support. " + InfoStats.SupportRule)
            .Add("Comfort at full support", "+" + SupportEffect.SereniteComfort.Cap.ToString("0.00"), "Added to the comfort of existing beds and seats. " + room)
            .Add("Mood at full support", "+" + SupportEffect.SereniteMood.Cap.ToString("0.#"), "A mood bonus while in the room. " + room)
            .Add(InfoStats.Solar("Sleep restoration at full support"), InfoStats.Plus(SupportEffect.SereniteSleep.Cap),
                "Faster rest while sleeping, and a rested mood memory for a while after waking. Scales with the room support of fixtures whose active effect is on." + InfoStats.SolarRule + drain)
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
        return $"Room support: {reading.Passive.ToStringPercent()}. Comfort +{reading.Bonus(SupportEffect.SereniteComfort):0.00}, mood +{reading.Bonus(SupportEffect.SereniteMood):0.0}, sleep restoration +{reading.Bonus(SupportEffect.SereniteSleep).ToStringPercent()}";
    }

    public override void CompTick()
    {
        if (!parent.Spawned || !parent.IsHashIntervalTick(300) || parent.Position.Fogged(parent.Map) || !CompAnimationToggle.On(parent)) return;
        var mote = FixtureMotes.Make("KM_Mote_SerenitePulse", parent.Map);
        if (mote == null) return;
        mote.exactPosition = parent.DrawPos + parent.def.graphicData.drawOffset;
        mote.exactPosition.y = AltitudeLayer.MoteOverhead.AltitudeFor();
        mote.Scale = 0.08f;
        mote.instanceColor = new Color(0.78f, 0.62f, 1f);
        mote.airTimeLeft = 1.8f;
        mote.SetVelocity(0, 0.12f);
        GenSpawn.Spawn(mote, parent.Position, parent.Map);
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
        return bonus > 0 ? "Serenite room support: +" + (comfort ? bonus.ToString("0.00") : bonus.ToStringPercent()) : null;
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
    public static void Postfix(Pawn p, Building_Bed bed, bool asleep, bool gainRest)
    {
        if (asleep && gainRest && bed != null && FixtureSupport.Bonus(p, SupportEffect.SereniteSleep).Support > 0)
            p.Map.GetComponent<SereniteSleepState>().ObserveSleep(p);
    }
}

public static class SereniteTunables
{
    internal static void DeclareSolarTunables(TunableScope s) =>
        s.In("Solar energy").Add("Serenite.sleeperSolarDrain", "Solar energy per day for each supported sleeper", SupportStone.Serenite.OccupantDrain,
            v => SupportStone.Serenite.OccupantDrain = v);

    internal static void DeclareRoomTunables(TunableScope s)
    {
        var room = s.In("Room support");
        room.Add("Serenite.sleepCap", "Sleep restoration cap (%)", SupportEffect.SereniteSleep.Cap, v => SupportEffect.SereniteSleep.Cap = v, Limit.Percent(10));
        room.Add("Serenite.comfortCap", "Bed/seat Comfort cap", SupportEffect.SereniteComfort.Cap, v => SupportEffect.SereniteComfort.Cap = v, Limit.Number(max: 10));
        room.Add("Serenite.moodCap", "Room Mood cap", SupportEffect.SereniteMood.Cap, v => SupportEffect.SereniteMood.Cap = v, Limit.Number(max: 100));
    }
}
