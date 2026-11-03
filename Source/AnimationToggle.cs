using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_AnimationToggle : CompProperties
{
    public CompProperties_AnimationToggle() => compClass = typeof(CompAnimationToggle);
}

// A per-fixture switch for its decorative animation: pulses, glints and fireflies. It changes nothing a fixture does.
// Fixtures that animate get this comp at startup, so every def with an animation source has the button.
public class CompAnimationToggle : ThingComp
{
    public const string Signal = "AnimationToggled";
    // Off until the player switches it on, so fixtures add no pulses, glints or fireflies by default.
    private bool on;

    public override void PostExposeData() => Scribe_Values.Look(ref on, "animationOn");

    // True when the thing has no switch, so unswitched things animate as before. A switched fixture also needs the global setting.
    public static bool On(Thing thing) => thing is not ThingWithComps comp || comp.GetComp<CompAnimationToggle>() is not { } toggle ||
        toggle.on && LumenstoneMod.Settings.fixtureAnimation;

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (!LumenstoneMod.Settings.fixtureAnimation) yield break;
        yield return new Command_Toggle
        {
            defaultLabel = "Fixture animation",
            defaultDesc = "Show this fixture's pulses, glints and drifting lights. Turning it off changes nothing else it does.",
            icon = TexCommand.ForbidOn,
            isActive = () => on,
            toggleAction = () => { on = !on; parent.BroadcastCompSignal(Signal); }
        };
    }
}

// Decorative pulse motes. They are skipped when the map already has many motes, as vanilla does for its own low-priority motes.
public static class FixtureMotes
{
    public static MoteThrown Make(string defName, Map map) =>
        map.moteCounter.SaturatedLowPriority ? null : (MoteThrown)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(defName));
}

// Gives the animation switch to every def of this mod that has an animation source.
[StaticConstructorOnStartup]
public static class AnimationToggleSetup
{
    static AnimationToggleSetup()
    {
        foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (def.comps == null || def.modContentPack?.PackageId != "kylemhb.lumenstones" || def.comps.Exists(c => c is CompProperties_AnimationToggle) || !Animates(def)) continue;
            def.comps.Add(new CompProperties_AnimationToggle());
        }
    }

    private static bool Animates(ThingDef def)
    {
        foreach (var comp in def.comps)
            switch (comp)
            {
                case CompProperties_Fireflies or CompProperties_Effecter or CompProperties_Vitalite or CompProperties_Serenite
                    or CompProperties_Verdite or CompProperties_LumenstoneFixture or CompProperties_Aegite:
                    return true;
                case CompProperties_Ferrite { personal: false }:
                    return true;
            }
        return false;
    }
}

// An effecter that also stops when the fixture's animation is switched off. Vanilla CompEffecter only knows its own conditions.
public class CompProperties_FixtureEffecter : CompProperties_Effecter
{
    public CompProperties_FixtureEffecter() => compClass = typeof(CompFixtureEffecter);
}

public class CompFixtureEffecter : CompEffecter
{
    protected override bool ShouldShowEffecter() => base.ShouldShowEffecter() && CompAnimationToggle.On(parent);

    public override void ReceiveCompSignal(string signal)
    {
        base.ReceiveCompSignal(signal);
        if (signal == CompAnimationToggle.Signal && !ShouldShowEffecter()) base.CompTick();
    }
}
