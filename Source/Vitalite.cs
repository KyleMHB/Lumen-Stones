using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_Vitalite : CompProperties
{
    public int fixtureIndex = -1;
    public float weight;
    // Beds apply these directly. Room fixtures scale the StoneSettings caps by their category weights instead.
    public float healingBonus, immunityBonus, infectionPrevention;

    public CompProperties_Vitalite() => compClass = typeof(CompVitalite);

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        var rows = new InfoStats();
        string patient = "Applies to patients resting in bed in the same enclosed, roofed room.";
        string drain = SolarRules.Enabled ? $" Costs {VitaliteSupport.PatientSolarDrain:0.##} Solar energy a day for each injured patient helped." : "";
        if (fixtureIndex >= 0)
        {
            rows.Add("Support weight", weight.ToStringPercent(), "This fixture's share of Vitalite room support. " + InfoStats.SupportRule)
                .Add("Immunity at full support", InfoStats.Plus(StoneSettings.RoomImmunityCap), "Scales with room support. " + patient)
                .Add(InfoStats.Solar("Healing at full support"), InfoStats.Plus(StoneSettings.RoomHealingCap), "Scales with the room support of fixtures whose active effect is on. " + patient + InfoStats.SolarRule + drain)
                .Add(InfoStats.Solar("Infection prevention at full support"), StoneSettings.RoomInfectionPreventionCap.ToStringPercent(), "Lowers the chance a new wound becomes infected. Existing infections are unaffected. " + patient + InfoStats.SolarRule);
        }
        else
        {
            string bed = "Applies to this bed's resting occupant. Room support adds to it.";
            rows.Add("Occupant immunity", InfoStats.Plus(immunityBonus), bed)
                .Add(InfoStats.Solar("Occupant healing"), InfoStats.Plus(healingBonus), "Applies to an injured occupant. " + bed + InfoStats.SolarRule + drain)
                .Add(InfoStats.Solar("Occupant infection prevention"), infectionPrevention.ToStringPercent(), "Combines with room prevention multiplicatively. " + bed + InfoStats.SolarRule);
        }
        return rows.Rows;
    }
}

public class CompVitalite : ThingComp
{
    public CompProperties_Vitalite Props => (CompProperties_Vitalite)props;

    public override void PostSpawnSetup(bool respawningAfterLoad) => parent.Map.GetComponent<RoomSupportCache>().Register(this);
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) => map.GetComponent<RoomSupportCache>().Unregister(this);

    public override void CompTick()
    {
        // The interval check comes first: it is arithmetic, while the animation switch is a comp lookup.
        if (!parent.Spawned || !parent.IsHashIntervalTick(240) || parent.Position.Fogged(parent.Map) || !CompAnimationToggle.On(parent)) return;
        for (int i = 0; i < 4; i++)
        {
            var mote = FixtureMotes.Make("KM_Mote_VitalitePulse", parent.Map);
            if (mote == null) return;
            mote.exactPosition = parent.DrawPos + parent.def.graphicData.drawOffset;
            mote.exactPosition.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            mote.Scale = 0.10f;
            mote.instanceColor = i % 2 == 0 ? new Color(1f, 0.96f, 0.94f) : new Color(1f, 0.72f, 0.80f);
            mote.airTimeLeft = 1f;
            mote.SetVelocity(i * 90f + parent.thingIDNumber % 90, 0.25f);
            GenSpawn.Spawn(mote, parent.Position, parent.Map);
        }
    }

    public override string CompInspectStringExtra()
    {
        var text = new StringBuilder();
        if (Props.fixtureIndex >= 0)
        {
            var room = parent.GetRoom();
            float support = VitaliteSupport.Fraction(room);
            text.Append($"Room support: {support.ToStringPercent()}. Immunity +{VitaliteSupport.RoomBonus(room, true).ToStringPercent()}, healing +{VitaliteSupport.RoomBonus(room, false).ToStringPercent()}, infection prevention {VitaliteSupport.RoomInfectionPrevention(room).ToStringPercent()}");
            if (support < 1f)
            {
                var missing = VitaliteSupport.MissingFixtures(room);
                if (missing.Length > 0) text.Append("\nMissing: " + missing);
            }
        }
        else if (parent is Building_Bed bed)
            foreach (var occupant in bed.CurOccupants)
                text.AppendLine($"{occupant.LabelShortCap}: immunity +{VitaliteSupport.Bonus(occupant, true).ToStringPercent("0.#")}, healing +{VitaliteSupport.Bonus(occupant, false).ToStringPercent("0.#")}, infection chance -{(1f - VitaliteSupport.InfectionChanceFactor(occupant)).ToStringPercent("0.#")}");
        return text.ToString().TrimEndNewlines();
    }
}

public static class VitaliteSupport
{
    public const float MaxRoomHealingBonus = 0.25f;
    public const float MaxRoomImmunityBonus = 0.10f;
    public const float MaxRoomInfectionPrevention = 0.10f;
    private static readonly string[] FixtureNames = { "column", "lamp", "pylon", "restorative cluster" };

    // Each category counts once at its highest weight, so duplicate lamps never add support.
    // Solar energy per day for each injured patient in bed who gets an active bonus.
    public static float PatientSolarDrain = 20f;

    private static float[] CategoryWeights(Room room) => RoomSupportCache.VitaliteWeights(room, false);

    // Passive support counts every fixture. Active support counts only fixtures toggled on, and with Sunstone off it counts all.
    public static float Fraction(Room room, bool activeOnly = false) =>
        Mathf.Clamp01(RoomSupportCache.Total(RoomSupportCache.VitaliteWeights(room, activeOnly && SolarRules.Enabled)));

    // Immunity is passive. Healing is the active effect.
    public static float RoomBonus(Room room, bool immunity) =>
        Fraction(room, !immunity) * (immunity ? StoneSettings.RoomImmunityCap : StoneSettings.RoomHealingCap);

    public static float RoomInfectionPrevention(Room room) => Fraction(room, true) * StoneSettings.RoomInfectionPreventionCap;

    public static float InfectionChanceFactor(Pawn pawn)
    {
        if (!pawn.Spawned || pawn.Dead || !pawn.InBed()) return 1;
        var room = pawn.GetRoom();
        if (room == null || !room.ProperRoom || room.OpenRoofCount > 0) return 1;
        if (SolarRules.Enabled)
        {
            var patient = RoomSupportCache.Patient(pawn);
            return patient is { powered: true } ? 1 - patient.secondary : 1;
        }
        float bed = pawn.CurrentBed().TryGetComp<CompVitalite>()?.Props.infectionPrevention ?? 0;
        return (1 - Mathf.Clamp01(bed)) * (1 - RoomInfectionPrevention(room));
    }

    public static string MissingFixtures(Room room)
    {
        var weights = CategoryWeights(room);
        var missing = new List<string>();
        for (int i = 0; i < FixtureNames.Length; i++)
            if (weights[i] <= 0) missing.Add(FixtureNames[i]);
        return string.Join(", ", missing);
    }

    public static float Bonus(Pawn pawn, bool immunity)
    {
        if (!pawn.Spawned || pawn.Dead || !pawn.InBed()) return 0f;
        // Healing costs Solar energy, and the cache has already applied the room, the bed and the powered state.
        if (!immunity && SolarRules.Enabled)
        {
            var patient = RoomSupportCache.Patient(pawn);
            return patient is { powered: true } ? patient.primary : 0f;
        }
        float bonus = RoomBonus(pawn.GetRoom(), immunity);
        var comp = pawn.CurrentBed().TryGetComp<CompVitalite>();
        if (comp != null)
            bonus += immunity ? comp.Props.immunityBonus : comp.Props.healingBonus;
        return bonus;
    }
}

// Native healing and immunity still run normally. Only their live stat values change,
// so leaving bed or changing fixtures cannot leave behind a persistent health effect.
public class StatPart_Vitalite : StatPart
{
    public bool immunity;

    public override void TransformValue(StatRequest req, ref float val)
    {
        if (req.Thing is Pawn pawn) val += VitaliteSupport.Bonus(pawn, immunity);
    }

    public override string ExplanationPart(StatRequest req)
    {
        if (req.Thing is not Pawn pawn) return null;
        float bonus = VitaliteSupport.Bonus(pawn, immunity);
        return bonus > 0f ? "Vitalite support: +" + bonus.ToStringPercent() : null;
    }
}

// Lists a resting patient's current Vitalite bonuses as a health-tab hediff, since the bonuses are otherwise only stat parts.
public class VitaliteStatus : MapComponent
{
    public const int PassOffset = 200;
    private static HediffDef status;
    private static HediffDef Status => status ??= DefDatabase<HediffDef>.GetNamed("KM_VitaliteSupport");

    public VitaliteStatus(Map map) : base(map) { }

    public override void MapComponentTick()
    {
        if (Find.TickManager.TicksGame % RoomSupportCache.Interval != PassOffset) return;
        Pass(map.mapPawns.FreeColonistsSpawned);
        Pass(map.mapPawns.PrisonersOfColonySpawned);
        Pass(map.mapPawns.SlavesOfColonySpawned);
    }

    private static void Pass(List<Pawn> pawns)
    {
        foreach (var pawn in pawns)
        {
            if (!pawn.InBed()) continue;
            float healing = VitaliteSupport.Bonus(pawn, false), immunity = VitaliteSupport.Bonus(pawn, true);
            float infection = 1f - VitaliteSupport.InfectionChanceFactor(pawn);
            if (healing <= 0f && immunity <= 0f && infection <= 0f) continue;
            var note = new StringBuilder();
            note.AppendLine("Immunity gain: +" + immunity.ToStringPercent("0.#"));
            note.AppendLine("Wound healing: +" + healing.ToStringPercent("0.#"));
            note.Append("Infection chance: -" + infection.ToStringPercent("0.#"));
            if (SolarRules.Enabled && healing <= 0f && infection <= 0f) note.Append("\nHealing and infection prevention need an injury and powered Solar energy.");
            Hediff_Status.Apply(pawn, Status, null, note.ToString(), RoomSupportCache.Interval + 50);
        }
    }
}
