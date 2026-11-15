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
        s.Add(name + ".support", "Lumen_Setting_CategoryContribution".Translate(), weight, v => weight = v, Limit.Percent());
        if (activeRadius <= 0) return;
        s.Add(name + ".activeRadius", "Lumen_Animastone_ActiveRadiusSetting".Translate(), activeRadius, v => activeRadius = v, Limit.Number(1, 50));
        s.Add(name + ".activeDrain", "Lumen_Animastone_ActiveDrain".Translate(), activeDrain, v => activeDrain = v);
        if (activeFocusBonus > 0)
            s.Add(name + ".activeFocus", "Lumen_Animastone_ActiveFocus".Translate(), activeFocusBonus, v => activeFocusBonus = v, Limit.Percent());
        if (activeHeatRecovery > 0)
            s.Add(name + ".activeHeatRecovery", "Lumen_Animastone_ActiveHeat".Translate(), activeHeatRecovery, v => activeHeatRecovery = v, Limit.Percent(10));
        if (activeGrowth > 0)
            s.Add(name + ".activeGrowth", "Lumen_Animastone_ActiveGrowth".Translate(), activeGrowth, v => activeGrowth = v, Limit.Percent(10));
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        string meditating = "Lumen_Animastone_MeditatingRule".Translate();
        var rows = new InfoStats()
            .Add("Lumen_Info_SupportWeight".Translate(), weight.ToStringPercent(), "Lumen_Animastone_SupportWeightDesc".Translate() + " " + InfoStats.SupportRule)
            .Add(Effects.AnimastoneRoomHeat, "Lumen_Animastone_HeatRecovery".Translate(), InfoStats.Plus(SupportEffect.AnimastoneHeatRecovery.Cap), meditating)
            .Add(Effects.AnimastoneRoomFocus, "Lumen_Animastone_FocusGain".Translate(), InfoStats.Plus(SupportEffect.AnimastoneFocusGain.Cap), meditating);
        if (activeRadius <= 0) return rows.Rows;
        string active = "Lumen_Animastone_ActiveRule".Translate(InfoStats.Tiles(activeRadius)) + InfoStats.SolarRule +
            (SolarRules.Enabled ? " " + "Lumen_Animastone_ActiveCost".Translate(activeDrain.ToString("0.##")).ToString() : "");
        rows.Add(InfoStats.Solar("Lumen_Animastone_ActiveRadius".Translate()), InfoStats.Tiles(activeRadius), active);
        if (activeFocusBonus > 0) rows.Add(Effects.AnimastoneClusterFocus, InfoStats.Solar("Lumen_Animastone_FocusStrength".Translate()), InfoStats.Plus(activeFocusBonus), active);
        if (activeHeatRecovery > 0) rows.Add(Effects.AnimastonePylonHeat, InfoStats.Solar("Lumen_Animastone_ExtraHeat".Translate()), InfoStats.Plus(activeHeatRecovery), active);
        if (activeGrowth > 0) rows.Add(Effects.AnimastonePylonGrowth, InfoStats.Solar("Lumen_Animastone_GrassGrowth".Translate()), InfoStats.Plus(activeGrowth), "Lumen_Animastone_GrassGrowthDesc".Translate() + " " + active);
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
        string text = "Lumen_Animastone_Inspect".Translate(reading.Passive.ToStringPercent(), reading.Bonus(SupportEffect.AnimastoneHeatRecovery).ToStringPercent(),
            reading.Bonus(SupportEffect.AnimastoneFocusGain).ToStringPercent());
        if (Props.activeRadius > 0)
            text += "\n" + (poweredNow ? "Lumen_Animastone_ActiveRunning" : "Lumen_Animastone_ActiveIdle").Translate();
        return text;
    }
}


// Raises an Animastone fixture's focus strength while its Solar active effect runs.
public class FocusStrengthOffset_AnimastoneActive : FocusStrengthOffset
{
    public override float GetOffset(Thing parent, Pawn user = null) =>
        Effects.AnimastoneClusterFocus.On && parent.TryGetComp<CompAnimastone>() is { poweredNow: true } comp ? comp.Props.activeFocusBonus : 0;

    public override string GetExplanation(Thing parent) =>
        GetOffset(parent) > 0 ? "Lumen_Animastone_ActiveOffset".Translate(GetOffset(parent).ToStringPercent()).ToString() : null;
}

// Speeds anima grass growth from meditation at a tree within range of a powered Animastone pylon.
public class StatPart_AnimastoneGrowth : StatPart
{
    private static float Bonus(StatRequest req) => req.Thing is { Spawned: true } thing ? RoomSupportCache.ActiveGrowth(thing) : 0;

    public override void TransformValue(StatRequest req, ref float val) => val += Bonus(req);

    public override string ExplanationPart(StatRequest req)
    {
        float bonus = Bonus(req);
        return bonus > 0 ? "Lumen_Animastone_PoweredPylon".Translate(bonus.ToStringPercent()).ToString() : null;
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
        return bonus > 0 ? "Lumen_Animastone_RoomSupport".Translate(bonus.ToStringPercent()).ToString() : null;
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
            s.In(def.LabelCap).PerItem().Add(name + ".focus", "Lumen_Animastone_FocusSetting".Translate(), def.GetStatValueAbstract(focus),
                v => def.SetStatBaseValue(focus, v), Limit.Percent());
        }
        var room = s.In("Lumen_Section_RoomSupport".Translate());
        room.Add("Animastone.heatRecoveryCap", "Lumen_Animastone_HeatCap".Translate(), SupportEffect.AnimastoneHeatRecovery.Cap,
            v => SupportEffect.AnimastoneHeatRecovery.Cap = v, Limit.Percent(10));
        room.Add("Animastone.focusGainCap", "Lumen_Animastone_FocusCap".Translate(), SupportEffect.AnimastoneFocusGain.Cap,
            v => SupportEffect.AnimastoneFocusGain.Cap = v, Limit.Percent(10));
    }
}
