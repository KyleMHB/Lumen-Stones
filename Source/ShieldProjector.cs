using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public static class BastionRules
{
    // Solar energy per day while the dome is up, and per shield hit point recharged.
    public static float UpkeepPerDay = 150f, SolarPerHp = 0.5f;
    // Shield hit points spent when a hostile drop pod or infestation spawner is crushed.
    public static float CrushHpCost = 150f;

    internal static void DeclareTunables(TunableScope s)
    {
        var projector = s.In("Lumen_Projector_Section".Translate());
        var props = DefDatabase<ThingDef>.GetNamed("KM_AegiteShieldProjector").GetCompProperties<CompProperties_AegiteShieldProjector>();
        projector.Add("KM_AegiteShieldProjector.radius", "Lumen_Projector_RadiusSetting".Translate(), props.radius, v => props.radius = v, Limit.Number(1, 40));
        projector.Add("KM_AegiteShieldProjector.hitPoints", "Lumen_Projector_HitPointsSetting".Translate(), props.hitPoints, v => props.hitPoints = (int)v, Limit.Integer(1));
        projector.Add("KM_AegiteShieldProjector.upkeep", "Lumen_Projector_Upkeep".Translate(), UpkeepPerDay, v => UpkeepPerDay = v);
        projector.Add("KM_AegiteShieldProjector.solarPerHp", "Lumen_Projector_SolarPerHpSetting".Translate(), SolarPerHp, v => SolarPerHp = v);
        projector.Add("KM_AegiteShieldProjector.rechargeTicks", "Lumen_Projector_RechargeTicks".Translate(), props.rechargeHitPointsIntervalTicks,
            v => props.rechargeHitPointsIntervalTicks = (int)v, Limit.Integer(1, 60000));
        projector.Add("KM_AegiteShieldProjector.crushCost", "Lumen_Projector_CrushCost".Translate(), CrushHpCost,
            v => CrushHpCost = v);
        projector.Add("KM_AegiteShieldProjector.rebootTicks", "Lumen_Projector_RebootTicks".Translate(), props.chargeDurationTicks,
            v => props.chargeDurationTicks = (int)v, Limit.Integer(1));
        projector.Add("KM_AegiteShieldProjector.empTicks", "Lumen_Projector_EmpTicks".Translate(), props.disarmedByEmpForTicks,
            v => props.disarmedByEmpForTicks = (int)v, Limit.Integer(1));
    }
}

public class CompProperties_AegiteShieldProjector : CompProperties_ProjectileInterceptor
{
    public CompProperties_AegiteShieldProjector() => compClass = typeof(CompAegiteShieldProjector);

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        var rows = new InfoStats()
            .Add(Effects.ProjectorInterception, "Lumen_Projector_Radius".Translate(), InfoStats.Tiles(radius), "Lumen_Projector_RadiusDesc".Translate())
            .Add("Lumen_Projector_HitPoints".Translate(), hitPoints.ToString("0"), "Lumen_Projector_HitPointsDesc".Translate((chargeDurationTicks / (float)GenDate.TicksPerDay).ToString("0.##"), InfoStats.Seconds(rechargeHitPointsIntervalTicks)))
            .Add(Effects.ProjectorCrushing, "Lumen_Projector_Crushes".Translate(), "Lumen_Projector_CrushesValue".Translate(), "Lumen_Projector_CrushesDesc".Translate(BastionRules.CrushHpCost.ToString("0")))
            .Add("Lumen_Projector_Emp".Translate(), "Lumen_Projector_EmpValue".Translate(), "Lumen_Projector_EmpDesc".Translate());
        if (SolarRules.Enabled)
        {
            rows.Add("Lumen_Info_SolarPerDay".Translate(), BastionRules.UpkeepPerDay.ToString("0"), "Lumen_Projector_UpkeepDesc".Translate() + InfoStats.SolarRule)
                .Add("Lumen_Projector_SolarPerHp".Translate(), BastionRules.SolarPerHp.ToString("0.##"), "Lumen_Projector_SolarPerHpDesc".Translate());
        }
        return rows.Rows;
    }
}

// A vanilla interceptor that only runs while its Solar active effect is on, powered and not interrupted. Vanilla finds it
// through the projectile interceptor thing group, so no projectile code is patched.
public class CompAegiteShieldProjector : CompProjectileInterceptor, IActiveEffectDrain
{
    private CompSolarToggle toggle;
    public CompSolarToggle Toggle => toggle ??= parent.GetComp<CompSolarToggle>();

    public float DrainPerTick => currentHitPoints > 0 && Effects.ProjectorInterception.On ? BastionRules.UpkeepPerDay / GenDate.TicksPerDay : 0;

    // Read by the Active postfix. Losing power, toggling off or switching the dome off in settings drops it without a reboot.
    public bool Allowed => Effects.ProjectorInterception.On && Toggle.Active && Toggle.Powered;

    // Vanilla asks once per recharge interval. One point costs Solar energy, and none is gained without it.
    protected override int HitPointsPerInterval =>
        currentHitPoints < HitPointsMax && Effects.ProjectorInterception.On && Toggle.Active && Toggle.Account.TryDraw(BastionRules.SolarPerHp) ? 1 : 0;

    private static readonly System.Reflection.FieldInfo startedCharging = AccessTools.Field(typeof(CompProjectileInterceptor), "startedChargingTick");

    // Spends shield hit points. At 0 the dome breaks and its reboot starts, as when projectiles empty it.
    public void Spend(float amount)
    {
        currentHitPoints = Mathf.Max(0, currentHitPoints - Mathf.RoundToInt(amount));
        if (currentHitPoints > 0) return;
        startedCharging?.SetValue(this, Find.TickManager.TicksGame);
        FleckMaker.ThrowLightningGlow(parent.TrueCenter(), parent.Map, 3f);
    }

    public bool Covers(IntVec3 cell) =>
        (cell.ToVector3Shifted() - parent.TrueCenter()).MagnitudeHorizontalSquared() <= Props.radius * Props.radius;
}

[HarmonyPatch(typeof(CompProjectileInterceptor), nameof(CompProjectileInterceptor.Active), MethodType.Getter)]
public static class AegiteProjectorActive
{
    public static void Postfix(CompProjectileInterceptor __instance, ref bool __result)
    {
        if (__result && __instance is CompAegiteShieldProjector projector && !projector.Allowed) __result = false;
    }
}

public static class AegiteDome
{
    // The covering dome with the most shield left pays. Domes at 0, rebooting or offline do not count.
    public static CompAegiteShieldProjector Covering(Map map, IntVec3 cell)
    {
        if (!Effects.ProjectorCrushing.On) return null;
        CompAegiteShieldProjector best = null;
        var domes = map.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor);
        for (int i = 0; i < domes.Count; i++)
            if (domes[i].TryGetComp<CompAegiteShieldProjector>() is { Active: true, currentHitPoints: > 0 } dome && dome.Covers(cell) &&
                (best == null || dome.currentHitPoints > best.currentHitPoints)) best = dome;
        return best;
    }

    // whatKey names a Keyed noun phrase such as "a hostile drop pod".
    public static void Announce(CompAegiteShieldProjector dome, IntVec3 cell, string whatKey)
    {
        FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(), dome.parent.Map, 2f);
        Messages.Message((dome.currentHitPoints <= 0 ? "Lumen_Projector_CrushedBroke" : "Lumen_Projector_Crushed").Translate(whatKey.Translate()), dome.parent,
            MessageTypeDefOf.PositiveEvent);
    }
}

[HarmonyPatch(typeof(DropPodIncoming), "Impact")]
public static class AegiteDomeCrushesPods
{
    public static void Prefix(DropPodIncoming __instance)
    {
        var contents = __instance.Contents?.innerContainer;
        var map = __instance.Map;
        if (contents == null || map == null) return;
        bool hostile = false;
        foreach (var thing in contents)
            if (thing is Pawn pawn && pawn.HostileTo(Faction.OfPlayer)) { hostile = true; break; }
        if (!hostile || AegiteDome.Covering(map, __instance.Position) is not { } dome) return;
        dome.Spend(BastionRules.CrushHpCost);
        foreach (var thing in new List<Thing>(contents))
        {
            if (thing is Pawn pawn)
            {
                // Bare corpses: gear is destroyed, and the projector gets the kill like a trap would.
                pawn.equipment?.DestroyAllEquipment();
                pawn.apparel?.DestroyAll();
                pawn.inventory?.DestroyAll();
                pawn.Kill(new DamageInfo(DamageDefOf.Crush, 9999f, instigator: dome.parent));
            }
            else if (!thing.Destroyed) thing.Destroy();
        }
        AegiteDome.Announce(dome, __instance.Position, "Lumen_Projector_DropPod");
    }
}

[HarmonyPatch(typeof(TunnelHiveSpawner), "Spawn")]
public static class AegiteDomeCrushesInfestations
{
    // The spawner still finishes as usual, but there is nothing left for it to release.
    public static void Prefix(TunnelHiveSpawner __instance, Map map, IntVec3 loc)
    {
        if (AegiteDome.Covering(map, loc) is not { } dome) return;
        dome.Spend(BastionRules.CrushHpCost);
        __instance.spawnHive = false;
        __instance.insectsPoints = 0f;
        AegiteDome.Announce(dome, loc, "Lumen_Projector_Infestation");
    }
}
