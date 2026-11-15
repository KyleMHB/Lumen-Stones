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
            s.Add(name + ".support", "Lumen_Setting_CategoryContribution".Translate(), weight, v => weight = v, Limit.Percent());
        else
        {
            s.Add(name + ".healing", "Lumen_Vitalite_HealingContribution".Translate(), healingBonus, v => healingBonus = v, Limit.Percent(10));
            s.Add(name + ".immunity", "Lumen_Vitalite_ImmunityContribution".Translate(), immunityBonus, v => immunityBonus = v, Limit.Percent(10));
            s.Add(name + ".infection", "Lumen_Vitalite_InfectionContribution".Translate(), infectionPrevention, v => infectionPrevention = v, Limit.Percent());
        }
        float cleanliness = def.statBases?.FirstOrDefault(stat => stat.stat == StatDefOf.Cleanliness)?.value ?? 0;
        s.Add(name + ".cleanliness", StatDefOf.Cleanliness.LabelCap, cleanliness, v => def.SetStatBaseValue(StatDefOf.Cleanliness, v));
    }

    protected override bool IsFixture(ThingDef parentDef) => !parentDef.IsBed;

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        var rows = new InfoStats();
        string patient = "Lumen_Vitalite_PatientRule".Translate();
        string drain = SolarRules.Enabled ? " " + "Lumen_Vitalite_PatientDrain".Translate(SupportStone.Vitalite.OccupantDrain.ToString("0.##")) : "";
        if (category != FixtureCategory.None)
        {
            rows.Add("Lumen_Info_SupportWeight".Translate(), weight.ToStringPercent(), "Lumen_Vitalite_SupportWeightDesc".Translate() + " " + InfoStats.SupportRule)
                .Add(Effects.VitaliteRoomImmunity, "Lumen_Vitalite_Immunity".Translate(), InfoStats.Plus(SupportEffect.VitaliteImmunity.Cap), "Lumen_Info_ScalesWithSupport".Translate() + " " + patient)
                .Add(Effects.VitaliteRoomHealing, InfoStats.Solar("Lumen_Vitalite_Healing".Translate()), InfoStats.Plus(SupportEffect.VitaliteHealing.Cap), "Lumen_Info_ScalesWithActiveSupport".Translate() + " " + patient + InfoStats.SolarRule + drain)
                .Add(Effects.VitaliteRoomInfection, InfoStats.Solar("Lumen_Vitalite_Infection".Translate()), SupportEffect.VitaliteInfection.Cap.ToStringPercent(), "Lumen_Vitalite_InfectionDesc".Translate() + " " + patient + InfoStats.SolarRule);
        }
        else
        {
            string bed = "Lumen_Vitalite_BedRule".Translate();
            var on = Effects.VitaliteBedBonuses;
            rows.Add(on, "Lumen_Vitalite_OccupantImmunity".Translate(), InfoStats.Plus(immunityBonus), bed)
                .Add(on, InfoStats.Solar("Lumen_Vitalite_OccupantHealing".Translate()), InfoStats.Plus(healingBonus), "Lumen_Vitalite_OccupantHealingDesc".Translate() + " " + bed + InfoStats.SolarRule + drain)
                .Add(on, InfoStats.Solar("Lumen_Vitalite_OccupantInfection".Translate()), infectionPrevention.ToStringPercent(), "Lumen_Vitalite_OccupantInfectionDesc".Translate() + " " + bed + InfoStats.SolarRule);
        }
        return rows.Rows;
    }
}

public class CompVitalite : CompRoomFixture
{
    public CompProperties_Vitalite Props => (CompProperties_Vitalite)props;
    protected override SupportStone Stone => SupportStone.Vitalite;

    public override string CompInspectStringExtra()
    {
        var text = new StringBuilder();
        if (Props.category != FixtureCategory.None)
        {
            var reading = FixtureSupport.Of(SupportStone.Vitalite, parent);
            text.Append("Lumen_Vitalite_Inspect".Translate(reading.Passive.ToStringPercent(), reading.Bonus(SupportEffect.VitaliteImmunity).ToStringPercent(),
                reading.Bonus(SupportEffect.VitaliteHealing).ToStringPercent(), reading.Bonus(SupportEffect.VitaliteInfection).ToStringPercent()));
            if (reading.Passive < 1f)
            {
                var missing = reading.Missing();
                if (missing.Length > 0) text.Append("\n" + "Lumen_Info_Missing".Translate(missing));
            }
        }
        else if (parent is Building_Bed bed)
            foreach (var occupant in bed.CurOccupants)
                text.AppendLine("Lumen_Vitalite_Occupant".Translate(occupant.LabelShortCap, FixtureSupport.Bonus(occupant, SupportEffect.VitaliteImmunity).Value.ToStringPercent("0.#"),
                    FixtureSupport.Bonus(occupant, SupportEffect.VitaliteHealing).Value.ToStringPercent("0.#"), FixtureSupport.Bonus(occupant, SupportEffect.VitaliteInfection).Value.ToStringPercent("0.#")));
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
        return bonus > 0f ? "Lumen_Vitalite_Support".Translate(bonus.ToStringPercent()).ToString() : null;
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
            note.AppendLine("Lumen_Vitalite_ImmunityGain".Translate(immunity.ToStringPercent("0.#")));
            note.AppendLine("Lumen_Vitalite_WoundHealing".Translate(healing.ToStringPercent("0.#")));
            note.Append("Lumen_Vitalite_InfectionChance".Translate(infection.ToStringPercent("0.#")));
            if (SolarRules.Enabled && healing <= 0f && infection <= 0f) note.Append("\n" + "Lumen_Vitalite_NeedsInjury".Translate());
            Hediff_Status.Apply(pawn, Status, null, note.ToString(), RoomSupportCache.Interval + 50);
        }
    }
}

public static class VitaliteTunables
{
    internal static void DeclareSolarTunables(TunableScope s) =>
        s.In("Lumen_Section_SolarEnergy".Translate()).Add("Vitalite.patientSolarDrain", "Lumen_Vitalite_PatientDrainSetting".Translate(), SupportStone.Vitalite.OccupantDrain,
            v => SupportStone.Vitalite.OccupantDrain = v);

    internal static void DeclareRoomTunables(TunableScope s)
    {
        var room = s.In("Lumen_Section_RoomSupport".Translate());
        room.Add("Vitalite.healingCap", "Lumen_Vitalite_HealingCap".Translate(), SupportEffect.VitaliteHealing.Cap, v => SupportEffect.VitaliteHealing.Cap = v, Limit.Percent(10));
        room.Add("Vitalite.immunityCap", "Lumen_Vitalite_ImmunityCap".Translate(), SupportEffect.VitaliteImmunity.Cap, v => SupportEffect.VitaliteImmunity.Cap = v, Limit.Percent(10));
        room.Add("Vitalite.infectionCap", "Lumen_Vitalite_InfectionCap".Translate(), SupportEffect.VitaliteInfection.Cap,
            v => SupportEffect.VitaliteInfection.Cap = v, Limit.Percent());
    }
}
