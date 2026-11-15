using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LumenStones;

// Info card rows for stone effects. Rows are built from Props and the static rules, which settings overwrite at
// startup, so the card follows the player's settings. They also show for an unbuilt def, so they never read a map.
public class InfoStats
{
    private static StatCategoryDef category;
    public static StatCategoryDef Category => category ??= DefDatabase<StatCategoryDef>.GetNamed("KM_StoneEffects");

    private readonly List<StatDrawEntry> rows = new();
    // Higher priorities draw first, so rows keep the order they were added in. A second comp on the same def starts
    // lower so its rows don't interleave with the first comp's.
    private int priority;

    public InfoStats(int startPriority = 5000) => priority = startPriority;

    public InfoStats Add(string label, string value, string report)
    {
        rows.Add(new StatDrawEntry(Category, label, value, report, priority--));
        return this;
    }

    // A row whose effect has a switch: while it is off, the row says so instead of showing the value.
    public InfoStats Add(EffectSwitch effect, string label, string value, string report) =>
        Add(label, effect == null || effect.On ? value : EffectSwitch.OffText, report);

    public IEnumerable<StatDrawEntry> Rows => rows;

    public static string SupportRule => "Lumen_Info_SupportRule".Translate();

    // With Sunstone off, active effects are free and always on, and nothing about Solar energy is shown.
    public static string Solar(string label) => SolarRules.Enabled ? "Lumen_Info_SolarLabel".Translate(label).ToString() : label;
    public static string SolarRule => SolarRules.Enabled
        ? " " + "Lumen_Info_SolarRule".Translate()
        : "";

    public static string Seconds(int ticks) => "Lumen_Info_Seconds".Translate((ticks / 60f).ToString("0.#"));
    public static string Tiles(float radius) => "Lumen_Info_Tiles".Translate(radius.ToString("0.#"));
    public static string Plus(float fraction) => "+" + fraction.ToStringPercent("0.#");

    // A hediff stage's effects in one line, so tiered effects follow their XML.
    public static string Stage(HediffStage stage)
    {
        var parts = new List<string>();
        if (stage.painFactor != 1f) parts.Add("Lumen_Info_PainFactor".Translate(stage.painFactor.ToStringPercent("0")));
        if (stage.capMods != null)
            foreach (var mod in stage.capMods)
                if (mod.offset != 0) parts.Add(mod.capacity.label + " " + mod.offset.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Offset));
        if (stage.statOffsets != null)
            foreach (var mod in stage.statOffsets)
                parts.Add(mod.stat.label + " " + mod.stat.ValueToString(mod.value, ToStringNumberSense.Offset));
        if (stage.statFactors != null)
            foreach (var mod in stage.statFactors)
                parts.Add(mod.stat.label + " " + mod.stat.ValueToString(mod.value, ToStringNumberSense.Factor));
        return string.Join(", ", parts);
    }
}
