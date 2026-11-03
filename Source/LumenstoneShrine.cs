using RimWorld;
using Verse;

namespace LumenStones;

// A shrine pairs a ritual host with its own stone's fixtures. The other family's host or fixtures, beds and
// unrelated workstations disqualify it, so each shrine needs a room to itself.
public abstract class RoomRoleWorker_Shrine : RoomRoleWorker
{
    // Above every vanilla role that can apply: the host counts as a worktable and would otherwise compete for Workshop.
    private const float ShrineScore = 100000f;

    protected abstract bool Dark { get; }

    public override float GetScore(Room room)
    {
        if (!room.ProperRoom) return 0;
        bool host = false, fixture = false;
        foreach (var thing in room.ContainedAndAdjacentThings)
        {
            if (thing.GetRoom() != room) continue;
            if (thing is Building_Bed || thing is Building_ResearchBench) return 0;
            // A circle can't host a shrine, but one left beside an upgraded altar shouldn't cancel a Lumenstone shrine.
            if (thing is Building_LumenCircle)
            {
                if (Dark) return 0;
            }
            else if (thing is IRitualSupportHost ritualHost)
            {
                if (ritualHost.Dark != Dark) return 0;
                host = true;
            }
            else if (thing is Building_WorkTable) return 0;
            // Sanguinite, Dreadstone and Hollowstone fixtures break a Lumenstone shrine and leave a Dark Stone shrine alone.
            if (!Dark && thing.TryGetComp<CompDarkFixture>() != null) return 0;
            var comp = thing.TryGetComp<CompLumenstoneFixture>();
            if (comp == null) continue;
            if (comp.Props.dark != Dark) return 0;
            fixture = true;
        }
        return host && fixture ? ShrineScore : 0;
    }
}

public class RoomRoleWorker_LumenstoneShrine : RoomRoleWorker_Shrine { protected override bool Dark => false; }
public class RoomRoleWorker_DarkStoneShrine : RoomRoleWorker_Shrine { protected override bool Dark => true; }
