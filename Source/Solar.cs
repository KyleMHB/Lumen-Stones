using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public static class SolarRules
{
    public const float FullSunTicks = 33000f, DepletedBelow = 0.01f, DayGlow = 0.4f;
    public const int PassInterval = 250, GlowSteps = 4;
    // Broadcast to a fixture's comps when its Solar active effect is toggled, so lights can react at once.
    public const string ToggledSignal = "SolarToggled";

    public static float Hysteresis = 5f;
    // Fog, rain and gentle snow, thunderstorms, then foggy rain and hard snow. Unlisted weather is full sun.
    public static readonly float[] WeatherFactors = { 0.7f, 0.6f, 0.5f, 0.4f };
    private static readonly Dictionary<string, int> weatherTier = new()
    {
        ["Fog"] = 0, ["Rain"] = 1, ["SnowGentle"] = 1, ["DryThunderstorm"] = 2, ["RainyThunderstorm"] = 2, ["FoggyRain"] = 3, ["SnowHard"] = 3
    };

    private static ResearchProjectDef research;
    public static ResearchProjectDef Research => research ??= DefDatabase<ResearchProjectDef>.GetNamed("KM_SunstoneChanneling");

    // With Sunstone off, active effects are free and nothing about Solar energy is shown.
    public static bool Enabled => LumenstoneMod.Settings.Value("Sunstone.enabled", 1) != 0;
    public static bool Visible => Enabled && Research.IsFinished;

    public static float WeatherFactor(Map map) =>
        weatherTier.TryGetValue(map.weatherManager.curWeather.defName, out int tier) ? WeatherFactors[tier] : 1f;
}

// Battery gauge layers named <texPath>Charge1..N. Their materials are looked up once per def, not from a path each frame.
public static class ChargeLayers
{
    private static readonly Dictionary<ThingDef, Material[]> materials = new();

    public static void Draw(Thing thing, int lit, int layers)
    {
        if (lit <= 0) return;
        var graphic = thing.def.graphicData;
        if (!materials.TryGetValue(thing.def, out var set))
        {
            set = new Material[layers];
            for (int i = 0; i < layers; i++) set[i] = MaterialPool.MatFrom(graphic.texPath + "Charge" + (i + 1), ShaderDatabase.Transparent);
            materials[thing.def] = set;
        }
        var position = thing.DrawPos + graphic.drawOffset;
        position.y = AltitudeLayer.BuildingOnTop.AltitudeFor() + 0.02f;
        var matrix = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(graphic.drawSize.x, 1f, graphic.drawSize.y));
        for (int i = 0; i < lit && i < set.Length; i++) Graphics.DrawMesh(MeshPool.plane10, matrix, set[i], 0);
    }
}

public class CompProperties_Sunstone : CompProperties
{
    public float capacity = 50f;
    // Nominal Solar energy per day under a clear midday sky. Zero for storage batteries.
    public float output;
    // Glow radius at night for collectors and when empty for batteries, as a fraction of the glower's radius.
    public float minGlowFraction = 0.5f;
    // Batteries draw full-canvas gauge layers named <texPath>Charge1..N as the charge reaches each Nth.
    public int chargeLayers = 4;
    public CompProperties_Sunstone() => compClass = typeof(CompSunstone);

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        string keep = "Every Sunstone collector and battery on a map shares one Solar network, however far apart. Charge is lost if it is destroyed or deconstructed, and kept if it is minified and reinstalled.";
        var rows = new InfoStats().Add("Solar energy capacity", capacity.ToString("0"), keep);
        if (output > 0)
            rows.Add("Solar energy per day", output.ToString("0"),
                "Under a clear midday sky. Less in fog, rain and storms, at dawn and dusk, and under a roof, and none at night. Surplus fills the network's batteries. " + keep);
        return rows.Rows;
    }
}

// Collector and battery in one: collectors have an output, batteries do not. All updates come from SolarNetwork's pass.
public class CompSunstone : ThingComp
{
    public float stored, ratePerTick;
    private float unroofed = 1f;
    private int glowStep = -1;
    private CompGlower glower;

    public CompProperties_Sunstone Props => (CompProperties_Sunstone)props;
    public bool IsCollector => Props.output > 0;
    public float Spare => Mathf.Max(0, Props.capacity - stored);

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        parent.Map.GetComponent<SolarNetwork>().Register(this);
        RefreshUnroofed();
        UpdateGlow(IsCollector ? (parent.Map.skyManager.CurSkyGlow >= SolarRules.DayGlow ? 1 : 0) : FillStep());
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish) => map.GetComponent<SolarNetwork>().Unregister(this);
    public override void PostExposeData() => Scribe_Values.Look(ref stored, "solarStored");

    // Same layering as the Warp batteries: the base sprite is empty and each lit gauge is drawn over it.
    public override void PostDraw()
    {
        if (IsCollector || Props.chargeLayers <= 0) return;
        int lit = Mathf.FloorToInt(Mathf.Clamp01(stored / Props.capacity) * Props.chargeLayers + 1e-4f);
        ChargeLayers.Draw(parent, lit, Props.chargeLayers);
    }

    public int FillStep() => Mathf.RoundToInt(Mathf.Clamp01(stored / Props.capacity) * SolarRules.GlowSteps);

    public void RefreshUnroofed()
    {
        int total = 0, open = 0;
        foreach (var cell in parent.OccupiedRect())
        {
            total++;
            if (!parent.Map.roofGrid.Roofed(cell)) open++;
        }
        unroofed = total == 0 ? 0 : (float)open / total;
    }

    public float UnroofedFraction => unroofed;

    // Collectors take 0 (night) or 1 (day). Batteries take their fill step.
    public void UpdateGlow(int step)
    {
        if (step == glowStep) return;
        glowStep = step;
        glower ??= parent.GetComp<CompGlower>();
        if (glower == null) return;
        float baseRadius = parent.def.GetCompProperties<CompProperties_Glower>().glowRadius, min = Props.minGlowFraction;
        float fraction = IsCollector ? (step == 1 ? 1f : min) : Mathf.Lerp(min, 1f, (float)step / SolarRules.GlowSteps);
        // The glow grid caches lit radii, so an already registered glower must be re-registered.
        if (glower.Glows && parent.Spawned)
        {
            var grid = parent.Map.glowGrid;
            grid.DeRegisterGlower(glower);
            glower.GlowRadius = baseRadius * fraction;
            grid.RegisterGlower(glower);
        }
        else glower.GlowRadius = baseRadius * fraction;
    }

    public override string CompInspectStringExtra()
    {
        if (!parent.Spawned) return null;
        var network = parent.Map.GetComponent<SolarNetwork>();
        string text = "Solar energy: " + stored.ToString("0") + " / " + Props.capacity.ToString("0");
        if (IsCollector)
            text += "\nGenerating " + (ratePerTick * GenDate.TicksPerDay).ToString("0") + " a day now (up to " + Props.output.ToString("0") + ")";
        text += "\nNetwork: " + network.TotalStored.ToString("0") + " / " + network.TotalCapacity.ToString("0");
        if (network.Depleted) text += "\nDepleted. Active effects resume at " + SolarRules.Hysteresis.ToString("0") + " stored.";
        return text;
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (!Prefs.DevMode) yield break;
        yield return new Command_Action { defaultLabel = "DEV: +25 Solar", action = () => stored = Mathf.Min(Props.capacity, stored + 25f) };
        yield return new Command_Action { defaultLabel = "DEV: empty", action = () => stored = 0 };
    }
}

// A continuous drain. The network asks once per pass for the rate and charges the elapsed interval, then caches whether it was paid.
public interface ISolarConsumer
{
    // Solar energy wanted per tick right now, or 0 when idle.
    float SolarDrainPerTick { get; }
    // Set by the network each pass. False until the first pass, and when a draw is refused.
    bool SolarPowered { get; set; }
}

// One network per map. Collectors fill their own buffers first and spill into batteries. Draws take buffers first, then batteries.
public class SolarNetwork : MapComponent
{
    private readonly List<CompSunstone> collectors = new(), batteries = new();
    private readonly List<ISolarConsumer> consumers = new();
    private bool depleted;

    public SolarNetwork(Map map) : base(map) { }

    public bool Depleted => depleted;
    public bool Available => !depleted && TotalStored >= SolarRules.DepletedBelow;
    public static SolarNetwork For(Map map) => map.GetComponent<SolarNetwork>();

    public float TotalStored => Sum(collectors) + Sum(batteries);
    public float TotalCapacity
    {
        get
        {
            float total = 0;
            foreach (var c in collectors) total += c.Props.capacity;
            foreach (var b in batteries) total += b.Props.capacity;
            return total;
        }
    }

    private static float Sum(List<CompSunstone> list)
    {
        float total = 0;
        foreach (var c in list) total += c.stored;
        return total;
    }

    public void Register(CompSunstone comp)
    {
        var list = comp.IsCollector ? collectors : batteries;
        if (!list.Contains(comp)) list.Add(comp);
    }

    public void Unregister(CompSunstone comp) => (comp.IsCollector ? collectors : batteries).Remove(comp);

    public void Register(ISolarConsumer consumer)
    {
        if (!consumers.Contains(consumer)) consumers.Add(consumer);
    }

    public void Unregister(ISolarConsumer consumer) => consumers.Remove(consumer);

    public override void ExposeData() => Scribe_Values.Look(ref depleted, "solarDepleted");

    public override void MapComponentTick()
    {
        if ((collectors.Count == 0 && batteries.Count == 0 && consumers.Count == 0) || Find.TickManager.TicksGame % SolarRules.PassInterval != 0) return;
        float sky = map.skyManager.CurSkyGlow, weather = SolarRules.WeatherFactor(map);
        int dayStep = sky >= SolarRules.DayGlow ? 1 : 0;
        float overflow = 0;
        foreach (var collector in collectors)
        {
            collector.RefreshUnroofed();
            collector.ratePerTick = collector.Props.output / SolarRules.FullSunTicks * sky * weather * collector.UnroofedFraction;
            float gain = collector.ratePerTick * SolarRules.PassInterval, taken = Mathf.Min(gain, collector.Spare);
            collector.stored += taken;
            overflow += gain - taken;
            collector.UpdateGlow(dayStep);
        }
        float spare = 0;
        foreach (var battery in batteries) spare += battery.Spare;
        float deposit = Mathf.Min(overflow, spare);
        foreach (var battery in batteries)
        {
            if (deposit > 0) battery.stored = Mathf.Min(battery.Props.capacity, battery.stored + deposit * battery.Spare / spare);
            battery.UpdateGlow(battery.FillStep());
        }
        UpdateDepletion();
        // Idle consumers are powered when the network is. A refused draw leaves that consumer unpowered until the next pass.
        foreach (var consumer in consumers)
        {
            float want = consumer.SolarDrainPerTick * SolarRules.PassInterval;
            consumer.SolarPowered = want > 0 ? TryConsumeContinuous(want) : Available;
        }
    }

    private void UpdateDepletion()
    {
        float stored = TotalStored;
        if (depleted) { if (stored >= SolarRules.Hysteresis) depleted = false; }
        else if (stored < SolarRules.DepletedBelow) depleted = true;
    }

    // Read-only check for ownership decisions. Free when Sunstone is off.
    public bool CanDraw(float amount) => !SolarRules.Enabled || amount <= 0 || (!depleted && amount <= TotalStored);

    // All or nothing, for per-event costs. A refusal does not deplete the network.
    public bool TryConsume(float amount) => Draw(amount, false);

    // For costs charged over time. A refusal marks the network depleted until it recovers.
    public bool TryConsumeContinuous(float amount) => Draw(amount, true);

    private bool Draw(float amount, bool continuous)
    {
        if (!SolarRules.Enabled || amount <= 0) return true;
        if (depleted || amount > TotalStored)
        {
            if (continuous) depleted = true;
            return false;
        }
        float fromCollectors = Mathf.Min(amount, Sum(collectors));
        Take(collectors, fromCollectors);
        Take(batteries, amount - fromCollectors);
        UpdateDepletion();
        return true;
    }

    private static void Take(List<CompSunstone> list, float amount)
    {
        float total = Sum(list);
        if (amount <= 0 || total <= 0) return;
        foreach (var c in list) c.stored = Mathf.Max(0, c.stored - amount * c.stored / total);
    }
}

public class CompProperties_SolarToggle : CompProperties
{
    public string effect = "its active effect";
    // Replaces the default description when an effect needs a warning.
    public string description;
    public CompProperties_SolarToggle() => compClass = typeof(CompSolarToggle);
}

// Attached to every fixture that has an active effect. Consumers ask Active before spending Solar energy.
public class CompSolarToggle : ThingComp
{
    private bool on = true;
    public CompProperties_SolarToggle Props => (CompProperties_SolarToggle)props;

    public override void PostExposeData() => Scribe_Values.Look(ref on, "solarActiveOn", true);

    public bool ToggledOn => on;

    // True when the active effect may run: always with Sunstone off, otherwise only toggled on and with Solar energy.
    public bool Active => !SolarRules.Enabled || (on && SolarNetwork.For(parent.Map).Available);

    // Pays a per-event cost. Free with Sunstone off, refused when toggled off or when the network cannot cover it.
    public bool TryPay(float amount) => !SolarRules.Enabled || (on && SolarNetwork.For(parent.Map).TryConsume(amount));

    public string Status => !on ? "Toggled off" : SolarNetwork.For(parent.Map).Available ? "Powered" : "No Solar energy";

    public override string CompInspectStringExtra() => SolarRules.Visible && parent.Spawned ? "Solar active effect: " + Status : null;

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (!SolarRules.Visible) yield break;
        yield return new Command_Toggle
        {
            defaultLabel = "Solar active effect",
            defaultDesc = Props.description ?? "Spend Solar energy on " + Props.effect + ". When off, only the passive effect works.",
            icon = TexCommand.ForbidOn,
            isActive = () => on,
            toggleAction = () => { on = !on; parent.BroadcastCompSignal(SolarRules.ToggledSignal); }
        };
    }
}
