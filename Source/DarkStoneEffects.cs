using RimWorld;
using Verse;

namespace LumenStones;

public class CompProperties_DarkStoneEffecter : CompProperties_FixtureEffecter
{
    public CompProperties_DarkStoneEffecter() => compClass = typeof(CompDarkStoneEffecter);
}

public class CompDarkStoneEffecter : CompFixtureEffecter
{
    private CompDarkFixture fixture;

    protected override bool ShouldShowEffecter() =>
        base.ShouldShowEffecter() && (fixture ??= parent.GetComp<CompDarkFixture>()) is { Active: true };

    public override void ReceiveCompSignal(string signal)
    {
        base.ReceiveCompSignal(signal);
        if (signal == WarpRules.ToggledSignal && !ShouldShowEffecter()) base.CompTick();
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        // The native off branch cleans up and clears the effecter after the parent leaves the map.
        base.CompTick();
        base.PostDeSpawn(map, mode);
    }
}
