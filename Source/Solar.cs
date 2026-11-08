using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public static class SolarRules
{
    public const float FullSunTicks = 33000f, DepletedBelow = ActiveEffectCore.DepletedBelow, DayGlow = 0.4f;
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
    public static bool Enabled => StoneTabs.Sunstone.Enabled.On;
    public static bool Visible => Enabled && Research.IsFinished;

    internal static void DeclareTunables(TunableScope s)
    {
        var network = s.In("Solar network");
        foreach (var (key, label, name) in new[] {
            ("collectorOutput", "Collector output per day at full sun", "KM_SunstoneCollector"),
            ("arrayOutput", "Array output per day at full sun", "KM_SunstoneArray") })
        {
            var comp = DefDatabase<ThingDef>.GetNamed(name).GetCompProperties<CompProperties_Sunstone>();
            network.Add("Sunstone." + key, label, comp.output, v => comp.output = v, Limit.Number(0.01f));
        }
        foreach (var (key, label, name) in new[] {
            ("vesselCapacity", "Vessel capacity", "KM_SunstoneVessel"),
            ("obeliskCapacity", "Obelisk capacity", "KM_SunstoneObelisk") })
        {
            var comp = DefDatabase<ThingDef>.GetNamed(name).GetCompProperties<CompProperties_Sunstone>();
            network.Add("Sunstone." + key, label, comp.capacity, v => comp.capacity = v, Limit.Number(1));
        }
        network.Add("Sunstone.solarHysteresis", "Stored energy needed to recover from depletion", Hysteresis, v => Hysteresis = v, Limit.Number(0.02f));
        string[] weather = { "Fog", "Rain and gentle snow", "Thunderstorms", "Foggy rain and hard snow" };
        for (int i = 0; i < weather.Length; i++)
        {
            int tier = i;
            network.Add("Sunstone.weather" + tier, weather[i] + " output factor (%)", WeatherFactors[tier], v => WeatherFactors[tier] = v, Limit.Percent());
        }
    }

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
            text += "\nGenerating " + (ratePerTick * GenDate.TicksPerDay).ToString("0") + " a day now (up to " + Props.output.ToString("0") + ")" +
                (ActiveEffectInterruption.Reason(parent.Map, ActiveEffectNetworks.Solar) is { } reason ? "\nInterrupted: " + reason : "");
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

// One network per map. Collectors fill their own buffers first and spill into batteries. Draws take buffers first, then batteries.
public class SolarNetwork : MapComponent, IEnergyStore
{
    private readonly List<CompSunstone> collectors = new(), batteries = new();
    public readonly EnergyAccount Account;

    public SolarNetwork(Map map) : base(map) => Account = new(ActiveEffectKind.Solar, this);

    public bool Depleted => Account.Depleted;
    public bool Available => Account.Available;
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

    public override void ExposeData() => Account.ExposeData("solarDepleted");

    public override void MapComponentTick()
    {
        if ((collectors.Count == 0 && batteries.Count == 0 && !Account.HasDrains) || Find.TickManager.TicksGame % SolarRules.PassInterval != 0) return;
        bool flared = ActiveEffectInterruption.Reason(map, ActiveEffectNetworks.Solar) != null;
        float sky = map.skyManager.CurSkyGlow, weather = SolarRules.WeatherFactor(map);
        int dayStep = sky >= SolarRules.DayGlow ? 1 : 0;
        float overflow = 0;
        foreach (var collector in collectors)
        {
            collector.RefreshUnroofed();
            collector.ratePerTick = flared ? 0 : collector.Props.output / SolarRules.FullSunTicks * sky * weather * collector.UnroofedFraction;
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
        Account.RefreshDepletion();
        Account.PollDrains(SolarRules.PassInterval);
    }

    public bool CanDraw(float amount) => Account.CanDraw(amount);
    public bool TryConsume(float amount) => Account.TryDraw(amount);
    public bool TryConsumeContinuous(float amount) => Account.TryDrawContinuous(amount);

    float IEnergyStore.Stored => TotalStored;

    // Collectors pay first, then batteries.
    void IEnergyStore.Withdraw(float amount)
    {
        float fromCollectors = Mathf.Min(amount, Sum(collectors));
        Take(collectors, fromCollectors);
        Take(batteries, amount - fromCollectors);
    }

    private static void Take(List<CompSunstone> list, float amount)
    {
        float total = Sum(list);
        if (amount <= 0 || total <= 0) return;
        foreach (var c in list) c.stored = Mathf.Max(0, c.stored - amount * c.stored / total);
    }
}
