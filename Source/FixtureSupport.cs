using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LumenStones;

// A fixture's category and weight in its stone's support. Beds (Vitalite) are not fixtures and leave the category unset.
public abstract class CompProperties_Fixture : CompProperties
{
    public FixtureCategory category = FixtureCategory.None;
    public float weight;

    protected virtual bool IsFixture(ThingDef parentDef) => true;

    public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
    {
        foreach (var error in base.ConfigErrors(parentDef)) yield return error;
        if (category == FixtureCategory.None && IsFixture(parentDef)) yield return "a support fixture needs a category";
        if (category == FixtureCategory.None && weight > 0) yield return "a weight needs a category";
    }
}

// A room stone's fixture or bed. The map cache finds it by its stone.
public abstract class CompRoomFixture : ThingComp
{
    protected abstract SupportStone Stone { get; }
    internal CompProperties_Fixture Fixture => (CompProperties_Fixture)props;

    public override void PostSpawnSetup(bool respawningAfterLoad) => parent.Map.GetComponent<RoomSupportCache>().Register(Stone, this);
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) => map.GetComponent<RoomSupportCache>().Unregister(Stone, this);
}

// One stone's support: its category names and tunables. Room stones read the map cache; ritual stones scan around their altar.
public sealed class SupportStone
{
    public static readonly SupportStone Vitalite = new("Vitalite", SupportKind.Room, true, 20f, "column", "lamp", "pylon", "restorative cluster");
    public static readonly SupportStone Serenite = new("Serenite", SupportKind.Room, true, 10f, "column", "lamp", "sanctuary shrine", "soothing cluster");
    public static readonly SupportStone Animastone = new("Animastone", SupportKind.Room, false, 0f, "column", "lamp", "pylon", "cluster");
    public static readonly SupportStone Lumenstone = new("Lumenstone", SupportKind.Ritual, false, 0f, "column", "lamp", "pylon", "cluster",
        () => SupportEffect.LumenstoneQuality, () => SupportEffect.LumenstoneYield);
    public static readonly SupportStone DarkStone = new("Dark Stone", SupportKind.Ritual, false, 0f, "column", "lamp", "pylon", "cluster",
        () => SupportEffect.DarkStoneQuality, () => SupportEffect.DarkStoneYield);

    private readonly string name;
    private readonly string[] labels;
    private readonly Func<SupportEffect> quality, yieldEffect;

    private SupportStone(string name, SupportKind kind, bool tracksActive, float occupantDrain, string column, string lamp, string pylon, string cluster,
        Func<SupportEffect> quality = null, Func<SupportEffect> yieldEffect = null)
    {
        this.name = name;
        Kind = kind;
        TracksActive = tracksActive;
        OccupantDrain = occupantDrain;
        labels = new[] { column, lamp, pylon, cluster };
        this.quality = quality;
        this.yieldEffect = yieldEffect;
    }

    public static SupportStone Ritual(bool dark) => dark ? DarkStone : Lumenstone;

    public SupportKind Kind { get; }
    // Whether any of the stone's effects reads only running fixtures.
    internal bool TracksActive { get; }
    public string Label(FixtureCategory category) => labels[(int)category];
    // Ritual stones: how far from the altar a fixture still counts.
    public float Radius { get; set; } = 8f;
    // Solar energy per day for each occupant with an active effect: an injured patient in bed (Vitalite), a sleeper (Serenite).
    public float OccupantDrain { get; set; }
    public SupportEffect Quality => quality?.Invoke();
    public SupportEffect Yield => yieldEffect?.Invoke();
    public override string ToString() => name;
}

// One capped effect of a stone's support. Cap is the tunable, at its shipped default.
public sealed class SupportEffect
{
    public static readonly SupportEffect VitaliteHealing = new(SupportStone.Vitalite, 0.25f, true);
    public static readonly SupportEffect VitaliteImmunity = new(SupportStone.Vitalite, 0.10f, false);
    public static readonly SupportEffect VitaliteInfection = new(SupportStone.Vitalite, 0.10f, true);
    public static readonly SupportEffect SereniteSleep = new(SupportStone.Serenite, 0.20f, true);
    public static readonly SupportEffect SereniteComfort = new(SupportStone.Serenite, 0.20f, false);
    public static readonly SupportEffect SereniteMood = new(SupportStone.Serenite, 5f, false);
    public static readonly SupportEffect AnimastoneHeatRecovery = new(SupportStone.Animastone, 0.5f, false);
    public static readonly SupportEffect AnimastoneFocusGain = new(SupportStone.Animastone, 0.25f, false);
    public static readonly SupportEffect LumenstoneQuality = new(SupportStone.Lumenstone, 0.20f, false);
    public static readonly SupportEffect LumenstoneYield = new(SupportStone.Lumenstone, 0.40f, false);
    public static readonly SupportEffect DarkStoneQuality = new(SupportStone.DarkStone, 0.20f, false);
    public static readonly SupportEffect DarkStoneYield = new(SupportStone.DarkStone, 0.40f, false);

    private SupportEffect(SupportStone stone, float cap, bool active)
    {
        Stone = stone;
        Cap = cap;
        Active = active;
    }

    public SupportStone Stone { get; }
    // Reads the fraction of fixtures whose active effect is running, rather than of all fixtures.
    public bool Active { get; }
    public float Cap { get; set; }
}

// A stone's support at one place. Passive counts every fixture. Active counts only running fixtures while Solar is metered,
// and equals Passive otherwise and for ritual stones. A room's reading is the cache's, so it lags changes by up to an interval.
public readonly struct SupportReading
{
    private static readonly CategoryWeights NoWeights = new();

    // No place to read: nothing is supported.
    public static readonly SupportReading None = default;

    private readonly CategoryWeights weights;

    internal SupportReading(SupportStone stone, CategoryWeights weights, float passive, float active, bool enclosed)
    {
        Stone = stone;
        this.weights = weights;
        Passive = passive;
        Active = active;
        Enclosed = enclosed;
    }

    internal static SupportReading Empty(SupportStone stone) => new(stone, NoWeights, 0, 0, false);

    public SupportStone Stone { get; }
    public float Passive { get; }
    public float Active { get; }
    public bool Enclosed { get; }

    public float Bonus(SupportEffect effect)
    {
        if (Stone != null && effect.Stone != Stone) throw new ArgumentException(effect.Stone + " effect read from " + Stone + " support");
        return (effect.Active ? Active : Passive) * effect.Cap;
    }

    public string Missing()
    {
        if (Stone == null) return "";
        var names = new List<string>();
        foreach (var category in SupportCore.Missing(weights)) names.Add(Stone.Label(category));
        return string.Join(", ", names);
    }
}

// What a pawn is getting now: the room support behind it (unset for Vitalite, whose value also includes its bed) and the value.
public readonly struct PawnBonus
{
    public readonly float Support, Value;
    public PawnBonus(float support, float value) { Support = support; Value = value; }
}

public static class FixtureSupport
{
    public static bool Enclosed(Room room) => room != null && room.ProperRoom && room.OpenRoofCount == 0;

    public static SupportReading Of(SupportStone stone, Room room)
    {
        if (stone.Kind != SupportKind.Room) throw new ArgumentException(stone + " is a ritual stone; read it at its altar");
        return room?.Map == null ? SupportReading.Empty(stone) : room.Map.GetComponent<RoomSupportCache>().Read(stone, room);
    }

    // A room stone reads the room of the thing. A ritual stone reads around the thing, which must be its altar,
    // by a live scan that is not cached.
    public static SupportReading Of(SupportStone stone, Thing at) =>
        stone.Kind == SupportKind.Room ? Of(stone, at?.GetRoom()) : Scan(stone, at);

    // Fixtures must share the host's room. Enclosed roofed rooms give full weights and anything else half.
    private static SupportReading Scan(SupportStone stone, Thing at)
    {
        if (at is not Building host || host is not IRitualSupportHost || !host.Spawned) return SupportReading.None;
        var room = host.GetRoom();
        if (room == null) return SupportReading.None;
        float radius = stone.Radius;
        var center = host.TrueCenter();
        var weights = new CategoryWeights();
        // Padding covers the cells of a multi-cell host, whose position is not its centre.
        foreach (var cell in GenRadial.RadialCellsAround(host.Position, radius + 2f, true))
        {
            if (!cell.InBounds(host.Map)) continue;
            foreach (var thing in cell.GetThingList(host.Map))
            {
                var comp = thing.TryGetComp<CompLumenstoneFixture>();
                if (comp == null || SupportStone.Ritual(comp.Props.dark) != stone || thing.GetRoom() != room) continue;
                if ((center - thing.TrueCenter()).MagnitudeHorizontalSquared() > radius * radius) continue;
                weights.Raise(comp.Props.category, comp.Props.weight);
            }
        }
        bool enclosed = Enclosed(room);
        float fraction = SupportCore.Fraction(SupportKind.Ritual, weights, enclosed);
        return new SupportReading(stone, weights, fraction, fraction, enclosed);
    }

    // The pawn's powered bonus. With Solar metered an active effect comes from the cache, which has already charged for it
    // once an interval, and otherwise it is read live from the pawn's room.
    public static PawnBonus Bonus(Pawn pawn, SupportEffect effect)
    {
        var stone = effect.Stone;
        if (stone == SupportStone.Vitalite) return VitaliteBonus(pawn, effect);
        if (stone == SupportStone.Serenite) return SereniteBonus(pawn, effect);
        if (stone == SupportStone.Animastone) return AnimastoneBonus(pawn, effect);
        throw new ArgumentException(stone + " support is not read per pawn");
    }

    // The one Vitalite occupant formula, for both the cache and the live read.
    internal static Treatment Treat(Room room, Thing bed)
    {
        var reading = Of(SupportStone.Vitalite, room);
        var props = bed?.TryGetComp<CompVitalite>()?.Props;
        bool isBed = props != null && props.category == FixtureCategory.None;
        return SupportCore.Vitalite(reading.Passive, reading.Active, Enclosed(room),
            isBed ? props.healingBonus : 0, isBed ? props.immunityBonus : 0, isBed ? props.infectionPrevention : 0,
            !isBed || (bed.TryGetComp<CompSolarToggle>()?.Permitted ?? true),
            SupportEffect.VitaliteHealing.Cap, SupportEffect.VitaliteImmunity.Cap, SupportEffect.VitaliteInfection.Cap);
    }

    private static PawnBonus VitaliteBonus(Pawn pawn, SupportEffect effect)
    {
        if (!pawn.Spawned || pawn.Dead || !pawn.InBed()) return default;
        var room = pawn.GetRoom();
        if (effect == SupportEffect.VitaliteInfection && !Enclosed(room)) return default;
        if (effect != SupportEffect.VitaliteImmunity && ActiveEffectKind.Solar.Metered)
        {
            var patient = RoomSupportCache.Patient(pawn);
            float cached = patient is { powered: true } ? effect == SupportEffect.VitaliteHealing ? patient.primary : patient.secondary : 0f;
            return new PawnBonus(0, cached);
        }
        var treatment = Treat(room, pawn.CurrentBed());
        return new PawnBonus(0, effect == SupportEffect.VitaliteHealing ? treatment.Healing
            : effect == SupportEffect.VitaliteImmunity ? treatment.Immunity : treatment.InfectionPrevention);
    }

    private static PawnBonus SereniteBonus(Pawn pawn, SupportEffect effect)
    {
        if (!pawn.Spawned || pawn.Dead) return default;
        float support;
        if (!effect.Active) support = Of(SupportStone.Serenite, pawn.GetRoom()).Passive;
        else
        {
            if (!pawn.InBed() || pawn.jobs?.curDriver?.asleep != true) return default;
            if (!ActiveEffectKind.Solar.Metered) support = Of(SupportStone.Serenite, pawn.GetRoom()).Active;
            else support = RoomSupportCache.Sleeper(pawn) is { powered: true } sleeper ? sleeper.primary : 0;
        }
        return new PawnBonus(support, support * effect.Cap);
    }

    // The job check drops a pawn as soon as it stops meditating, rather than at the next cache interval.
    private static PawnBonus AnimastoneBonus(Pawn pawn, SupportEffect effect)
    {
        if (pawn.CurJobDef != JobDefOf.Meditate) return default;
        float support = RoomSupportCache.Meditator(pawn)?.primary ?? 0;
        return new PawnBonus(support, support * effect.Cap);
    }
}
