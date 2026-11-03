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

    public IEnumerable<StatDrawEntry> Rows => rows;

    public const string SupportRule = "Each fixture category counts once in an enclosed room, at its highest weight. Floor and wall lamps share the lamp category, and duplicates add nothing. Support is capped at 100%.";

    // With Sunstone off, active effects are free and always on, and nothing about Solar energy is shown.
    public static string Solar(string label) => SolarRules.Enabled ? label + " (Solar)" : label;
    public static string SolarRule => SolarRules.Enabled
        ? " This is a Solar active effect: it runs only while the fixture's Solar active effect is toggled on and the map has Solar energy."
        : "";

    public static string Seconds(int ticks) => (ticks / 60f).ToString("0.#") + " s";
    public static string Tiles(float radius) => radius.ToString("0.#") + " tiles";
    public static string Plus(float fraction) => "+" + fraction.ToStringPercent("0.#");

    // A hediff stage's effects in one line, so tiered effects follow their XML.
    public static string Stage(HediffStage stage)
    {
        var parts = new List<string>();
        if (stage.painFactor != 1f) parts.Add("pain x" + stage.painFactor.ToStringPercent("0"));
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
