using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_Animastone : CompProperties
{
    public int fixtureIndex;
    public float weight;
    // Solar active effect while a pawn meditates within activeRadius. Zero radius means no active effect.
    public float activeRadius, activeDrain, activeFocusBonus, activeHeatRecovery, activeGrowth;
    public CompProperties_Animastone() => compClass = typeof(CompAnimastone);

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        string meditating = "Scales with room support. Applies only while a pawn meditates in the same enclosed room.";
        var rows = new InfoStats()
            .Add("Support weight", weight.ToStringPercent(), "This fixture's share of Animastone room support. " + InfoStats.SupportRule)
            .Add("Neural heat recovery at full support", InfoStats.Plus(AnimastoneSupport.HeatRecoveryCap), meditating)
            .Add("Psyfocus gain at full support", InfoStats.Plus(AnimastoneSupport.FocusGainCap), meditating);
        if (activeRadius <= 0) return rows.Rows;
        string active = $"Applies while a pawn meditates within {InfoStats.Tiles(activeRadius)}, indoors or outdoors." + InfoStats.SolarRule +
            (SolarRules.Enabled ? $" Costs {activeDrain:0.##} Solar energy a day while running." : "");
        rows.Add(InfoStats.Solar("Active effect radius"), InfoStats.Tiles(activeRadius), active);
        if (activeFocusBonus > 0) rows.Add(InfoStats.Solar("Meditation focus strength"), InfoStats.Plus(activeFocusBonus), active);
        if (activeHeatRecovery > 0) rows.Add(InfoStats.Solar("Extra neural heat recovery"), InfoStats.Plus(activeHeatRecovery), active);
        if (activeGrowth > 0) rows.Add(InfoStats.Solar("Anima grass growth"), InfoStats.Plus(activeGrowth), "Speeds anima grass growth around an anima tree in range. " + active);
        return rows.Rows;
    }
}

public class CompAnimastone : ThingComp
{
    public CompProperties_Animastone Props => (CompProperties_Animastone)props;

    // Set by RoomSupportCache each interval: a pawn meditates in range and the active effect was paid for.
    public bool poweredNow;

    public override void PostSpawnSetup(bool respawningAfterLoad) => parent.Map.GetComponent<RoomSupportCache>().Register(this);
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) => map.GetComponent<RoomSupportCache>().Unregister(this);

    public override string CompInspectStringExtra()
    {
        float support = AnimastoneSupport.Fraction(parent.GetRoom());
        string text = $"Room support: {support.ToStringPercent()}. While meditating: neural heat recovery +{(support * AnimastoneSupport.HeatRecoveryCap).ToStringPercent()}, psyfocus gain +{(support * AnimastoneSupport.FocusGainCap).ToStringPercent()}";
        if (Props.activeRadius > 0)
            text += "\nActive effect: " + (poweredNow ? "running" : "idle, no one meditating in range");
        return text;
    }
}

public static class AnimastoneSupport
{
    public static float HeatRecoveryCap = 0.5f, FocusGainCap = 0.25f;

    // Each category counts once at its highest weight, like Serenite and Vitalite.
    public static float Fraction(Room room, bool activeOnly = false) =>
        Mathf.Clamp01(RoomSupportCache.Total(RoomSupportCache.AnimastoneWeights(room, activeOnly && SolarRules.Enabled)));

    // Cached room support for a pawn meditating in a supported room. The job check drops a pawn as soon as it stops,
    // rather than at the next cache interval.
    public static float MeditationFraction(Pawn pawn)
    {
        if (pawn.CurJobDef != JobDefOf.Meditate) return 0;
        return RoomSupportCache.Meditator(pawn)?.primary ?? 0;
    }

    // Extra neural heat recovery from a powered pylon in range, on top of room support.
    public static float ActiveHeatRecovery(Pawn pawn)
    {
        if (pawn.CurJobDef != JobDefOf.Meditate) return 0;
        return RoomSupportCache.Meditator(pawn)?.secondary ?? 0;
    }
}

// Raises an Animastone fixture's focus strength while its Solar active effect runs.
public class FocusStrengthOffset_AnimastoneActive : FocusStrengthOffset
{
    public override float GetOffset(Thing parent, Pawn user = null) =>
        parent.TryGetComp<CompAnimastone>() is { poweredNow: true } comp ? comp.Props.activeFocusBonus : 0;

    public override string GetExplanation(Thing parent) =>
        GetOffset(parent) > 0 ? "Animastone Solar active effect: +" + GetOffset(parent).ToStringPercent() : null;
}

// Speeds anima grass growth from meditation at a tree within range of a powered Animastone pylon.
public class StatPart_AnimastoneGrowth : StatPart
{
    private static float Bonus(StatRequest req) => req.Thing is { Spawned: true } thing ? RoomSupportCache.ActiveGrowth(thing) : 0;

    public override void TransformValue(StatRequest req, ref float val) => val += Bonus(req);

    public override string ExplanationPart(StatRequest req)
    {
        float bonus = Bonus(req);
        return bonus > 0 ? "Powered Animastone pylon: +" + bonus.ToStringPercent() : null;
    }
}

public class StatPart_Animastone : StatPart
{
    public bool focusGain;

    private float Bonus(StatRequest req) => req.Thing is Pawn pawn
        ? focusGain ? AnimastoneSupport.MeditationFraction(pawn) * AnimastoneSupport.FocusGainCap
            : AnimastoneSupport.MeditationFraction(pawn) * AnimastoneSupport.HeatRecoveryCap + AnimastoneSupport.ActiveHeatRecovery(pawn)
        : 0;

    public override void TransformValue(StatRequest req, ref float val) => val *= 1 + Bonus(req);

    public override string ExplanationPart(StatRequest req)
    {
        float bonus = Bonus(req);
        return bonus > 0 ? "Animastone room support: +" + bonus.ToStringPercent() : null;
    }
}
