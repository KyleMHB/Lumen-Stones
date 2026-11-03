using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LumenStones;

// Tuning for the Verdite implant's reserve. Stage thresholds and hunger factors live in the hediff's XML stages.
public class SunlightReserveExtension : DefModExtension
{
    public float hoursToFill = 4f, daysToDrain = 1.5f, startSeverity = 0.5f;
    // Fill rate under a lit Verdite overlight, as a share of full clear sun.
    public float overlightRate = 0.5f;
}

// Severity is stored sunlight. Sun fills it, anything else drains it, and its stage sets hunger.
// The lowest stage is a drawback: only then does it draw Solar energy for relief, like any implant drawback.
public class Hediff_SunlightReserve : HediffWithComps
{
    private const int StarvedStage = 0;
    public bool relieved;
    private float rate;
    private HediffStage stage;
    private int stageKey = -1;
    private HediffComp_Attunement implant;

    private SunlightReserveExtension Tuning => def.GetModExtension<SunlightReserveExtension>();
    public bool Starved => CurStageIndex == StarvedStage;

    // The implant that sustains this reserve. Null once it is removed or dormant.
    private HediffComp_Attunement Implant
    {
        get
        {
            if (implant != null && !implant.dormant && pawn.health.hediffSet.hediffs.Contains(implant.parent)) return implant;
            implant = null;
            foreach (var part in ImplantRules.Parts(pawn))
                if (!part.dormant && part.Props.reserve == def) return implant = part;
            return null;
        }
    }

    public override bool ShouldRemove => Implant == null;

    // Relief softens the starved hunger; pairing scales the sun-fed and fading bonuses.
    public override HediffStage CurStage
    {
        get
        {
            var source = base.CurStage;
            if (source == null) return null;
            float benefit = Implant?.BenefitFactor ?? 1f;
            int key = CurStageIndex * 4 + (relieved ? 2 : 0) + (benefit < 1f ? 1 : 0);
            if (stage != null && key == stageKey) return stage;
            float scale = CurStageIndex == StarvedStage ? (relieved ? 1f - ImplantRules.ReliefFactor : 1f) : benefit;
            stageKey = key;
            stage = new HediffStage
            {
                minSeverity = source.minSeverity, label = source.label, overrideLabel = source.overrideLabel,
                becomeVisible = source.becomeVisible, extraTooltip = source.extraTooltip,
                hungerRateFactor = 1f + (source.hungerRateFactor - 1f) * scale
            };
            return stage;
        }
    }

    public override string LabelInBrackets => base.LabelInBrackets + ", " + Severity.ToStringPercent("0") + (Starved && !relieved ? ", unrelieved" : "");

    public override string TipStringExtra
    {
        get
        {
            var tuning = Tuning;
            string text = rate > 0
                ? "Gaining sunlight: " + (rate * GenDate.TicksPerHour).ToStringPercent("0.#") + " an hour."
                : "Without sunlight: losing " + (GenDate.TicksPerHour / (tuning.daysToDrain * GenDate.TicksPerDay)).ToStringPercent("0.#") + " an hour.";
            if (Starved)
                text += relieved ? "\nSolar energy reduces the hunger by " + ImplantRules.ReliefFactor.ToStringPercent("0") + "."
                    : "\nUnrelieved: no Solar energy reached it, so the hunger is at full strength.";
            return text;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref relieved, "relieved");
    }

    public override void PostTickInterval(int delta)
    {
        base.PostTickInterval(delta);
        if (!pawn.IsHashIntervalTick(ImplantRules.PassInterval, delta) || Implant == null) return;
        var tuning = Tuning;
        float sun = Sunlight(pawn, tuning);
        rate = sun > 0f ? sun / (tuning.hoursToFill * GenDate.TicksPerHour) : 0f;
        float change = sun > 0f ? rate * ImplantRules.PassInterval : -ImplantRules.PassInterval / (tuning.daysToDrain * GenDate.TicksPerDay);
        Severity = Mathf.Clamp(Severity + change, 0f, 1f);
        bool paid = Starved && ImplantEnergy.TryDraw(pawn, StoneSide.Light, ImplantRules.DrainPerDay * ImplantRules.PassInterval / GenDate.TicksPerDay);
        if (paid == relieved) return;
        relieved = paid;
        pawn.health.Notify_HediffChanged(this);
    }

    // Share of full clear sun the pawn is getting now.
    private static float Sunlight(Pawn pawn, SunlightReserveExtension tuning)
    {
        if (pawn.Spawned)
        {
            var map = pawn.Map;
            float sky = map.skyManager.CurSkyGlow;
            if (!pawn.Position.Roofed(map) && sky >= SolarRules.DayGlow) return sky * SolarRules.WeatherFactor(map);
            return VerditeOverlit(pawn) ? tuning.overlightRate : 0f;
        }
        // A world caravan walks under the sky. Its weather isn't tracked, so only daylight counts.
        if (pawn.GetCaravan() is { } caravan)
        {
            float glow = GenCelestial.CelestialSunGlow(caravan.Tile, Find.TickManager.TicksAbs);
            return glow >= SolarRules.DayGlow ? glow : 0f;
        }
        return 0f;
    }

    // Overlit cells read as full glow without the sky. Sun lamps do that too, so a lit Verdite overlight must be in reach.
    private static bool VerditeOverlit(Pawn pawn)
    {
        var map = pawn.Map;
        if (map.glowGrid.GroundGlowAt(pawn.Position, ignoreCavePlants: true, ignoreSky: true) < 1f) return false;
        // Overlights are only on Verdite fixtures, so the registered fixtures are enough.
        foreach (var fixture in map.GetComponent<VerditeField>().fixtures)
        {
            if (fixture.parent.GetComp<CompOverlight>() is not { Glows: true } light) continue;
            float radius = ((CompProperties_Overlight)light.props).overlightRadius;
            if ((fixture.parent.Position - pawn.Position).LengthHorizontalSquared < radius * radius) return true;
        }
        return false;
    }

    public static void Start(Pawn pawn, HediffDef def)
    {
        if (pawn.health.hediffSet.GetFirstHediffOfDef(def) != null) return;
        var hediff = HediffMaker.MakeHediff(def, pawn);
        hediff.Severity = def.GetModExtension<SunlightReserveExtension>().startSeverity;
        pawn.health.AddHediff(hediff);
    }

    public static void Stop(Pawn pawn, HediffDef def)
    {
        if (pawn.health.hediffSet.GetFirstHediffOfDef(def) is { } hediff) pawn.health.RemoveHediff(hediff);
    }
}
