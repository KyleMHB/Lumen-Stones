using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_Animastone : CompProperties_Fixture, ITunableProps
{
    // Solar active effect while a pawn meditates within activeRadius. Zero radius means no active effect.
    public float activeRadius, activeDrain, activeFocusBonus, activeHeatRecovery, activeGrowth;
    public CompProperties_Animastone() => compClass = typeof(CompAnimastone);

    public void DeclareTunables(TunableScope s, Def owner)
    {
        string name = owner.defName;
        s.Add(name + ".support", "Category room contribution (%)", weight, v => weight = v, Limit.Percent());
        if (activeRadius <= 0) return;
        s.Add(name + ".activeRadius", "Solar active effect radius (tiles)", activeRadius, v => activeRadius = v, Limit.Number(1, 50));
        s.Add(name + ".activeDrain", "Solar energy per day while a pawn meditates in range", activeDrain, v => activeDrain = v);
        if (activeFocusBonus > 0)
            s.Add(name + ".activeFocus", "Active focus strength bonus (%)", activeFocusBonus, v => activeFocusBonus = v, Limit.Percent());
        if (activeHeatRecovery > 0)
            s.Add(name + ".activeHeatRecovery", "Active neural heat recovery bonus (%)", activeHeatRecovery, v => activeHeatRecovery = v, Limit.Percent(10));
        if (activeGrowth > 0)
            s.Add(name + ".activeGrowth", "Active anima grass growth offset (%)", activeGrowth, v => activeGrowth = v, Limit.Percent(10));
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        string meditating = "Scales with room support. Applies only while a pawn meditates in the same enclosed room.";
        var rows = new InfoStats()
            .Add("Support weight", weight.ToStringPercent(), "This fixture's share of Animastone room support. " + InfoStats.SupportRule)
            .Add("Neural heat recovery at full support", InfoStats.Plus(SupportEffect.AnimastoneHeatRecovery.Cap), meditating)
            .Add("Psyfocus gain at full support", InfoStats.Plus(SupportEffect.AnimastoneFocusGain.Cap), meditating);
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

public class CompAnimastone : CompRoomFixture
{
    public CompProperties_Animastone Props => (CompProperties_Animastone)props;

    // Set by RoomSupportCache each interval: a pawn meditates in range and the active effect was paid for.
    public bool poweredNow;

    protected override SupportStone Stone => SupportStone.Animastone;

    public override string CompInspectStringExtra()
    {
        var reading = FixtureSupport.Of(SupportStone.Animastone, parent);
        string text = $"Room support: {reading.Passive.ToStringPercent()}. While meditating: neural heat recovery +{reading.Bonus(SupportEffect.AnimastoneHeatRecovery).ToStringPercent()}, psyfocus gain +{reading.Bonus(SupportEffect.AnimastoneFocusGain).ToStringPercent()}";
        if (Props.activeRadius > 0)
            text += "\nActive effect: " + (poweredNow ? "running" : "idle, no one meditating in range");
        return text;
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
        ? focusGain ? FixtureSupport.Bonus(pawn, SupportEffect.AnimastoneFocusGain).Value
            : FixtureSupport.Bonus(pawn, SupportEffect.AnimastoneHeatRecovery).Value + RoomSupportCache.ActiveHeatRecovery(pawn)
        : 0;

    public override void TransformValue(StatRequest req, ref float val) => val *= 1 + Bonus(req);

    public override string ExplanationPart(StatRequest req)
    {
        float bonus = Bonus(req);
        return bonus > 0 ? "Animastone room support: +" + bonus.ToStringPercent() : null;
    }
}

public static class AnimastoneTunables
{
    public static readonly string[] Fixtures = { "KM_LumenAnimastoneColumn", "KM_AnimastoneLamp", "KM_AnimastoneWallLamp", "KM_AnimastoneCluster", "KM_AnimastonePylon" };

    internal static void DeclareTunables(TunableScope s)
    {
        string[] fixtures = Fixtures;
        var focus = DefDatabase<StatDef>.GetNamed("MeditationFocusStrength");
        foreach (string name in fixtures)
        {
            var def = DefDatabase<ThingDef>.GetNamed(name);
            s.In(def.LabelCap).Add(name + ".focus", "Meditation focus strength (%)", def.GetStatValueAbstract(focus),
                v => def.SetStatBaseValue(focus, v), Limit.Percent());
        }
        var room = s.In("Room support");
        room.Add("Animastone.heatRecoveryCap", "Neural heat recovery cap while meditating (%)", SupportEffect.AnimastoneHeatRecovery.Cap,
            v => SupportEffect.AnimastoneHeatRecovery.Cap = v, Limit.Percent(10));
        room.Add("Animastone.focusGainCap", "Psyfocus gain cap while meditating (%)", SupportEffect.AnimastoneFocusGain.Cap,
            v => SupportEffect.AnimastoneFocusGain.Cap = v, Limit.Percent(10));
    }
}
