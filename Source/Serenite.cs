using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_Serenite : CompProperties
{
    public int fixtureIndex;
    public float weight;
    public CompProperties_Serenite() => compClass = typeof(CompSerenite);

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        string room = "Scales with room support. Applies to pawns in the same enclosed room.";
        string drain = SolarRules.Enabled ? $" Costs {SereniteSupport.SleeperSolarDrain:0.##} Solar energy a day for each sleeper." : "";
        return new InfoStats()
            .Add("Support weight", weight.ToStringPercent(), "This fixture's share of Serenite room support. " + InfoStats.SupportRule)
            .Add("Comfort at full support", "+" + SereniteSupport.ComfortCap.ToString("0.00"), "Added to the comfort of existing beds and seats. " + room)
            .Add("Mood at full support", "+" + SereniteSupport.MoodCap.ToString("0.#"), "A mood bonus while in the room. " + room)
            .Add(InfoStats.Solar("Sleep restoration at full support"), InfoStats.Plus(SereniteSupport.SleepCap),
                "Faster rest while sleeping, and a rested mood memory for a while after waking. Scales with the room support of fixtures whose active effect is on." + InfoStats.SolarRule + drain)
            .Rows;
    }
}

public class CompSerenite : ThingComp
{
    public CompProperties_Serenite Props => (CompProperties_Serenite)props;

    public override void PostSpawnSetup(bool respawningAfterLoad) => parent.Map.GetComponent<RoomSupportCache>().Register(this);
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) => map.GetComponent<RoomSupportCache>().Unregister(this);

    public override string CompInspectStringExtra()
    {
        var room = parent.GetRoom();
        float support = SereniteSupport.Fraction(room), active = SereniteSupport.Fraction(room, true);
        return $"Room support: {support.ToStringPercent()}. Comfort +{support * SereniteSupport.ComfortCap:0.00}, mood +{support * SereniteSupport.MoodCap:0.0}, sleep restoration +{(active * SereniteSupport.SleepCap).ToStringPercent()}";
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

public static class SereniteSupport
{
    public static float SleepCap = 0.20f, ComfortCap = 0.20f, MoodCap = 5f;

    // Solar energy per day for each sleeper in a room with active support.
    public static float SleeperSolarDrain = 10f;

    // Each category counts once at its highest weight, so differing lamp settings give an order-independent result.
    // Passive support counts every fixture. Active support counts only fixtures toggled on, and with Sunstone off it counts all.
    public static float Fraction(Room room, bool activeOnly = false) =>
        Mathf.Clamp01(RoomSupportCache.Total(RoomSupportCache.SereniteWeights(room, activeOnly && SolarRules.Enabled)));

    public static float Fraction(Pawn pawn) => pawn.Spawned && !pawn.Dead ? Fraction(pawn.GetRoom()) : 0;

    // Active sleep support for a sleeper in bed: the cached, powered value, or the room value with Sunstone off.
    public static float SleepFraction(Pawn pawn)
    {
        if (!pawn.Spawned || pawn.Dead || !pawn.InBed() || pawn.jobs?.curDriver?.asleep != true) return 0;
        if (!SolarRules.Enabled) return Fraction(pawn.GetRoom(), true);
        var sleeper = RoomSupportCache.Sleeper(pawn);
        return sleeper is { powered: true } ? sleeper.primary : 0;
    }
}

public class StatPart_Serenite : StatPart
{
    public bool comfort;
    private float Bonus(StatRequest req)
    {
        if (comfort)
            return req.Thing is Building building && (building.def.IsBed || building.def.building.isSittable)
                ? SereniteSupport.Fraction(building.GetRoom()) * SereniteSupport.ComfortCap : 0;
        return req.Thing is Pawn pawn ? SereniteSupport.SleepFraction(pawn) * SereniteSupport.SleepCap : 0;
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
    protected override ThoughtState CurrentStateInternal(Pawn p) => SereniteSupport.Fraction(p) > 0;
    // Native thought offsets are floats. Scale once after summing weights, including half-point support.
    public override float MoodMultiplier(Pawn p) => SereniteSupport.Fraction(p) * SereniteSupport.MoodCap / 5f;
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
        if (asleep && gainRest && bed != null && SereniteSupport.SleepFraction(p) > 0)
            p.Map.GetComponent<SereniteSleepState>().ObserveSleep(p);
    }
}
