using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Unity.Collections;
using UnityEngine;
using Verse;
using Verse.Glow;

namespace LumenStones;

public class CompProperties_LumenDarklight : CompProperties
{
    public ColorInt? darklightColor;
    public bool external;
    public CompProperties_LumenDarklight() => compClass = typeof(CompLumenDarklight);
}

public class CompLumenDarklight : ThingComp
{
    private static readonly Action<CompGlower, ColorInt?> SetColor = AccessTools.MethodDelegate<Action<CompGlower, ColorInt?>>(
        AccessTools.Method(typeof(CompGlower), "SetGlowColorInternal"));
    private bool darklight, initialized;
    private CompProperties_LumenDarklight Props => (CompProperties_LumenDarklight)props;
    private bool Enabled => !Props.external || LumenstoneMod.Settings.overridePyrinth;
    public bool Active => darklight && Enabled;
    public static bool On(Thing thing) => (thing as ThingWithComps)?.GetComp<CompLumenDarklight>()?.Active == true;
    public static int DefaultMode => Mathf.Clamp(Mathf.RoundToInt(StoneTabs.DarklightMode.Value), 0, 2);

    public override void PostExposeData()
    {
        Scribe_Values.Look(ref darklight, "darklight");
        Scribe_Values.Look(ref initialized, "darklightInitialized");
        // Old minified buildings also keep Normal when first reinstalled after upgrading.
        if (Scribe.mode == LoadSaveMode.PostLoadInit && !initialized) initialized = true;
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        if (!initialized)
        {
            var ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
            bool player = parent.Faction == Faction.OfPlayer || parent.def.defName == "KM_LumenFireflyJar" && parent.Map.IsPlayerHome;
            darklight = !respawningAfterLoad && player && (DefaultMode == 2 || DefaultMode == 1 && ModsConfig.IdeologyActive &&
                ideo != null && IdeoUtility.IdeoPrefersDarkness(ideo));
            initialized = true;
        }
        parent.Map.GetComponent<DarklightField>().fixtures[parent.thingIDNumber] = this;
        SetDarklight(darklight);
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) =>
        map.GetComponent<DarklightField>().fixtures.Remove(parent.thingIDNumber);

    public override void PostSwapMap() => PostSpawnSetup(true);

    public override void PostSplitOff(Thing piece)
    {
        if (piece.TryGetComp<CompLumenDarklight>() is not { } split) return;
        split.initialized = true;
        split.SetDarklight(darklight);
    }

    // Absorbing stacks keep their own comp state; no merge veto or source-state copy is needed.
    public void SetDarklight(bool value)
    {
        darklight = value && Enabled;
        initialized = true;
        // Turn off the separate plant light before updating the ordinary glower.
        foreach (var overlight in parent.GetComps<CompOverlight>())
            if (parent.Spawned) overlight.UpdateLit(parent.Map);
        foreach (var glower in parent.GetComps<CompGlower>())
        {
            if (glower is CompOverlight) continue;
            if (Active) glower.GlowColor = Props.darklightColor ?? Fallback(glower.Props.glowColor);
            else SetColor(glower, null);
            if (parent.Spawned) glower.UpdateLit(parent.Map);
        }
        if (parent.Spawned) parent.Map.GetComponent<DarklightField>().Invalidate();
    }

    private static ColorInt Fallback(ColorInt normal)
    {
        Color.RGBToHSV(normal.ToColor, out float h, out float s, out float v);
        var result = new ColorInt(Color.HSVToRGB(h, Mathf.Clamp01(s * 1.45f), Mathf.Max(0.65f, v * 0.78f)));
        result.a = 0;
        return result;
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (!Enabled || parent.Faction != Faction.OfPlayer && parent.def.defName != "KM_LumenFireflyJar") yield break;
        bool target = !Active;
        yield return new DarklightCommand
        {
            fixture = this,
            target = target,
            groupKey = 148372901,
            groupKeyIgnoreContent = 148372901,
            alsoClickIfOtherInGroupClicked = false,
            defaultLabel = (Active ? "ToggleDarklightOff" : "ToggleDarklightOn").Translate(),
            defaultDesc = (Active ? "ToggleDarklightOffDesc" : "ToggleDarklightOnDesc").Translate(),
            icon = ContentFinder<Texture2D>.Get(Active ? "UI/Commands/SetNormalLight" : "UI/Commands/SetDarklight"),
            action = () => SetDarklight(target)
        };
    }
}

// Use the clicked command's target for the whole native group, including mixed on/off selections.
public class DarklightCommand : Command_Action
{
    public CompLumenDarklight fixture;
    public bool target;
    public override void ProcessGroupInput(Event ev, List<Gizmo> group)
    {
        foreach (var gizmo in group)
            if (gizmo is DarklightCommand command && command != this) command.fixture.SetDarklight(target);
    }
}

[StaticConstructorOnStartup]
public static class DarklightSetup
{
    static DarklightSetup()
    {
        foreach (var palette in new (string family, ColorInt color)[] {
            ("Firefly", new(55,185,110,0)), ("Glacite", new(40,95,205,0)), ("Vitalite", new(190,70,115,0)),
            ("Serenite", new(135,65,215,0)), ("Verdite", new(60,185,90,0)), ("Aegite", new(195,130,50,0)),
            ("Ferrite", new(165,95,65,0)), ("Pyrinth", new(175,55,40,0)), ("Bloodstone", new(145,35,95,0)),
            ("Sanguinite", new(170,35,50,0)), ("Dreadstone", new(150,55,30,0)), ("Hollowstone", new(50,155,185,0)) })
        {
            var names = new List<string> { "KM_Lumen" + palette.family + "Column", "KM_" + palette.family + "Lamp",
                "KM_" + palette.family + "WallLamp", "KM_" + palette.family + "Cluster", "KM_" + palette.family + "Pylon" };
            if (palette.family == "Firefly") names.Add("KM_LumenFireflyJar");
            if (palette.family == "Glacite") names.Add("KM_GlaciteCooler");
            if (palette.family == "Bloodstone") names.AddRange(new[] { "KM_BloodstoneVessel", "KM_BloodstoneObelisk" });
            if (palette.family == "Pyrinth" && StoneSettings.PyrinthPresent)
                names.AddRange(new[] { "DV_PyrinthLamp", "DV_PyrinthWallLamp", "DV_PyrinthBrazier", "DV_PyrinthHeater" });
            foreach (string name in names)
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def?.GetCompProperties<CompProperties_Glower>() == null || def.GetCompProperties<CompProperties_LumenDarklight>() != null) continue;
                def.comps.Add(new CompProperties_LumenDarklight { darklightColor = palette.color, external = name.StartsWith("DV_") });
            }
        }
    }
}

public class DarklightField : MapComponent
{
    public readonly Dictionary<int, CompLumenDarklight> fixtures = new();
    // GlowGrid's vanilla reverse lookup uses only the thing ID, which is shared by Verdite's two glowers.
    public readonly Dictionary<CompGlower, int> glowSlots = new();
    private readonly Dictionary<int, bool> cells = new();
    private int tick = -1;
    private static readonly AccessTools.FieldRef<GlowGrid, NativeList<GlowLight>> Lights =
        AccessTools.FieldRefAccess<GlowGrid, NativeList<GlowLight>>("lights");
    private static readonly FieldInfo GlowPool = AccessTools.Field(typeof(GlowGrid), "glowPool");
    private static readonly FieldInfo Pool = AccessTools.Field(GlowPool.FieldType, "pool");

    public DarklightField(Map map) : base(map) { }
    public void Invalidate() => cells.Clear();
    public static NativeList<GlowLight> GetLights(GlowGrid grid) => Lights(grid);

    public bool IsDarklight(IntVec3 position)
    {
        if (!position.InBounds(map) || !position.Roofed(map) || map.glowGrid.PsychGlowAt(position) < PsychGlow.Lit) return false;
        int now = Find.TickManager.TicksGame;
        if (tick != now) { tick = now; cells.Clear(); }
        int cell = map.cellIndices.CellToIndex(position);
        if (cells.TryGetValue(cell, out bool cached)) return cached;
        var lights = Lights(map.glowGrid);
        var pool = (NativeList<LocalGlowArea>)Pool.GetValue(GlowPool.GetValue(map.glowGrid));
        bool dark = false;
        for (int i = 0; i < lights.Length; i++)
        {
            var light = lights[i];
            if (light.isCavePlant || !light.AffectedRect.Contains(position)) continue;
            var color = pool[light.localGlowPoolIndex].colors[light.WorldToLocalIndex(in position)];
            if (color.r == 0 && color.g == 0 && color.b == 0) continue;
            bool sourceDark = !light.isTerrain && !light.isCustom && fixtures.TryGetValue(light.id, out var fixture)
                ? fixture.Active : DarklightUtility.IsDarklight(light.glowColor.ToColor);
            if (!sourceDark) return cells[cell] = false;
            dark = true;
        }
        return cells[cell] = dark;
    }
}

[HarmonyPatch(typeof(DarklightUtility), nameof(DarklightUtility.IsDarklightAt))]
public static class ResolveLumenDarklight
{
    public static bool Prefix(IntVec3 position, Map map, ref bool __result)
    {
        __result = map.GetComponent<DarklightField>().IsDarklight(position);
        return false;
    }
}

// A toggle, moving lamp or changed wall may rebuild the light grid while the game is paused on the same tick.
[HarmonyPatch(typeof(GlowGrid), nameof(GlowGrid.GlowGridUpdate_First))]
public static class InvalidateDarklightCells
{
    public static void Prefix(bool ___anyDirtyCell, bool ___anyDirtyLight, out bool __state) =>
        __state = ___anyDirtyCell || ___anyDirtyLight;
    public static void Postfix(Map ___map, bool __state)
    {
        if (__state) ___map.GetComponent<DarklightField>().Invalidate();
    }
}

[HarmonyPatch(typeof(GlowLight), MethodType.Constructor, typeof(CompGlower), typeof(int))]
public static class RememberGlowerSlot
{
    public static void Postfix(CompGlower glower, int glowPoolIndex)
    {
        if (glower.parent.GetComp<CompOverlight>() != null)
            glower.parent.Map.GetComponent<DarklightField>().glowSlots[glower] = glowPoolIndex;
    }
}

[HarmonyPatch(typeof(GlowGrid), "GetGlowerIndex")]
public static class ResolveGlowerSlot
{
    public static bool Prefix(GlowGrid __instance, CompGlower glower, Map ___map, ref int __result)
    {
        var slots = ___map.GetComponent<DarklightField>().glowSlots;
        if (!slots.TryGetValue(glower, out int slot)) return true;
        var lights = DarklightField.GetLights(__instance);
        for (int i = 0; i < lights.Length; i++)
            if (lights[i].localGlowPoolIndex == slot) { __result = i; slots.Remove(glower); return false; }
        return true;
    }
}
