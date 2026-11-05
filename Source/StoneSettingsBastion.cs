using RimWorld;
using Verse;

namespace LumenStones;

// Balance numbers for active effect interruptions, Lunite and the Aegite defences. Costs stay in XML.
public static partial class StoneSettings
{
    private static void AddBastion()
    {
        const string sunstone = "Sunstone";
        Add(sunstone, "Interruptions", "Interruptions.enabled", "Pause active effects and Solar collection during solar flares", 1, toggle: true);
        Add(sunstone, "Lunite", "Lunite.sunstoneCost", "Sunstone spent per conversion", LuniteRules.SunstoneCost,
            v => LuniteRules.SunstoneCost = (int)v, min: 1, integer: true);
        Add(sunstone, "Lunite", "Lunite.yield", "Lunite produced per conversion", LuniteRules.Yield,
            v => LuniteRules.Yield = (int)v, min: 1, integer: true);
        Add(sunstone, "Lunite", "Lunite.cooldownDays", "Cooldown after a conversion (days)", LuniteRules.CooldownDays,
            v => LuniteRules.CooldownDays = v);
        Add(sunstone, "Lunite stabilizer", "Lunite.linkRadius", "Link radius (tiles)", LuniteRules.LinkRadius,
            v => LuniteRules.LinkRadius = v, min: 1, max: 30);

        const string projector = "Aegite shield projector";
        var props = DefDatabase<ThingDef>.GetNamed("KM_AegiteShieldProjector").GetCompProperties<CompProperties_AegiteShieldProjector>();
        Add(sunstone, projector, "KM_AegiteShieldProjector.radius", "Dome radius (tiles)", props.radius, v => props.radius = v, min: 1, max: 40);
        Add(sunstone, projector, "KM_AegiteShieldProjector.hitPoints", "Dome hit points", props.hitPoints, v => props.hitPoints = (int)v, min: 1, integer: true);
        Add(sunstone, projector, "KM_AegiteShieldProjector.upkeep", "Solar energy per day while up", BastionRules.UpkeepPerDay, v => BastionRules.UpkeepPerDay = v);
        Add(sunstone, projector, "KM_AegiteShieldProjector.solarPerHp", "Solar energy per hit point recharged", BastionRules.SolarPerHp, v => BastionRules.SolarPerHp = v);
        Add(sunstone, projector, "KM_AegiteShieldProjector.crushCost", "Hit points spent crushing a hostile drop pod or infestation", BastionRules.CrushHpCost,
            v => BastionRules.CrushHpCost = v);
        Add(sunstone, projector, "KM_AegiteShieldProjector.rebootTicks", "Reboot time after breaking (ticks)", props.chargeDurationTicks,
            v => props.chargeDurationTicks = (int)v, min: 1, integer: true);
        Add(sunstone, projector, "KM_AegiteShieldProjector.empTicks", "Time disarmed by EMP (ticks)", props.disarmedByEmpForTicks,
            v => props.disarmedByEmpForTicks = (int)v, min: 1, integer: true);

        const string charm = "Aegite shield charm";
        var charmDef = DefDatabase<ThingDef>.GetNamed("KM_AegiteShieldCharm");
        var shield = charmDef.GetCompProperties<CompProperties_Shield>();
        Add("Aegite", charm, "KM_AegiteShieldCharm.energyMax", "Maximum shield energy", charmDef.GetStatValueAbstract(StatDefOf.EnergyShieldEnergyMax),
            v => charmDef.SetStatBaseValue(StatDefOf.EnergyShieldEnergyMax, v), min: 0.01f, max: 10);
        Add("Aegite", charm, "KM_AegiteShieldCharm.rechargeRate", "Energy recharge rate per second", charmDef.GetStatValueAbstract(StatDefOf.EnergyShieldRechargeRate),
            v => charmDef.SetStatBaseValue(StatDefOf.EnergyShieldRechargeRate, v), min: 0.001f, max: 10);
        Add("Aegite", charm, "KM_AegiteShieldCharm.resetTicks", "Ticks to reset after breaking", shield.startingTicksToReset,
            v => shield.startingTicksToReset = (int)v, min: 1, max: 60000, integer: true);
    }
}
