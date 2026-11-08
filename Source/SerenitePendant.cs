using System.Linq;
using RimWorld;
using Verse;

namespace LumenStones;

public static class SerenitePendantRules
{
    internal static void DeclareTunables(TunableScope s)
    {
        var pendantMood = DefDatabase<ThoughtDef>.GetNamed("KM_SerenitePendantWorn").stages[0];
        s.In("Serenite pendant").Add("KM_SerenitePendant.mood", "Mood while worn", pendantMood.baseMoodEffect,
            v => pendantMood.baseMoodEffect = v, Limit.Number(-100, 100));
    }
}

public class ThoughtWorker_SerenitePendant : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p) =>
        p.apparel?.WornApparel.Any(a => a.def.defName == "KM_SerenitePendant") == true;
}
