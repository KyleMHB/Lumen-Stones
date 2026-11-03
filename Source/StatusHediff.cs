using System.Text;
using RimWorld;
using Verse;

namespace LumenStones;

// A short-lived hediff whose tooltip lists the live numbers of an effect, so rates can be read in the health tab.
// The owning pass refreshes it each interval and the Disappears comp clears it soon after the effect stops.
public class Hediff_Status : HediffWithComps
{
    public string bracket, note;

    public override string LabelInBrackets => bracket;
    public override string TipStringExtra => note;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref bracket, "bracket");
        Scribe_Values.Look(ref note, "note");
    }

    // Adds or refreshes the pawn's status of this def and restarts its timer.
    public static Hediff_Status Apply(Pawn pawn, HediffDef def, string bracket, string note, int ticks, float severity = -1f)
    {
        var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def) as Hediff_Status;
        if (hediff == null)
        {
            hediff = (Hediff_Status)HediffMaker.MakeHediff(def, pawn);
            pawn.health.AddHediff(hediff);
        }
        if (severity >= 0f) hediff.Severity = severity;
        hediff.bracket = bracket;
        hediff.note = note;
        if (hediff.TryGetComp<HediffComp_Disappears>() is { } timer) timer.ticksToDisappear = ticks;
        return hediff;
    }

    // The numbers a stage applies, as plain lines for a tooltip.
    public static string Describe(HediffStage stage)
    {
        var text = new StringBuilder();
        if (stage == null) return "";
        if (stage.painFactor != 1f) text.AppendLine("Pain taken: " + stage.painFactor.ToStringPercent("0"));
        if (stage.totalBleedFactor != 1f) text.AppendLine("Bleeding: " + stage.totalBleedFactor.ToStringPercent("0"));
        if (stage.capMods != null)
            foreach (var mod in stage.capMods)
                if (mod.postFactor != 1f) text.AppendLine(mod.capacity.LabelCap + ": " + mod.postFactor.ToStringPercent("0"));
        if (stage.statFactors != null)
            foreach (var factor in stage.statFactors) text.AppendLine(factor.stat.LabelCap + ": " + factor.value.ToStringPercent("0"));
        if (stage.statOffsets != null)
            foreach (var offset in stage.statOffsets) text.AppendLine(offset.stat.LabelCap + ": " + offset.ValueToStringAsOffset);
        return text.ToString().TrimEnd();
    }
}
