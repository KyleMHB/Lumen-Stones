using RimWorld;
using Verse;

namespace LumenStones;

// Gives the wielder a hediff while the weapon is equipped.
public class CompProperties_WielderHediff : CompProperties
{
    public HediffDef hediff;
    public CompProperties_WielderHediff() => compClass = typeof(CompWielderHediff);
}

public class CompWielderHediff : ThingComp
{
    private HediffDef Hediff => ((CompProperties_WielderHediff)props).hediff;

    public override void Notify_Equipped(Pawn pawn)
    {
        if (pawn.health.hediffSet.GetFirstHediffOfDef(Hediff) == null) pawn.health.AddHediff(Hediff);
    }

    public override void Notify_Unequipped(Pawn pawn)
    {
        if (pawn.health.hediffSet.GetFirstHediffOfDef(Hediff) is { } hediff) pawn.health.RemoveHediff(hediff);
    }
}
