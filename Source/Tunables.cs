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
    // A per-item number (one item's cost, damage, light, weight or stage value). The window shows these only in its
    // advanced view. Stone-level rules, availability and effect switches are never advanced.
    public readonly bool Advanced;
    internal Action<float> Apply;

    internal TunableRow(TunableTab tab, string section, string key, string label, float @default, Action<float> apply, Limit limit, bool external,
        bool advanced = false)
    {
        Tab = tab; Section = section; Key = key; Label = label; Default = @default; Apply = apply; Limit = limit; External = external;
        Advanced = advanced;
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

    public TunableTab(string name, Func<string> enabledLabel = null, Func<bool> when = null, Action<Listing_Standard> header = null, Action onReset = null)
    {
        Name = name;
        if (enabledLabel != null) Enabled = new Tunable(name + ".enabled", 1, Limit.Toggle);
        enabledLabelText = enabledLabel;
        When = when ?? (() => true);
        Header = header;
        OnReset = onReset;
    }

    private readonly Func<string> enabledLabelText;

    public string Name { get; }
    // What the window shows; Name stays the saved-key prefix. Keyed as Lumen_Tab_<Name>.
    public string Label => ("Lumen_Tab_" + Name).Translate();
    // The tab's availability switch, if it has one: its row leads the Availability section.
    public Tunable Enabled { get; }
    public string EnabledLabel => enabledLabelText?.Invoke();
    public Func<bool> When { get; }
    public Action<Listing_Standard> Header { get; }
    // Clears state outside the saved overrides, such as the pre-schema-1 booleans.
    public Action OnReset { get; }

    public IEnumerable<TunableRow> AllRows => rows;
    internal ISet<string> OwnedKeys => owned;

    public IReadOnlyList<string> Sections(bool external, bool advanced = true) =>
        rows.Where(r => Shown(r, external, advanced)).Select(r => r.Section).Distinct().ToList();

    public IReadOnlyList<TunableRow> Rows(string section, bool external, bool advanced = true) =>
        rows.Where(r => r.Section == section && Shown(r, external, advanced)).ToList();

    // Advanced rows that hold a saved value, so the simple view can say what it is hiding.
    public int HiddenOverrides(bool external) => rows.Count(r => r.Advanced && r.HasOverride && (!r.External || external));

    private static bool Shown(TunableRow row, bool external, bool advanced) => (!row.External || external) && (!row.Advanced || advanced);

    internal void Clear() { rows.Clear(); owned.Clear(); }
    internal void Own(string key) => owned.Add(key);
    internal void Add(TunableRow row) { rows.Add(row); owned.Add(row.Key); }
}

public sealed class TunableScope
{
    private readonly string section;

    internal TunableScope(TunableTab tab, string section, bool external = false, bool advanced = false)
    {
        Tab = tab; this.section = section; External = external; Advanced = advanced;
    }

    public TunableTab Tab { get; }
    // Rows added through this scope are external content's.
    public bool External { get; }
    // Rows added through this scope are per-item numbers, shown only in the advanced view.
    public bool Advanced { get; }

    public TunableScope In(string section) => new(Tab, section, External, Advanced);
    internal TunableScope In(string section, bool external) => new(Tab, section, external, Advanced);
    // The same scope for one item's own numbers.
    public TunableScope PerItem() => new(Tab, section, External, true);

    // A balance row. A null apply makes a row that is saved and read elsewhere. Returns null if the key was rejected.
    public TunableRow Add(string key, string label, float value, Action<float> apply, Limit limit = default, bool external = false) =>
        Tunables.AddRow(Tab, section, key, label, value, apply, limit, external || External, Advanced);

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

    // Bumped whenever a saved value may have changed, so effect switches can cache their value between changes.
    public static int Version { get; private set; }
    internal static void Changed() => Version++;

    public static IReadOnlyList<TunableTab> All => StoneTabs.All;
    // The tabs the settings window shows.
    public static IReadOnlyList<TunableTab> Tabs =>
        StoneTabs.All.Where(t => t.When() && (t.Header != null || t.AllRows.Any())).ToList();

    public static TunableEdit Edit() => new();
    internal static string AvailabilitySection => "Lumen_Section_Availability".Translate();
    public static TunableRow Find(string key) => byKey.TryGetValue(key, out var row) ? row : null;
    internal static bool Has(string key) => ledger.Has(key);
    internal static bool TryLimit(string key, out Limit limit) => limits.TryGetValue(key, out limit);

    // Declares every tab's tunables, applies the saved overrides, then publishes availability.
    public static void Initialize()
    {
        rows.Clear(); byKey.Clear(); limits.Clear(); afterApply.Clear(); touched.Clear(); Items.Clear();
        Changed();
        foreach (var tab in StoneTabs.All) tab.Clear();
        ledger = new TunableLedger(StoneTabs.All.Select(t => t.Name));

        StoneSettings.DeclareAll();
        // Tab and effect labels are keyed from their names at run time, so tools/translations.py cannot see them.
        foreach (var tab in StoneTabs.All)
        {
            if (!("Lumen_Tab_" + tab.Name).CanTranslate()) Log.Warning("Lumen Stones: no Keyed label Lumen_Tab_" + tab.Name);
            foreach (var effect in Effects.For(tab))
                if (!("Lumen_Effect_" + tab.Name + "_" + effect.Id).CanTranslate()) Log.Warning("Lumen Stones: no Keyed label Lumen_Effect_" + tab.Name + "_" + effect.Id);
        }

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

    // The first scope taken on a tab also gives it its availability switch, then its effect switches.
    internal static TunableScope Scope(TunableTab tab)
    {
        if (!touched.Add(tab)) return new TunableScope(tab, null);
        if (tab.Enabled != null) new TunableScope(tab, Tunables.AvailabilitySection).Add(tab.Enabled, tab.EnabledLabel);
        var effects = new TunableScope(tab, "Lumen_Section_Effects".Translate());
        foreach (var effect in Effects.For(tab))
        {
            if (!effect.Present) continue;
            effects.Add(effect.Tunable, effect.Restart ? "Lumen_Effect_RestartRequired".Translate(effect.Label).ToString() : effect.Label);
            if (!effect.Restart) continue;
            // After every value override, so a switched-off effect wins over its values.
            AddAfterApply(() =>
            {
                effect.Capture();
                if (!effect.On) effect.Disable?.Invoke();
            });
        }
        return new TunableScope(tab, null);
    }

    internal static TunableRow AddRow(TunableTab tab, string section, string key, string label, float value, Action<float> apply, Limit limit, bool external,
        bool advanced = false)
    {
        if (section == null) { Log.Error("Lumen Stones: tunable " + key + " was added without a section."); section = "Lumen_Section_General".Translate(); }
        if (!ledger.TryClaim(key, tab.Name, out string problem)) { Log.Error("Lumen Stones: " + problem); return null; }
        var row = new TunableRow(tab, section, key, label, value, apply, limit, external, advanced);
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
        if (section == null) { Log.Error("Lumen Stones: tunable " + live.Key + " was added without a section."); section = "Lumen_Section_General".Translate(); }
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
    private bool? savedOverridePyrinth;
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
        Tunables.Changed();
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
        Tunables.Changed();
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
        Tunables.Changed();
        foreach (var row in applied) Apply(row);
        applied.Clear();
        if (savedOverridePyrinth.HasValue) Settings.overridePyrinth = savedOverridePyrinth.Value;
        savedOverridePyrinth = null;
        changed = false;
        Refresh();
    }
}
