using System;
using System.Collections.Generic;
using System.Linq;

namespace LumenStones;

// Pure tunable rules: no Verse or Unity, so xunit links this file directly.

// The bounds and display of one tunable. default(Limit) is Number(): 0 to 100000, shown as is.
public readonly struct Limit
{
    private readonly bool set;
    private readonly float min, max, scale;
    private readonly string unit;
    private readonly bool integer, toggle;

    private Limit(float min, float max, float scale, string unit, bool integer, bool toggle)
    {
        set = true;
        this.min = min; this.max = max; this.scale = scale; this.unit = unit; this.integer = integer; this.toggle = toggle;
    }

    public static Limit Number(float min = 0, float max = 100000) => new(min, max, 1, "", false, false);
    public static Limit Integer(float min = 0, float max = 100000) => new(min, max, 1, "", true, false);
    // Stored as a fraction, shown and typed as a percentage.
    public static Limit Percent(float max = 1, float min = 0) => new(min, max, 100, "%", false, false);
    public static readonly Limit Toggle = new(0, 1, 1, "", false, true);

    public float Min => set ? min : 0;
    public float Max => set ? max : 100000;
    public float DisplayScale => set ? scale : 1;
    public string Unit => unit ?? "";
    public bool IsInteger => integer;
    public bool IsToggle => toggle;

    // Same as Unity's Mathf.Clamp, then Mathf.Round (which rounds halves to even, like Math.Round) for integers.
    public float Clamp(float v)
    {
        float lo = Min, hi = Max;
        if (v < lo) v = lo;
        else if (v > hi) v = hi;
        return integer ? (float)Math.Round(v) : v;
    }
}

public static class TunableRules
{
    // The saved override when it is a usable number, otherwise the default.
    public static float Resolve(IReadOnlyDictionary<string, float> overrides, string key, float @default) =>
        overrides != null && overrides.TryGetValue(key, out float value) && !float.IsNaN(value) && !float.IsInfinity(value) ? value : @default;

    // The saved keys a tab's reset removes: the keys it owns, plus anything under "<TabName>.". Never a substring match,
    // which would also wipe other tabs' keys such as KM_AegiteShieldProjector.* (owned by Sunstone) when resetting Aegite.
    public static List<string> ResetKeys(IEnumerable<string> savedKeys, ISet<string> owned, string tabName)
    {
        string prefix = tabName + ".";
        return savedKeys.Where(key => owned.Contains(key) || key.StartsWith(prefix, StringComparison.Ordinal)).ToList();
    }
}

// Claims each tunable key for one tab. A key may be claimed once, and may not sit under another tab's "<Name>." prefix,
// because that tab's reset would remove it. Problems come back as messages; the first claim wins and nothing throws.
public sealed class TunableLedger
{
    private readonly List<string> tabNames;
    private readonly Dictionary<string, string> owners = new();

    public TunableLedger(IEnumerable<string> tabNames) => this.tabNames = tabNames.ToList();

    public bool Has(string key) => owners.ContainsKey(key);

    public bool TryClaim(string key, string owner, out string problem)
    {
        problem = null;
        if (owners.TryGetValue(key, out string first))
        {
            problem = "Tunable key " + key + " for " + owner + " is already claimed by " + first;
            return false;
        }
        foreach (string name in tabNames)
            if (name != owner && key.StartsWith(name + ".", StringComparison.Ordinal))
            {
                problem = "Tunable key " + key + " for " + owner + " sits under the " + name + " tab's reset prefix";
                return false;
            }
        owners[key] = owner;
        return true;
    }
}

// First-seen values of the keys an edit changes (null = the key was absent), so the edit can be undone.
public sealed class OverrideSnapshot
{
    private readonly Dictionary<string, float?> saved = new();

    public bool IsEmpty => saved.Count == 0;

    // Call before the first change to the key; later calls keep the first value.
    public void Remember(IDictionary<string, float> overrides, string key)
    {
        if (!saved.ContainsKey(key)) saved[key] = overrides.TryGetValue(key, out float old) ? old : null;
    }

    // Puts every remembered key back and forgets them. Returns the keys it restored.
    public List<string> Revert(IDictionary<string, float> overrides)
    {
        var keys = saved.Keys.ToList();
        foreach (var pair in saved)
            if (pair.Value.HasValue) overrides[pair.Key] = pair.Value.Value;
            else overrides.Remove(pair.Key);
        saved.Clear();
        return keys;
    }
}
