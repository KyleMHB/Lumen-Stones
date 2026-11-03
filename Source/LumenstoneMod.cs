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

    public float Value(string key, float fallback) => overrides.TryGetValue(key, out float value) &&
        !float.IsNaN(value) && !float.IsInfinity(value) ? value : fallback;

    public void Reset(string group)
    {
        foreach (var key in overrides.Keys.Where(key => key.IndexOf(group, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList())
            overrides.Remove(key);
        // Item keys such as KM_BloodstoneVessel.capacity do not contain the group name.
        foreach (var key in StoneSettings.Entries.Where(e => e.Group == group).Select(e => e.Key).ToList())
            overrides.Remove(key);
        if (group == "Firefly") firefly = true;
        if (group == "Glacite") glacite = true;
        if (group == "Vitalite") vitalite = true;
        if (group == "Pyrinth") { pyrinth = pyrinthHeat = true; overridePyrinth = false; }
    }
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
        StoneSettings.RefreshAvailability();
    }

    public override void DoSettingsWindowContents(Rect area)
    {
        var list = new Listing_Standard();
        list.Begin(area);
        list.Label("Balance and effects require a restart, including for existing items. Generation affects future maps.");
        list.Label("Availability applies immediately. Existing items, buildings and blueprints remain usable.");
        list.CheckboxLabeled("Fixture animation", ref Settings.fixtureAnimation,
            "Allow fixtures to show pulses, glints and fireflies. Each fixture's animation starts off and is switched on with its button. Turning this off removes the button and stops every fixture's animation. It changes nothing else a fixture does.");
        var groups = new List<string> { "Lumenstone", "Firefly", "Glacite", "Vitalite", "Serenite", "Verdite", "Aegite", "Ferrite", "Sunstone", "Pyrinth", "Darklight" };
        if (ModsConfig.RoyaltyActive) groups.Add("Animastone");
        if (ModsConfig.IdeologyActive) groups.Add("DarkStone");
        if (StoneSettings.Entries.Any(e => e.Group == "Implants")) groups.Add("Implants");
        var tabs = list.GetRect(32 * ((groups.Count + 3) / 4));
        for (int i = 0; i < groups.Count; i++)
            if (Widgets.ButtonText(new Rect(tabs.x + i % 4 * tabs.width / 4, tabs.y + i / 4 * 32, tabs.width / 4 - 4, 30), groups[i]))
            { selectedStone = groups[i]; selectedItem = null; scroll = Vector2.zero; }
        list.Gap();
        if (list.ButtonText("Reset " + selectedStone + " to active mod list"))
        { Settings.Reset(selectedStone); buffers.Clear(); }
        if (selectedStone == "Darklight")
        {
            string[] modes = { "Off (Normal light)", "Auto (by Ideoligion)", "Always Darklight" };
            list.Label("Default light mode for newly placed player fixtures and jars of fireflies. Existing fixtures keep their chosen mode.");
            if (list.ButtonText(modes[CompLumenDarklight.DefaultMode]))
                Find.WindowStack.Add(new FloatMenu(Enumerable.Range(0, modes.Length).Select(mode =>
                    new FloatMenuOption(modes[mode], () => Settings.overrides["Darklight.defaultMode"] = mode)).ToList()));
            list.End();
            return;
        }
        if (selectedStone == "Pyrinth")
        {
            list.CheckboxLabeled("Override Epochs - Pyrinth balance (restart required)", ref Settings.overridePyrinth);
            list.Label(StoneSettings.PyrinthPresent ? "Base Pyrinth content only. Add-ons keep their own settings." :
                "Epochs - Pyrinth is not loaded. Its column and external controls appear when its definitions are present.");
        }
        var sections = StoneSettings.Entries.Where(e => e.Group == selectedStone && (!e.External || Settings.overridePyrinth))
            .Select(e => e.Section).Distinct().ToList();
        if (sections.Count == 0) { list.End(); return; }
        if (!sections.Contains(selectedItem)) selectedItem = sections[0];
        if (list.ButtonText("Section: " + selectedItem))
            Find.WindowStack.Add(new FloatMenu(sections.Select(section => new FloatMenuOption(section,
                () => { selectedItem = section; scroll = Vector2.zero; })).ToList()));
        list.Label("Checked balance overrides use your value; Reset restores the loaded mod list. Enter percentages as percentages.");
        float top = list.CurHeight;
        list.End();
        var entries = StoneSettings.Entries.Where(e => e.Group == selectedStone && e.Section == selectedItem &&
            (!e.External || Settings.overridePyrinth)).ToList();
        var outer = new Rect(area.x, area.y + top, area.width, Mathf.Max(40, area.height - top));
        var inner = new Rect(0, 0, outer.width - 20, entries.Count * 66);
        Widgets.BeginScrollView(outer, ref scroll, inner);
        list.Begin(inner);
        foreach (var entry in entries)
        {
            if (entry.Toggle)
            {
                bool enabled = Settings.Value(entry.Key, entry.Default) != 0;
                bool before = enabled;
                list.CheckboxLabeled(entry.Label, ref enabled);
                if (enabled != before) Settings.overrides[entry.Key] = enabled ? 1 : 0;
                list.Gap(36);
                continue;
            }
            bool useOverride = Settings.overrides.ContainsKey(entry.Key), wasOverride = useOverride;
            var row = list.GetRect(30);
            Widgets.CheckboxLabeled(new Rect(row.x, row.y, row.width - 215, 30), entry.Label, ref useOverride);
            if (useOverride != wasOverride)
            {
                if (useOverride) Settings.overrides[entry.Key] = entry.Default;
                else { Settings.overrides.Remove(entry.Key); buffers.Remove(entry.Key); }
            }
            float value = entry.Clamp(Settings.Value(entry.Key, entry.Default));
            float display = value * entry.DisplayScale;
            if (useOverride)
            {
                if (!buffers.TryGetValue(entry.Key, out string buffer)) buffer = display.ToString("0.###");
                Widgets.TextFieldNumeric(new Rect(row.xMax - 205, row.y, 110, 28), ref display, ref buffer,
                    entry.Min * entry.DisplayScale, entry.Max * entry.DisplayScale);
                buffers[entry.Key] = buffer;
                Settings.overrides[entry.Key] = entry.Clamp(display / entry.DisplayScale);
            }
            else Widgets.Label(new Rect(row.xMax - 205, row.y, 110, 28), display.ToString("0.###"));
            if (Widgets.ButtonText(new Rect(row.xMax - 88, row.y, 88, 28), "Reset"))
            { Settings.overrides.Remove(entry.Key); buffers.Remove(entry.Key); }
            list.Label("Mod list: " + (entry.Default * entry.DisplayScale).ToString("0.###") + entry.Unit);
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
        LongEventHandler.ExecuteWhenFinished(StoneSettings.Initialize);
    }
}
