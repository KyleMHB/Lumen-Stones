using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public class LumenstoneSettings : ModSettings
{
    // Retain the old fields for settings files and AutoConfig presets written before schema 1.
    public bool firefly = true, pyrinth = true, glacite = true, vitalite = true, pyrinthHeat = true;
    public bool overridePyrinth;
    // Off hides every fixture's animation switch and stops all fixture animation.
    public bool fixtureAnimation = true;
    public int settingsVersion = 1;
    public Dictionary<string, float> overrides = new();

    public override void ExposeData()
    {
        Scribe_Values.Look(ref firefly, "firefly", true);
        Scribe_Values.Look(ref pyrinth, "pyrinth", true);
        Scribe_Values.Look(ref glacite, "glacite", true);
        Scribe_Values.Look(ref vitalite, "vitalite", true);
        Scribe_Values.Look(ref pyrinthHeat, "pyrinthHeat", true);
        Scribe_Values.Look(ref overridePyrinth, "overridePyrinth");
        Scribe_Values.Look(ref fixtureAnimation, "fixtureAnimation", true);
        Scribe_Values.Look(ref settingsVersion, "settingsVersion");
        Scribe_Collections.Look(ref overrides, "overrides", LookMode.Value, LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            overrides ??= new Dictionary<string, float>();
            if (settingsVersion == 0)
            {
                if (!firefly) overrides.TryAdd("KM_LumenFireflyColumn.enabled", 0);
                if (!pyrinth) overrides.TryAdd("KM_LumenPyrinthColumn.enabled", 0);
                if (!glacite) overrides.TryAdd("KM_LumenGlaciteColumn.enabled", 0);
                if (!vitalite) overrides.TryAdd("KM_LumenVitaliteColumn.enabled", 0);
                if (!pyrinthHeat) overrides.TryAdd("KM_LumenPyrinthColumn.heat", 0);
                settingsVersion = 1;
            }
        }
        base.ExposeData();
    }

    public float Value(string key, float fallback) => TunableRules.Resolve(overrides, key, fallback);
}

public class LumenstoneMod : Mod
{
    public static LumenstoneSettings Settings;
    private string selectedStone = "Lumenstone";
    private string selectedItem;
    private Vector2 scroll;
    private readonly Dictionary<string, string> buffers = new();

    public LumenstoneMod(ModContentPack content) : base(content) => Settings = GetSettings<LumenstoneSettings>();
    public override string SettingsCategory() => "Lumen Stones";

    // Called when the settings window closes, so availability changes apply before play resumes.
    public override void WriteSettings()
    {
        base.WriteSettings();
        Tunables.Edit().Refresh();
    }

    public override void DoSettingsWindowContents(Rect area)
    {
        var edit = Tunables.Edit();
        var list = new Listing_Standard();
        list.Begin(area);
        list.Label("Balance and effects require a restart, including for existing items. Generation affects future maps.");
        list.Label("Availability applies immediately. Existing items, buildings and blueprints remain usable.");
        bool animation = edit.Animation;
        list.CheckboxLabeled("Fixture animation", ref animation,
            "Allow fixtures to show pulses, glints and fireflies. Each fixture's animation starts off and is switched on with its button. Turning this off removes the button and stops every fixture's animation. It changes nothing else a fixture does.");
        edit.Animation = animation;
        var groups = Tunables.Tabs;
        var tabs = list.GetRect(32 * ((groups.Count + 3) / 4));
        for (int i = 0; i < groups.Count; i++)
            if (Widgets.ButtonText(new Rect(tabs.x + i % 4 * tabs.width / 4, tabs.y + i / 4 * 32, tabs.width / 4 - 4, 30), groups[i].Name))
            { selectedStone = groups[i].Name; selectedItem = null; scroll = Vector2.zero; }
        list.Gap();
        var tab = groups.FirstOrDefault(t => t.Name == selectedStone);
        if (tab == null) { list.End(); return; }
        if (list.ButtonText("Reset " + selectedStone + " to active mod list"))
        { edit.Reset(tab); buffers.Clear(); }
        tab.Header?.Invoke(list);
        var sections = tab.Sections(edit.OverridePyrinth);
        if (sections.Count == 0) { list.End(); return; }
        if (!sections.Contains(selectedItem)) selectedItem = sections[0];
        if (list.ButtonText("Section: " + selectedItem))
            Find.WindowStack.Add(new FloatMenu(sections.Select(section => new FloatMenuOption(section,
                () => { selectedItem = section; scroll = Vector2.zero; })).ToList()));
        list.Label("Checked balance overrides use your value; Reset restores the loaded mod list. Enter percentages as percentages.");
        float top = list.CurHeight;
        list.End();
        var entries = tab.Rows(selectedItem, edit.OverridePyrinth);
        var outer = new Rect(area.x, area.y + top, area.width, Mathf.Max(40, area.height - top));
        var inner = new Rect(0, 0, outer.width - 20, entries.Count * 66);
        Widgets.BeginScrollView(outer, ref scroll, inner);
        list.Begin(inner);
        foreach (var entry in entries)
        {
            if (entry.Limit.IsToggle)
            {
                bool enabled = entry.Raw != 0;
                bool before = enabled;
                list.CheckboxLabeled(entry.Label, ref enabled);
                if (enabled != before) edit.Set(entry.Key, enabled ? 1 : 0);
                list.Gap(36);
                continue;
            }
            bool useOverride = edit.Has(entry.Key), wasOverride = useOverride;
            var row = list.GetRect(30);
            Widgets.CheckboxLabeled(new Rect(row.x, row.y, row.width - 215, 30), entry.Label, ref useOverride);
            if (useOverride != wasOverride)
            {
                if (useOverride) edit.Set(entry.Key, entry.Default);
                else { edit.Set(entry.Key, null); buffers.Remove(entry.Key); }
            }
            float scale = entry.Limit.DisplayScale;
            float value = entry.Limit.Clamp(entry.Raw);
            float display = value * scale;
            if (useOverride)
            {
                if (!buffers.TryGetValue(entry.Key, out string buffer)) buffer = display.ToString("0.###");
                Widgets.TextFieldNumeric(new Rect(row.xMax - 205, row.y, 110, 28), ref display, ref buffer,
                    entry.Limit.Min * scale, entry.Limit.Max * scale);
                buffers[entry.Key] = buffer;
                edit.Set(entry.Key, display / scale);
            }
            else Widgets.Label(new Rect(row.xMax - 205, row.y, 110, 28), display.ToString("0.###"));
            if (Widgets.ButtonText(new Rect(row.xMax - 88, row.y, 88, 28), "Reset"))
            { edit.Set(entry.Key, null); buffers.Remove(entry.Key); }
            list.Label("Mod list: " + (entry.Default * scale).ToString("0.###") + entry.Limit.Unit);
            list.Gap(10);
        }
        list.End();
        Widgets.EndScrollView();
    }
}

[StaticConstructorOnStartup]
public static class Startup
{
    static Startup()
    {
        var column = DefDatabase<ThingDef>.GetNamed("Column");
        foreach (var pair in new[] {
            ("KM_LumenFireflyColumn", "Column"), ("KM_LumenPyrinthColumn", "DV_PyrinthLamp"),
            ("KM_LumenGlaciteColumn", "KM_GlaciteLamp"), ("KM_LumenVitaliteColumn", "KM_VitaliteLamp") })
        {
            var target = DefDatabase<ThingDef>.GetNamedSilentFail(pair.Item1);
            var source = DefDatabase<ThingDef>.GetNamedSilentFail(pair.Item2);
            if (target == null || source == null) continue;
            target.researchPrerequisites = new List<ResearchProjectDef>();
            foreach (var researchSource in new[] { column, source })
                if (researchSource.researchPrerequisites != null)
                    foreach (var research in researchSource.researchPrerequisites)
                        if (!target.researchPrerequisites.Contains(research)) target.researchPrerequisites.Add(research);
            if (pair.Item1 == "KM_LumenPyrinthColumn")
                target.costList = source.costList?.ConvertAll(item => new ThingDefCountClass(item.thingDef, item.count));
        }
        new Harmony("kylemhb.lumenstones").PatchAll(Assembly.GetExecutingAssembly());
        // Capture after other mods' startup patches, not from upstream XML or saved fallback values.
        LongEventHandler.ExecuteWhenFinished(Tunables.Initialize);
    }
}
