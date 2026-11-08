using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LumenStones;

[Flags]
public enum ActiveEffectNetworks
{
    None = 0,
    Solar = 1,
    Warp = 2
}

// The one place that decides whether a map event pauses active effects. Rules are registered once, each for the
// networks it interrupts. Passive effects, implants, prosthetics and worn gear never ask.
public static class ActiveEffectInterruption
{
    private static GameConditionDef solarFlare;
    private static GameConditionDef SolarFlareDef => solarFlare ??= DefDatabase<GameConditionDef>.GetNamed("SolarFlare");

    private static readonly List<(Func<Map, string> rule, ActiveEffectNetworks networks)> rules = new()
    {
        ((Func<Map, string>)(map => map.gameConditionManager.ConditionIsActive(SolarFlareDef) ? "solar flare" : null),
            ActiveEffectNetworks.Solar | ActiveEffectNetworks.Warp)
    };

    internal static void DeclareTunables(TunableScope s) =>
        s.In("Interruptions").Add(StoneTabs.Interruptions, "Pause active effects and Solar collection during solar flares");

    // Master switch from the settings window. Interruptions also need Sunstone, since Lunite is the only counterplay.
    private static bool SettingEnabled => SolarRules.Enabled && StoneTabs.Interruptions.On;

    // Bumped when the settings window closes, so cached reasons pick up the switches without waiting for the next pass.
    private static int settingsVersion;
    public static void SettingsChanged() => settingsVersion++;

    // Toggle checks run inside per-pawn loops, so the last map's cache is kept instead of searching the map's components.
    private static Map lastMap;
    private static InterruptionCache lastCache;

    public static void Register(Func<Map, string> rule, ActiveEffectNetworks networks) => rules.Add((rule, networks));

    // The short reason the network is interrupted on this map, or null. Cached for one 250-tick pass.
    public static string Reason(Map map, ActiveEffectNetworks network)
    {
        if (map == null) return null;
        if (map != lastMap)
        {
            lastCache = map.GetComponent<InterruptionCache>();
            lastMap = map;
        }
        var cache = lastCache;
        int bucket = Find.TickManager.TicksGame / SolarRules.PassInterval;
        if (cache.bucket != bucket || cache.version != settingsVersion)
        {
            cache.bucket = bucket;
            cache.version = settingsVersion;
            bool enabled = SettingEnabled;
            cache.solar = enabled ? Evaluate(map, ActiveEffectNetworks.Solar) : null;
            cache.warp = enabled ? Evaluate(map, ActiveEffectNetworks.Warp) : null;
        }
        return network == ActiveEffectNetworks.Solar ? cache.solar : cache.warp;
    }

    internal static void Forget(Map map)
    {
        if (map != lastMap) return;
        lastMap = null;
        lastCache = null;
    }

    private static string Evaluate(Map map, ActiveEffectNetworks network)
    {
        foreach (var (rule, networks) in rules)
        {
            if ((networks & network) == 0) continue;
            string reason = rule(map);
            if (reason != null) return reason;
        }
        return null;
    }

    // True when the network is interrupted and the fixture has no stabilizer to ride it out.
    public static bool Blocked(Thing fixture, ActiveEffectNetworks network) =>
        fixture.Map is { } map && Reason(map, network) != null && !LuniteLinkTracker.IsStabilized(fixture);

    public static string BlockedReason(Thing fixture, ActiveEffectNetworks network) => Blocked(fixture, network) ? Reason(fixture.Map, network) : null;
}

public class InterruptionCache : MapComponent
{
    public int bucket = -1, version = -1;
    public string solar, warp;
    public InterruptionCache(Map map) : base(map) { }
    public override void MapRemoved() => ActiveEffectInterruption.Forget(map);
}
