using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

// Room support values are read by stats, thoughts and inspect text many times a tick, so they come from this cache
// instead of a room scan. Weights refresh on a fixed interval and whenever a registered fixture is added or removed.
//
// Each stone has two weight sets: every fixture (passive effects) and only fixtures whose Solar active effect is toggled
// on (active effects). Patients and sleepers are found from the rooms with active support and from registered beds, and
// Animastone meditators from the supported rooms' regions, never by scanning every pawn. Patients and sleepers are charged
// Solar energy here once per interval. None of this depends on SolarNetwork holding any building, so passive room support
// works without one.
public class RoomSupportCache : MapComponent
{
    public const int Interval = 250;
    public const int Categories = 4;
    private static readonly float[] None = new float[Categories];

    // Cached per-pawn result of the last interval. Values are what the stat parts add, already gated on being powered.
    public class Occupant
    {
        public float primary, secondary;
        public bool powered, seen;
    }

    private readonly List<CompVitalite> vitalite = new();
    private readonly List<CompSerenite> serenite = new();
    private readonly List<CompAnimastone> animastone = new();
    private readonly Dictionary<Room, float[]> vitaliteAll = new(), vitaliteActive = new(), sereniteAll = new(), sereniteActive = new(),
        animastoneAll = new(), animastoneActive = new();
    private readonly Dictionary<Pawn, Occupant> patients = new(), sleepers = new(), meditators = new();
    private readonly List<Pawn> stale = new(), found = new();
    private readonly List<CompAnimastone> poweredGrowth = new();
    private bool dirty = true;

    public RoomSupportCache(Map map) : base(map) { }

    public void Register(CompVitalite comp) { vitalite.Add(comp); dirty = true; }
    public void Unregister(CompVitalite comp) { vitalite.Remove(comp); dirty = true; }
    public void Register(CompSerenite comp) { serenite.Add(comp); dirty = true; }
    public void Unregister(CompSerenite comp) { serenite.Remove(comp); dirty = true; }
    public void Register(CompAnimastone comp) { animastone.Add(comp); dirty = true; }
    public void Unregister(CompAnimastone comp) { animastone.Remove(comp); dirty = true; }

    // Highest weight of each category in the room, or all zeros when the room is not enclosed and roofed.
    public static float[] VitaliteWeights(Room room, bool activeOnly) =>
        room?.Map == null ? None : Lookup(activeOnly ? Of(room).vitaliteActive : Of(room).vitaliteAll, room);
    public static float[] SereniteWeights(Room room, bool activeOnly) =>
        room?.Map == null ? None : Lookup(activeOnly ? Of(room).sereniteActive : Of(room).sereniteAll, room);
    public static float[] AnimastoneWeights(Room room, bool activeOnly) =>
        room?.Map == null ? None : Lookup(activeOnly ? Of(room).animastoneActive : Of(room).animastoneAll, room);

    // A plain loop: stats and thoughts read this often, and LINQ Sum allocates on the game's runtime.
    public static float Total(float[] weights)
    {
        float total = 0;
        for (int i = 0; i < weights.Length; i++) total += weights[i];
        return total;
    }

    private static RoomSupportCache Of(Room room) => room.Map.GetComponent<RoomSupportCache>();
    private static float[] Lookup(Dictionary<Room, float[]> table, Room room) => table.TryGetValue(room, out var weights) ? weights : None;

    // Cached state for a patient (Vitalite) or sleeper (Serenite), or null when the pawn has no active treatment.
    public static Occupant Patient(Pawn pawn) => pawn.Map?.GetComponent<RoomSupportCache>().patients.GetValueOrDefault(pawn);
    public static Occupant Sleeper(Pawn pawn) => pawn.Map?.GetComponent<RoomSupportCache>().sleepers.GetValueOrDefault(pawn);
    // Cached state for a pawn meditating with Animastone support, or null. Primary is room support, and secondary is
    // the extra neural heat recovery from a powered fixture in range.
    public static Occupant Meditator(Pawn pawn) => pawn.Map?.GetComponent<RoomSupportCache>().meditators.GetValueOrDefault(pawn);

    // Highest growth bonus from a powered Animastone fixture whose active radius covers the thing.
    public static float ActiveGrowth(Thing thing)
    {
        var powered = thing.Map.GetComponent<RoomSupportCache>().poweredGrowth;
        float best = 0;
        for (int i = 0; i < powered.Count; i++)
        {
            var props = powered[i].Props;
            if (props.activeGrowth > best && powered[i].parent.Position.InHorDistOf(thing.Position, props.activeRadius)) best = props.activeGrowth;
        }
        return best;
    }

    public override void MapComponentTick()
    {
        bool interval = Find.TickManager.TicksGame % Interval == 0;
        if (!dirty && !interval) return;
        dirty = false;
        RefreshWeights();
        if (interval) RefreshOccupants();
    }

    private void RefreshWeights()
    {
        vitaliteAll.Clear(); vitaliteActive.Clear(); sereniteAll.Clear(); sereniteActive.Clear(); animastoneAll.Clear(); animastoneActive.Clear();
        foreach (var comp in vitalite)
            if (comp.Props.fixtureIndex >= 0) Add(vitaliteAll, vitaliteActive, comp.parent, comp.Props.fixtureIndex, comp.Props.weight);
        foreach (var comp in serenite) Add(sereniteAll, sereniteActive, comp.parent, comp.Props.fixtureIndex, comp.Props.weight);
        foreach (var comp in animastone) Add(animastoneAll, animastoneActive, comp.parent, comp.Props.fixtureIndex, comp.Props.weight);
    }

    private static void Add(Dictionary<Room, float[]> all, Dictionary<Room, float[]> active, Thing fixture, int index, float weight)
    {
        if (!fixture.Spawned) return;
        var room = fixture.GetRoom();
        if (room == null || !room.ProperRoom || room.OpenRoofCount > 0) return;
        Raise(all, room, index, weight);
        if (fixture.TryGetComp<CompSolarToggle>()?.ToggledOn ?? true) Raise(active, room, index, weight);
    }

    private static void Raise(Dictionary<Room, float[]> table, Room room, int index, float weight)
    {
        if (!table.TryGetValue(room, out var entry)) table[room] = entry = new float[Categories];
        entry[index] = Mathf.Max(entry[index], weight);
    }

    private void RefreshOccupants()
    {
        RefreshMeditators();
        if (!SolarRules.Enabled)
        {
            // Active effects are free and ungated, so nobody needs tracking or charging.
            patients.Clear();
            sleepers.Clear();
            return;
        }
        foreach (var occupant in patients.Values) occupant.seen = false;
        foreach (var occupant in sleepers.Values) occupant.seen = false;

        foreach (var room in vitaliteActive.Keys) foreach (var bed in room.ContainedBeds) FindPatients(bed);
        foreach (var comp in vitalite)
            if (comp.Props.fixtureIndex < 0 && comp.parent is Building_Bed bed) FindPatients(bed);
        foreach (var room in sereniteActive.Keys) foreach (var bed in room.ContainedBeds) FindSleepers(bed);

        Sweep(patients, VitaliteSupport.PatientSolarDrain);
        Sweep(sleepers, SereniteSupport.SleeperSolarDrain);
    }

    private void FindPatients(Building_Bed bed)
    {
        var comp = bed.GetComp<CompVitalite>();
        bool bedOn = comp != null && comp.Props.fixtureIndex < 0 && (bed.GetComp<CompSolarToggle>()?.ToggledOn ?? true);
        float roomActive = VitaliteSupport.Fraction(bed.GetRoom(), true);
        float healing = roomActive * StoneSettings.RoomHealingCap + (bedOn ? comp.Props.healingBonus : 0);
        // Infection prevention only works in an enclosed, roofed room, as before.
        var room = bed.GetRoom();
        bool enclosed = room != null && room.ProperRoom && room.OpenRoofCount == 0;
        float infection = !enclosed ? 0 : 1 - (1 - (bedOn ? Mathf.Clamp01(comp.Props.infectionPrevention) : 0)) * (1 - roomActive * StoneSettings.RoomInfectionPreventionCap);
        if (healing <= 0 && infection <= 0) return;
        foreach (var pawn in bed.CurOccupants)
        {
            if (!pawn.InBed() || !Injured(pawn)) continue;
            var occupant = Entry(patients, pawn);
            occupant.primary = healing;
            occupant.secondary = infection;
            occupant.seen = true;
        }
    }

    private void FindSleepers(Building_Bed bed)
    {
        float support = SereniteSupport.Fraction(bed.GetRoom(), true);
        if (support <= 0) return;
        foreach (var pawn in bed.CurOccupants)
        {
            if (!pawn.InBed() || pawn.jobs?.curDriver?.asleep != true) continue;
            var occupant = Entry(sleepers, pawn);
            occupant.primary = support;
            occupant.seen = true;
        }
    }

    // Passive Animastone support needs no Solar energy, so meditators are tracked whether or not Sunstone is enabled.
    // Pawns come from the supported rooms' regions, never from every pawn on the map.
    private void RefreshMeditators()
    {
        foreach (var occupant in meditators.Values) { occupant.seen = false; occupant.primary = occupant.secondary = 0; }
        foreach (var pair in animastoneAll)
        {
            float support = AnimastoneSupport.Fraction(pair.Key);
            if (support <= 0) continue;
            var regions = pair.Key.Regions;
            for (int i = 0; i < regions.Count; i++)
            {
                var pawns = regions[i].ListerThings.ThingsInGroup(ThingRequestGroup.Pawn);
                for (int j = 0; j < pawns.Count; j++)
                {
                    if (pawns[j] is not Pawn pawn || pawn.CurJobDef != JobDefOf.Meditate) continue;
                    var occupant = Entry(meditators, pawn);
                    occupant.primary = support;
                    occupant.seen = true;
                }
            }
        }
        RefreshAnimastoneActive();
        stale.Clear();
        foreach (var pair in meditators) if (!pair.Value.seen) stale.Add(pair.Key);
        foreach (var pawn in stale) meditators.Remove(pawn);
    }

    // Active fixtures run, and pay, only while a pawn meditates within their radius. They work outdoors too, so an
    // anima grove needs no room. Pawns come from the radius cells, never from every pawn on the map.
    private void RefreshAnimastoneActive()
    {
        poweredGrowth.Clear();
        foreach (var comp in animastone)
        {
            var props = comp.Props;
            comp.poweredNow = false;
            if (props.activeRadius <= 0 || !comp.parent.Spawned) continue;
            if (SolarRules.Enabled && comp.parent.GetComp<CompSolarToggle>() is { ToggledOn: false }) continue;
            FindMeditators(comp.parent.Position, props.activeRadius);
            if (found.Count == 0) continue;
            float cost = props.activeDrain / GenDate.TicksPerDay * Interval;
            if (SolarRules.Enabled && !SolarNetwork.For(map).TryConsumeContinuous(cost)) continue;
            comp.poweredNow = true;
            if (props.activeGrowth > 0) poweredGrowth.Add(comp);
            if (props.activeHeatRecovery <= 0) continue;
            foreach (var pawn in found)
            {
                var occupant = Entry(meditators, pawn);
                occupant.secondary = Mathf.Max(occupant.secondary, props.activeHeatRecovery);
                occupant.seen = true;
            }
        }
    }

    private void FindMeditators(IntVec3 center, float radius)
    {
        found.Clear();
        int cells = GenRadial.NumCellsInRadius(radius);
        for (int i = 0; i < cells; i++)
        {
            var cell = center + GenRadial.RadialPattern[i];
            if (!cell.InBounds(map)) continue;
            var things = map.thingGrid.ThingsListAtFast(cell);
            for (int j = 0; j < things.Count; j++)
                if (things[j] is Pawn pawn && pawn.CurJobDef == JobDefOf.Meditate) found.Add(pawn);
        }
    }

    private static bool Injured(Pawn pawn)
    {
        var hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
            if (hediffs[i] is Hediff_Injury injury && !injury.IsPermanent()) return true;
        return false;
    }

    private static Occupant Entry(Dictionary<Pawn, Occupant> table, Pawn pawn)
    {
        if (!table.TryGetValue(pawn, out var occupant)) table[pawn] = occupant = new Occupant();
        return occupant;
    }

    // Drops pawns that are no longer served, and charges the rest for the elapsed interval. A refused draw leaves that pawn
    // unpowered until the next interval and depletes the network.
    private void Sweep(Dictionary<Pawn, Occupant> table, float drainPerDay)
    {
        stale.Clear();
        var network = SolarNetwork.For(map);
        float cost = drainPerDay / GenDate.TicksPerDay * Interval;
        foreach (var pair in table)
        {
            if (!pair.Value.seen) { stale.Add(pair.Key); continue; }
            pair.Value.powered = network.TryConsumeContinuous(cost);
        }
        foreach (var pawn in stale) table.Remove(pawn);
    }
}
