using System;
using System.Collections.Generic;
using Verse;

namespace LumenStones;

// Map.GetComponent<T> scans every component on the map, and stat parts, patches and passes ask many times a tick.
// This keeps the last map's component of each type, so a repeat lookup is one comparison.
public static class MapState
{
    private static readonly List<Action> clears = new();

    private static class Slot<T> where T : MapComponent
    {
        public static Map map;
        public static T value;
        static Slot() => clears.Add(() => { map = null; value = null; });
    }

    public static T Of<T>(Map map) where T : MapComponent
    {
        if (map == null) return null;
        if (Slot<T>.map == map) return Slot<T>.value;
        var found = map.GetComponent<T>();
        // A miss is not kept: the map may still be filling its components.
        if (found != null) { Slot<T>.map = map; Slot<T>.value = found; }
        return found;
    }

    // Called when a map is removed, so no slot keeps it alive.
    internal static void Forget()
    {
        foreach (var clear in clears) clear();
    }
}
