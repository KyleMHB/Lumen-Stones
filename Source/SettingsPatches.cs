using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

[HarmonyPatch(typeof(Designator_Build), "Visible", MethodType.Getter)]
public static class ConstructionVisibility
{
    public static void Postfix(Designator_Build __instance, ref bool __result)
    {
        if (__result && __instance.PlacingDef is ThingDef def) __result = StoneSettings.Available(def);
    }
}

// 1.7.5000: a dropdown entry selects its designator directly and never runs Designator_Build.ProcessInput,
// which is where the material menu opens. Our stuff-made fixtures route through it so stuff stays choosable.
[HarmonyPatch(typeof(Designator_Dropdown), "GetDesignatorSelectAction")]
public static class FixtureDropdownStuffMenu
{
    public static void Postfix(Designator_Dropdown __instance, Event ev, Designator des, ref Action __result)
    {
        if (des is not Designator_Build build || !build.PlacingDef.MadeFromStuff) return;
        if (build.PlacingDef.designatorDropdown?.defName.StartsWith("KM_Lumen") != true) return;
        __result = () =>
        {
            __instance.SetActiveDesignator(des, true);
            des.ProcessInput(ev);
        };
    }
}

// A Lumen dropdown button names its group ("lumenstone pylons"), not whichever variant is active,
// so players can tell other stone families are inside it.
[HarmonyPatch(typeof(Designator_Dropdown), "Label", MethodType.Getter)]
public static class FixtureDropdownLabel
{
    public static void Postfix(Designator_Dropdown __instance, ref string __result)
    {
        foreach (var element in __instance.Elements)
        {
            var group = (element as Designator_Place)?.PlacingDef?.designatorDropdown;
            if (group == null) continue;
            if (group.defName.StartsWith("KM_Lumen")) __result = group.label;
            return;
        }
    }
}

// Names the variant whose icon a Lumen dropdown button always shows.
public class DropdownFace : DefModExtension
{
    public ThingDef face;

    // The visible face designator of a Lumen dropdown, or null to keep vanilla's active-variant icon.
    public static Designator For(Designator_Dropdown dropdown)
    {
        var elements = dropdown.Elements;
        if (elements.Count == 0) return null;
        var face = ((elements[0] as Designator_Place)?.PlacingDef?.designatorDropdown)?.GetModExtension<DropdownFace>()?.face;
        if (face == null) return null;
        foreach (var element in elements)
            if (element is Designator_Place place && place.PlacingDef == face) return element.Visible ? element : null;
        return null;
    }
}

// 1.7.5000: Designator_Dropdown.DrawIcon and IconDrawColor delegate to the active designator.
[HarmonyPatch(typeof(Designator_Dropdown), nameof(Designator_Dropdown.DrawIcon))]
public static class FixtureDropdownIcon
{
    public static bool Prefix(Designator_Dropdown __instance, Rect rect, Material buttonMat, GizmoRenderParms parms)
    {
        var face = DropdownFace.For(__instance);
        if (face == null) return true;
        face.DrawIcon(rect, buttonMat, parms);
        return false;
    }
}

[HarmonyPatch(typeof(Designator_Dropdown), "IconDrawColor", MethodType.Getter)]
public static class FixtureDropdownIconColor
{
    public static void Postfix(Designator_Dropdown __instance, ref Color __result)
    {
        var face = DropdownFace.For(__instance);
        if (face != null) __result = face.IconDrawColor;
    }
}

[HarmonyPatch(typeof(RecipeDef), "AvailableNow", MethodType.Getter)]
public static class RecipeAvailability
{
    public static void Postfix(RecipeDef __instance, ref bool __result)
    {
        if (__result) __result = StoneSettings.RecipeAvailable(__instance);
    }
}

[HarmonyPatch(typeof(Bill_Production), "ShouldDoNow")]
public static class ExistingBillAvailability
{
    public static void Postfix(Bill_Production __instance, ref bool __result)
    {
        if (__result) __result = StoneSettings.RecipeAvailable(__instance.recipe);
    }
}

[HarmonyPatch(typeof(ThingSetMaker_TraderStock), "Generate")]
public static class TraderAvailability
{
    public static void Postfix(List<Thing> outThings) => outThings.RemoveAll(t => !StoneSettings.Available(t.GetInnerIfMinified().def));
}

[HarmonyPatch(typeof(GenStep_ScatterThings), "Generate")]
public static class SurfaceAvailability
{
    public static bool Prefix(GenStep_ScatterThings __instance) => StoneSettings.Available(__instance.thingDef);
}

[HarmonyPatch(typeof(GenStep_ScatterLumpsMineable), "ScatterAt")]
public static class ForcedDepositAvailability
{
    public static bool Prefix(GenStep_ScatterLumpsMineable __instance) => StoneSettings.DepositAvailable(__instance.forcedDefToScatter);
}

// Keep generation weights separate from the defs: existing deposits and drilling stay usable.
[HarmonyPatch]
public static class ResourceGenerationAvailability
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var type in new[] { typeof(GenStep_ScatterLumpsMineable), typeof(CompDeepScanner), typeof(TileMutatorWorker_MineralRich) })
        foreach (var owner in new[] { type }.Concat(type.GetNestedTypes(AccessTools.all)))
        foreach (var method in AccessTools.GetDeclaredMethods(owner))
            if (method.Name.Contains("ChooseThingDef") || method.Name.Contains("ChooseLumpThingDef") || method.Name.Contains("GeneratePostTerrain"))
                yield return method;
    }

    // Map generation reads a weight for every mineable on every lump, so the deposit of each building is looked up once.
    private static Dictionary<BuildingProperties, ThingDef> deposits;

    public static float SurfaceWeight(BuildingProperties building)
    {
        var resource = building.mineableThing;
        if (!StoneSettings.Available(resource)) return 0;
        if (deposits == null)
        {
            var table = new Dictionary<BuildingProperties, ThingDef>();
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
                if (def.building != null) table.TryAdd(def.building, def);
            deposits = table;
        }
        return StoneSettings.Available(deposits.GetValueOrDefault(building)) ? building.mineableScatterCommonality : 0;
    }

    public static float DeepWeight(ThingDef def) => StoneSettings.Available(def) ? def.deepCommonality : 0;

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var surface = AccessTools.Field(typeof(BuildingProperties), "mineableScatterCommonality");
        var deep = AccessTools.Field(typeof(ThingDef), "deepCommonality");
        foreach (var instruction in instructions)
        {
            if (instruction.LoadsField(surface))
            { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(ResourceGenerationAvailability), nameof(SurfaceWeight)); }
            else if (instruction.LoadsField(deep))
            { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(ResourceGenerationAvailability), nameof(DeepWeight)); }
            yield return instruction;
        }
    }
}

[HarmonyPatch(typeof(HediffComp_Infecter), "CheckMakeInfection")]
public static class VitaliteInfectionPrevention
{
    public static float AdjustChance(float chance, HediffComp_Infecter infecter) =>
        infecter.fromScaria ? chance : Mathf.Clamp01(chance) * (1f - FixtureSupport.Bonus(infecter.Pawn, SupportEffect.VitaliteInfection).Value);

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        // 1.7.4990: the delayed check compares Rand.Value with the final risk after
        // tending, severity and difficulty. Clamp before multiplying to preserve a relative reduction.
        var code = instructions.ToList();
        var random = AccessTools.PropertyGetter(typeof(Rand), "Value");
        int index = code.FindIndex(i => i.Calls(random));
        if (index < 0 || index + 2 >= code.Count || code[index + 1].opcode != OpCodes.Ldloc_0 || code[index + 2].opcode != OpCodes.Bge_Un_S)
        {
            // A game update here must cost only this one bonus: PatchAll and settings startup still need to finish.
            Log.Error("[Lumen Stones] Wound infection calculation changed; Vitalite infection prevention is inactive until this patch is updated.");
            return code;
        }
        code.InsertRange(index + 2, new[] { new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(VitaliteInfectionPrevention), nameof(AdjustChance))) });
        return code;
    }
}
