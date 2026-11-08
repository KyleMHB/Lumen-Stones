using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace LumenStones;

// A live handle on one saved setting. Reads on demand, so it works from the mod constructor on and follows edits at once.
// Value is the raw saved number (or the default); callers clamp or round it themselves where they always did.
public sealed class Tunable
{
    public Tunable(string key, float @default, Limit limit)
    {
        Key = key; Default = @default; Limit = limit;
    }

    public string Key { get; }
    public float Default { get; }
    public Limit Limit { get; }
    public float Value => TunableRules.Resolve(Tunables.Overrides, Key, Default);
    public bool On => Value != 0;
}

// Implemented by the mod's own comp properties, which declare their rows in the scope the registry hands them (section set,
// External carried). A def's comps are visited in its comp order.
public interface ITunableProps
{
    void DeclareTunables(TunableScope s, Def owner);
}

// A heat pusher whose cutoff row reads differently from the plain one.
public interface IHeatCutoffLabel
{
    string CutoffLabel { get; }
}

public sealed class TunableRow
{
    public readonly string Key, Label, Section;
    public readonly TunableTab Tab;
    public readonly Limit Limit;
    public readonly bool External;
    internal Action<float> Apply;

    internal TunableRow(TunableTab tab, string section, string key, string label, float @default, Action<float> apply, Limit limit, bool external)
    {
        Tab = tab; Section = section; Key = key; Label = label; Default = @default; Apply = apply; Limit = limit; External = external;
    }

    // What the loaded mod list gives. A recipe-coupled cost row replaces it with the recipe's own count.
    public float Default { get; internal set; }
    // The saved override, or the default when there is none.
    public float Raw => TunableRules.Resolve(Tunables.Overrides, Key, Default);
    public bool HasOverride => Tunables.Overrides.ContainsKey(Key);
    public bool HasApply => Apply != null;
}

public sealed class TunableTab
{
    private readonly List<TunableRow> rows = new();
    private readonly HashSet<string> owned = new();

    public TunableTab(string name, string enabledLabel = null, Func<bool> when = null, Action<Listing_Standard> header = null, Action onReset = null)
    {
        Name = name;
        if (enabledLabel != null) Enabled = new Tunable(name + ".enabled", 1, Limit.Toggle);
        EnabledLabel = enabledLabel;
        When = when ?? (() => true);
        Header = header;
        OnReset = onReset;
    }

    public string Name { get; }
    // The tab's availability switch, if it has one: its row leads the Availability section.
    public Tunable Enabled { get; }
    public string EnabledLabel { get; }
    public Func<bool> When { get; }
    public Action<Listing_Standard> Header { get; }
    // Clears state outside the saved overrides, such as the pre-schema-1 booleans.
    public Action OnReset { get; }

    public IEnumerable<TunableRow> AllRows => rows;
    internal ISet<string> OwnedKeys => owned;

    public IReadOnlyList<string> Sections(bool external) =>
        rows.Where(r => !r.External || external).Select(r => r.Section).Distinct().ToList();

    public IReadOnlyList<TunableRow> Rows(string section, bool external) =>
        rows.Where(r => r.Section == section && (!r.External || external)).ToList();

    internal void Clear() { rows.Clear(); owned.Clear(); }
    internal void Own(string key) => owned.Add(key);
    internal void Add(TunableRow row) { rows.Add(row); owned.Add(row.Key); }
}

public sealed class TunableScope
{
    private readonly string section;

    internal TunableScope(TunableTab tab, string section, bool external = false)
    {
        Tab = tab; this.section = section; External = external;
    }

    public TunableTab Tab { get; }
    // Rows added through this scope are external content's.
    public bool External { get; }

    public TunableScope In(string section) => new(Tab, section, External);
    internal TunableScope In(string section, bool external) => new(Tab, section, external);

    // A balance row. A null apply makes a row that is saved and read elsewhere. Returns null if the key was rejected.
    public TunableRow Add(string key, string label, float value, Action<float> apply, Limit limit = default, bool external = false) =>
        Tunables.AddRow(Tab, section, key, label, value, apply, limit, external || External);

    // Takes ownership of a live handle. A null label owns the key without drawing a row.
    public void Add(Tunable live, string label, bool external = false) => Tunables.AddLive(Tab, section, live, label, external || External);

    // Availability rows and balance rows for these defs, with the tab's toggle gating them.
    public void Things(bool external, params string[] defNames) => StoneSettings.DeclareThings(this, external, defNames);

    // The item follows another tab's or stone's availability but has no row of its own: an implant part follows its stone,
    // the Dreadstone seed crystal follows Dreadstone.
    public void Gate(string itemDefName, string followsTabOrStone) => Tunables.AddGate(itemDefName, followsTabOrStone);

    // Runs once after the saved overrides have been applied, for repairs such as a minimum that ended above its maximum.
    public void AfterApply(Action action) => Tunables.AddAfterApply(action);
}

public static class Tunables
{
    private static readonly Dictionary<string, float> noOverrides = new();
    private static readonly List<TunableRow> rows = new();                  // declaration order is apply order
    private static readonly Dictionary<string, TunableRow> byKey = new();
    private static readonly Dictionary<string, Limit> limits = new();
    private static readonly List<Action> afterApply = new();
    private static readonly HashSet<TunableTab> touched = new();
    private static TunableLedger ledger = new(Array.Empty<string>());

    // Items whose availability the toggles control: the owning tab, whether it is external content, and its own switch.
    internal static readonly Dictionary<string, (TunableTab Tab, bool External, Tunable Enabled)> Items = new();

    internal static Dictionary<string, float> Overrides => LumenstoneMod.Settings?.overrides ?? noOverrides;

    public static IReadOnlyList<TunableTab> All => StoneTabs.All;
    // The tabs the settings window shows.
    public static IReadOnlyList<TunableTab> Tabs =>
        StoneTabs.All.Where(t => t.When() && (t.Header != null || t.AllRows.Any())).ToList();

    public static TunableEdit Edit() => new();
    public static TunableRow Find(string key) => byKey.TryGetValue(key, out var row) ? row : null;
    internal static bool Has(string key) => ledger.Has(key);
    internal static bool TryLimit(string key, out Limit limit) => limits.TryGetValue(key, out limit);

    // Declares every tab's tunables, applies the saved overrides, then publishes availability.
    public static void Initialize()
    {
        rows.Clear(); byKey.Clear(); limits.Clear(); afterApply.Clear(); touched.Clear(); Items.Clear();
        foreach (var tab in StoneTabs.All) tab.Clear();
        ledger = new TunableLedger(StoneTabs.All.Select(t => t.Name));

        StoneSettings.DeclareAll();

        var settings = LumenstoneMod.Settings;
        foreach (var row in rows)
            if (row.Apply != null && (!row.External || settings.overridePyrinth) && settings.overrides.ContainsKey(row.Key))
                row.Apply(row.Limit.Clamp(row.Raw));
        foreach (var action in afterApply) action();
        foreach (var def in Items.Keys.Select(name => DefDatabase<ThingDef>.GetNamed(name)))
        {
            if (def.building != null && def.building.mineableScatterLumpSizeRange.max < def.building.mineableScatterLumpSizeRange.min)
                def.building.mineableScatterLumpSizeRange.max = def.building.mineableScatterLumpSizeRange.min;
        }
        StoneSettings.RefreshAvailability();
    }

    // The first scope taken on a tab also gives it its availability switch.
    internal static TunableScope Scope(TunableTab tab)
    {
        if (touched.Add(tab) && tab.Enabled != null)
            new TunableScope(tab, "Availability").Add(tab.Enabled, tab.EnabledLabel);
        return new TunableScope(tab, null);
    }

    internal static TunableRow AddRow(TunableTab tab, string section, string key, string label, float value, Action<float> apply, Limit limit, bool external)
    {
        if (section == null) { Log.Error("Lumen Stones: tunable " + key + " was added without a section."); section = "General"; }
        if (!ledger.TryClaim(key, tab.Name, out string problem)) { Log.Error("Lumen Stones: " + problem); return null; }
        var row = new TunableRow(tab, section, key, label, value, apply, limit, external);
        rows.Add(row);
        byKey[key] = row;
        limits[key] = limit;
        tab.Add(row);
        return row;
    }

    internal static void AddLive(TunableTab tab, string section, Tunable live, string label, bool external)
    {
        if (!ledger.TryClaim(live.Key, tab.Name, out string problem)) { Log.Error("Lumen Stones: " + problem); return; }
        limits[live.Key] = live.Limit;
        if (label == null) { tab.Own(live.Key); return; }
        if (section == null) { Log.Error("Lumen Stones: tunable " + live.Key + " was added without a section."); section = "General"; }
        var row = new TunableRow(tab, section, live.Key, label, live.Default, null, live.Limit, external);
        rows.Add(row);
        byKey[live.Key] = row;
        tab.Add(row);
    }

    internal static bool AddItem(string defName, TunableTab tab, bool external, Tunable enabled) =>
        Items.TryAdd(defName, (tab, external, enabled));

    internal static void AddGate(string itemDefName, string follows)
    {
        var owner = StoneTabs.All.FirstOrDefault(t => t.Name == follows);
        if (owner == null && Items.TryGetValue(follows, out var stone)) owner = stone.Tab;
        if (owner == null) return;
        Items.TryAdd(itemDefName, (owner, false, new Tunable(itemDefName + ".enabled", 1, Limit.Toggle)));
    }

    internal static void AddAfterApply(Action action) => afterApply.Add(action);
}

// The one lifecycle for changing settings: the window edits through it, and so do the developer tests.
public sealed class TunableEdit
{
    private readonly OverrideSnapshot snapshot = new();
    private readonly List<TunableRow> applied = new();
    private bool? savedAnimation, savedOverridePyrinth;
    private bool changed;

    private static LumenstoneSettings Settings => LumenstoneMod.Settings;

    public bool Has(string key) => Tunables.Overrides.ContainsKey(key);

    // Sets (or with null, clears) a saved value. A known tunable's value is clamped to its limit. With applyNow the row's
    // apply step also runs, as a restart would, and Revert re-applies it.
    public void Set(string key, float? value, bool applyNow = false)
    {
        var overrides = Tunables.Overrides;
        snapshot.Remember(overrides, key);
        changed = true;
        if (!value.HasValue) overrides.Remove(key);
        else overrides[key] = Tunables.TryLimit(key, out var limit) ? limit.Clamp(value.Value) : value.Value;
        if (!applyNow) return;
        var row = Tunables.Find(key);
        if (row?.Apply == null) throw new InvalidOperationException("setting " + key + " has no apply step");
        Apply(row);
        if (!applied.Contains(row)) applied.Add(row);
    }

    private static void Apply(TunableRow row) => row.Apply(row.Limit.Clamp(row.Raw));

    // Removes the tab's owned keys and its "<Name>." keys, and clears its legacy state. Availability updates on Refresh.
    public void Reset(TunableTab tab)
    {
        var overrides = Tunables.Overrides;
        foreach (string key in TunableRules.ResetKeys(overrides.Keys.ToList(), tab.OwnedKeys, tab.Name))
        {
            snapshot.Remember(overrides, key);
            overrides.Remove(key);
        }
        savedOverridePyrinth ??= Settings.overridePyrinth;
        changed = true;
        tab.OnReset?.Invoke();
    }

    public bool OverridePyrinth
    {
        get => Settings.overridePyrinth;
        set
        {
            if (value == Settings.overridePyrinth) return;
            savedOverridePyrinth ??= Settings.overridePyrinth;
            changed = true;
            Settings.overridePyrinth = value;
        }
    }

    public bool Animation
    {
        get => Settings.fixtureAnimation;
        set
        {
            if (value == Settings.fixtureAnimation) return;
            savedAnimation ??= Settings.fixtureAnimation;
            changed = true;
            Settings.fixtureAnimation = value;
        }
    }

    // What closing the settings window does, without the save.
    public void Refresh()
    {
        StoneSettings.RefreshAvailability();
        ActiveEffectInterruption.SettingsChanged();
    }

    // Undoes this edit's overrides and flags, re-applies the rows it applied, and refreshes. Nothing to do if nothing changed.
    public void Revert()
    {
        if (!changed) return;
        snapshot.Revert(Tunables.Overrides);
        foreach (var row in applied) Apply(row);
        applied.Clear();
        if (savedAnimation.HasValue) Settings.fixtureAnimation = savedAnimation.Value;
        if (savedOverridePyrinth.HasValue) Settings.overridePyrinth = savedOverridePyrinth.Value;
        savedAnimation = savedOverridePyrinth = null;
        changed = false;
        Refresh();
    }
}
