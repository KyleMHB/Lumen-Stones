using System.Linq;
using RimWorld;
using Verse;

namespace LumenStones;

public static class SerenitePendantRules
{
    internal static void DeclareTunables(TunableScope s)
    {
        var pendantMood = DefDatabase<ThoughtDef>.GetNamed("KM_SerenitePendantWorn").stages[0];
        s.In("Lumen_SerenitePendant_Section".Translate()).PerItem().Add("KM_SerenitePendant.mood", "Lumen_SerenitePendant_Mood".Translate(), pendantMood.baseMoodEffect,
            v => pendantMood.baseMoodEffect = v, Limit.Number(-100, 100));
    }
}

public class ThoughtWorker_SerenitePendant : ThoughtWorker
{
    private static ThingDef pendant;
    private static ThingDef Pendant => pendant ??= DefDatabase<ThingDef>.GetNamed("KM_SerenitePendant");

    protected override ThoughtState CurrentStateInternal(Pawn p) => Effects.SerenitePendant.On && NewEquipment.Wears(p, Pendant);
}
