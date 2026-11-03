using System.Linq;
using RimWorld;
using Verse;

namespace LumenStones;

public class ThoughtWorker_SerenitePendant : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn p) =>
        p.apparel?.WornApparel.Any(a => a.def.defName == "KM_SerenitePendant") == true;
}
