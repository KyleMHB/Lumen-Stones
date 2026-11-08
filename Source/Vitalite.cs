using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_Vitalite : CompProperties_Fixture, ITunableProps
{
    // Beds apply these directly. Room fixtures scale the SupportEffect caps by their category weights instead.
    public float healingBonus, immunityBonus, infectionPrevention;

    public CompProperties_Vitalite() => compClass = typeof(CompVitalite);

    public void DeclareTunables(TunableScope s, Def owner)
    {
        var def = (ThingDef)owner;
        string name = def.defName;
        if (category != FixtureCategory.None)
            s.Add(name + ".support", "Category room contribution (%)", weight, v => weight = v, Limit.Percent());
        else
        {
            s.Add(name + ".healing", "Healing contribution (%)", healingBonus, v => healingBonus = v, Limit.Percent(10));
            s.Add(name + ".immunity", "Immunity contribution (%)", immunityBonus, v => immunityBonus = v, Limit.Percent(10));
            s.Add(name + ".infection", "Infection prevention (%)", infectionPrevention, v => infectionPrevention = v, Limit.Percent());
        }
        float cleanliness = def.statBases?.FirstOrDefault(stat => stat.stat == StatDefOf.Cleanliness)?.value ?? 0;
        s.Add(name + ".cleanliness", "Cleanliness", cleanliness, v => def.SetStatBaseValue(StatDefOf.Cleanliness, v));
    }

    protected override bool IsFixture(ThingDef parentDef) => !parentDef.IsBed;

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        var rows = new InfoStats();
        string patient = "Applies to patients resting in bed in the same enclosed, roofed room.";
        string drain = SolarRules.Enabled ? $" Costs {SupportStone.Vitalite.OccupantDrain:0.##} Solar energy a day for each injured patient helped." : "";
        if (category != FixtureCategory.None)
        {
            rows.Add("Support weight", weight.ToStringPercent(), "This fixture's share of Vitalite room support. " + InfoStats.SupportRule)
                .Add("Immunity at full support", InfoStats.Plus(SupportEffect.VitaliteImmunity.Cap), "Scales with room support. " + patient)
                .Add(InfoStats.Solar("Healing at full support"), InfoStats.Plus(SupportEffect.VitaliteHealing.Cap), "Scales with the room support of fixtures whose active effect is on. " + patient + InfoStats.SolarRule + drain)
                .Add(InfoStats.Solar("Infection prevention at full support"), SupportEffect.VitaliteInfection.Cap.ToStringPercent(), "Lowers the chance a new wound becomes infected. Existing infections are unaffected. " + patient + InfoStats.SolarRule);
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

public class CompVitalite : CompRoomFixture
{
    public CompProperties_Vitalite Props => (CompProperties_Vitalite)props;
    protected override SupportStone Stone => SupportStone.Vitalite;

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
        if (Props.category != FixtureCategory.None)
        {
            var reading = FixtureSupport.Of(SupportStone.Vitalite, parent);
            text.Append($"Room support: {reading.Passive.ToStringPercent()}. Immunity +{reading.Bonus(SupportEffect.VitaliteImmunity).ToStringPercent()}, healing +{reading.Bonus(SupportEffect.VitaliteHealing).ToStringPercent()}, infection prevention {reading.Bonus(SupportEffect.VitaliteInfection).ToStringPercent()}");
            if (reading.Passive < 1f)
            {
                var missing = reading.Missing();
                if (missing.Length > 0) text.Append("\nMissing: " + missing);
            }
        }
        else if (parent is Building_Bed bed)
            foreach (var occupant in bed.CurOccupants)
                text.AppendLine($"{occupant.LabelShortCap}: immunity +{FixtureSupport.Bonus(occupant, SupportEffect.VitaliteImmunity).Value.ToStringPercent("0.#")}, healing +{FixtureSupport.Bonus(occupant, SupportEffect.VitaliteHealing).Value.ToStringPercent("0.#")}, infection chance -{FixtureSupport.Bonus(occupant, SupportEffect.VitaliteInfection).Value.ToStringPercent("0.#")}");
        return text.ToString().TrimEndNewlines();
    }
}

// Native healing and immunity still run normally. Only their live stat values change,
// so leaving bed or changing fixtures cannot leave behind a persistent health effect.
public class StatPart_Vitalite : StatPart
{
    public bool immunity;

    public override void TransformValue(StatRequest req, ref float val)
    {
        if (req.Thing is Pawn pawn) val += FixtureSupport.Bonus(pawn, immunity ? SupportEffect.VitaliteImmunity : SupportEffect.VitaliteHealing).Value;
    }

    public override string ExplanationPart(StatRequest req)
    {
        if (req.Thing is not Pawn pawn) return null;
        float bonus = FixtureSupport.Bonus(pawn, immunity ? SupportEffect.VitaliteImmunity : SupportEffect.VitaliteHealing).Value;
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
            float healing = FixtureSupport.Bonus(pawn, SupportEffect.VitaliteHealing).Value, immunity = FixtureSupport.Bonus(pawn, SupportEffect.VitaliteImmunity).Value;
            float infection = FixtureSupport.Bonus(pawn, SupportEffect.VitaliteInfection).Value;
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

public static class VitaliteTunables
{
    internal static void DeclareSolarTunables(TunableScope s) =>
        s.In("Solar energy").Add("Vitalite.patientSolarDrain", "Solar energy per day for each injured patient treated", SupportStone.Vitalite.OccupantDrain,
            v => SupportStone.Vitalite.OccupantDrain = v);

    internal static void DeclareRoomTunables(TunableScope s)
    {
        var room = s.In("Room support");
        room.Add("Vitalite.healingCap", "Room healing cap (%)", SupportEffect.VitaliteHealing.Cap, v => SupportEffect.VitaliteHealing.Cap = v, Limit.Percent(10));
        room.Add("Vitalite.immunityCap", "Room immunity cap (%)", SupportEffect.VitaliteImmunity.Cap, v => SupportEffect.VitaliteImmunity.Cap = v, Limit.Percent(10));
        room.Add("Vitalite.infectionCap", "Room infection prevention cap (%)", SupportEffect.VitaliteInfection.Cap,
            v => SupportEffect.VitaliteInfection.Cap = v, Limit.Percent());
    }
}
