using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

// Floor lamps take any stone for the plinth, but their sprites are drawn with the plinth and crystal
// in their own colours and have no material mask, so the stuff colour must not tint them.
public class Building_PlinthLamp : Building
{
    public override Color DrawColor => def.graphicData?.color ?? Color.white;

    public override void ExposeData()
    {
        // Lamps built while the plinth was a fixed granite cost load without stuff. Thing.ExposeData
        // logs an error and assigns a default in PostLoadInit, so supply the granite they were paid with first.
        if (Scribe.mode == LoadSaveMode.PostLoadInit && Stuff == null) SetStuffDirect(DefDatabase<ThingDef>.GetNamedSilentFail("BlocksGranite") ?? GenStuff.DefaultStuffFor(def));
        base.ExposeData();
    }
}

// 1.7.5005: Designator_Build.IconDrawColor returns the selected stuff's colour, which DrawIcon passes to Widgets.DefIcon.
[HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.IconDrawColor), MethodType.Getter)]
public static class PlinthLampIconColor
{
    public static void Postfix(Designator_Build __instance, ref Color __result)
    {
        if (__instance.PlacingDef is ThingDef { thingClass: var cls } def && typeof(Building_PlinthLamp).IsAssignableFrom(cls)) __result = def.uiIconColor;
    }
}
