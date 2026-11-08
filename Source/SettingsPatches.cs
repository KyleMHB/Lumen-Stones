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
