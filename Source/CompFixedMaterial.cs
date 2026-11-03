using RimWorld;
using Verse;

namespace LumenStones;

// Floor torches were once stuffable. A saved material would still set their draw colour
// after the definition stopped using stuff, including while minified.
public class CompFixedMaterial : ThingComp
{
    public override void PostExposeData()
    {
        if (Scribe.mode == LoadSaveMode.PostLoadInit && parent.Stuff != null) parent.SetStuffDirect(null);
    }
}
