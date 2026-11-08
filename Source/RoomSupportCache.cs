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

    // Cached per-pawn result of the last interval. Values are what the stat parts add, already gated on being powered.
    public class Occupant
    {
        public float primary, secondary;
        public bool powered, seen;
    }

    // Rooms' category weights: every fixture, and only the running ones for stones whose effects read them.
    private sealed class RoomWeights
    {
        public readonly CategoryWeights All = new();
        public CategoryWeights Active;
    }

    private sealed class Table
    {
        public readonly List<CompRoomFixture> Fixtures = new();
        public readonly Dictionary<Room, RoomWeights> Rooms = new();
        // Rooms with a running fixture, in the order the first one was seen. Occupants are found and charged in this
        // order, so it decides who stays powered when the network can't pay everyone.
        public readonly List<Room> RunningRooms = new();
    }

    private readonly Dictionary<SupportStone, Table> tables = new()
    {
        [SupportStone.Vitalite] = new(), [SupportStone.Serenite] = new(), [SupportStone.Animastone] = new()
    };
    private readonly Dictionary<Pawn, Occupant> patients = new(), sleepers = new(), meditators = new();
    private readonly List<Pawn> stale = new(), found = new();
    private readonly List<CompAnimastone> poweredGrowth = new();
    private bool dirty = true;

    public RoomSupportCache(Map map) : base(map) { }

    public void Register(SupportStone stone, CompRoomFixture comp) { tables[stone].Fixtures.Add(comp); dirty = true; }
    public void Unregister(SupportStone stone, CompRoomFixture comp) { tables[stone].Fixtures.Remove(comp); dirty = true; }

    // Highest weight of each category in the room, or nothing when the room is not enclosed and roofed.
    internal SupportReading Read(SupportStone stone, Room room)
    {
        if (!tables[stone].Rooms.TryGetValue(room, out var entry)) return SupportReading.Empty(stone);
        float passive = SupportCore.Fraction(SupportKind.Room, entry.All, true);
        float active = entry.Active != null && SupportCore.UseActiveSet(true, ActiveEffectKind.Solar.Metered)
            ? SupportCore.Fraction(SupportKind.Room, entry.Active, true) : passive;
        return new SupportReading(stone, entry.All, passive, active, true);
    }

    // Cached state for a patient (Vitalite) or sleeper (Serenite), or null when the pawn has no active treatment.
    internal static Occupant Patient(Pawn pawn) => pawn.Map?.GetComponent<RoomSupportCache>().patients.GetValueOrDefault(pawn);
    internal static Occupant Sleeper(Pawn pawn) => pawn.Map?.GetComponent<RoomSupportCache>().sleepers.GetValueOrDefault(pawn);
    // Cached state for a pawn meditating with Animastone support, or null. Primary is room support, and secondary is
    // the extra neural heat recovery from a powered fixture in range.
    internal static Occupant Meditator(Pawn pawn) => pawn.Map?.GetComponent<RoomSupportCache>().meditators.GetValueOrDefault(pawn);

    // Extra neural heat recovery from a powered Animastone pylon in range, on top of room support.
    public static float ActiveHeatRecovery(Pawn pawn) => pawn.CurJobDef == JobDefOf.Meditate ? Meditator(pawn)?.secondary ?? 0 : 0;

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
        foreach (var pair in tables)
        {
            pair.Value.Rooms.Clear();
            pair.Value.RunningRooms.Clear();
            foreach (var comp in pair.Value.Fixtures) Add(pair.Key, pair.Value, comp);
        }
    }

    private static void Add(SupportStone stone, Table table, CompRoomFixture comp)
    {
        var props = comp.Fixture;
        var fixture = comp.parent;
        if (props.category == FixtureCategory.None || !fixture.Spawned) return;
        var room = fixture.GetRoom();
        if (!FixtureSupport.Enclosed(room)) return;
        if (!table.Rooms.TryGetValue(room, out var entry)) table.Rooms[room] = entry = new RoomWeights { Active = stone.TracksActive ? new CategoryWeights() : null };
        entry.All.Raise(props.category, props.weight);
        if (entry.Active == null || !(fixture.TryGetComp<CompSolarToggle>()?.Running ?? true)) return;
        entry.Active.Raise(props.category, props.weight);
        if (!table.RunningRooms.Contains(room)) table.RunningRooms.Add(room);
    }

    private void RefreshOccupants()
    {
        RefreshMeditators();
        if (!ActiveEffectKind.Solar.Metered)
        {
            // Active effects are free and ungated, so nobody needs tracking or charging.
            patients.Clear();
            sleepers.Clear();
            return;
        }
        foreach (var occupant in patients.Values) occupant.seen = false;
        foreach (var occupant in sleepers.Values) occupant.seen = false;

        var vitalite = tables[SupportStone.Vitalite];
        foreach (var room in vitalite.RunningRooms) foreach (var bed in room.ContainedBeds) FindPatients(bed);
        foreach (var comp in vitalite.Fixtures)
            if (comp.Fixture.category == FixtureCategory.None && comp.parent is Building_Bed bed) FindPatients(bed);
        foreach (var room in tables[SupportStone.Serenite].RunningRooms) foreach (var bed in room.ContainedBeds) FindSleepers(bed);

        Sweep(patients, SupportStone.Vitalite.OccupantDrain);
        Sweep(sleepers, SupportStone.Serenite.OccupantDrain);
    }

    private void FindPatients(Building_Bed bed)
    {
        var treatment = FixtureSupport.Treat(bed.GetRoom(), bed);
        if (treatment.Healing <= 0 && treatment.InfectionPrevention <= 0) return;
        foreach (var pawn in bed.CurOccupants)
        {
            if (!pawn.InBed() || !Injured(pawn)) continue;
            var occupant = Entry(patients, pawn);
            occupant.primary = treatment.Healing;
            occupant.secondary = treatment.InfectionPrevention;
            occupant.seen = true;
        }
    }

    private void FindSleepers(Building_Bed bed)
    {
        float support = FixtureSupport.Of(SupportStone.Serenite, bed.GetRoom()).Active;
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
        foreach (var room in tables[SupportStone.Animastone].Rooms.Keys)
        {
            float support = FixtureSupport.Of(SupportStone.Animastone, room).Passive;
            if (support <= 0) continue;
            var regions = room.Regions;
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
        foreach (var fixture in tables[SupportStone.Animastone].Fixtures)
        {
            if (fixture is not CompAnimastone comp) continue;
            var props = comp.Props;
            comp.poweredNow = false;
            if (props.activeRadius <= 0 || !comp.parent.Spawned) continue;
            if (comp.parent.GetComp<CompSolarToggle>() is { Permitted: false }) continue;
            FindMeditators(comp.parent.Position, props.activeRadius);
            if (found.Count == 0) continue;
            float cost = props.activeDrain / GenDate.TicksPerDay * Interval;
            if (!SolarNetwork.For(map).Account.TryDrawContinuous(cost)) continue;
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
