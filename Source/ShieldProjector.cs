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
}

public class CompProperties_AegiteShieldProjector : CompProperties_ProjectileInterceptor
{
    public CompProperties_AegiteShieldProjector() => compClass = typeof(CompAegiteShieldProjector);

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        var rows = new InfoStats()
            .Add("Dome radius", InfoStats.Tiles(radius), "Stops hostile ground projectiles, mortar shells and orbital bombardments fired from outside it. Colonists shoot out freely.")
            .Add("Shield hit points", hitPoints.ToString("0"), "Reduced by what the dome stops. At 0 it shuts down and reboots for " + (chargeDurationTicks / (float)GenDate.TicksPerDay).ToString("0.##") + " days, then recharges gradually.")
            .Add("Crushes", "hostile drop pods and infestations", "A hostile raid drop pod or tunneling infestation that lands inside the dome is crushed, costing " + BastionRules.CrushHpCost.ToString("0") +
                " shield hit points. Pawns inside a pod die and their gear is destroyed. Refugee, trade, resource and quest pods are not affected.")
            .Add("EMP", "disarms briefly", "Keeps its hit points.");
        if (SolarRules.Enabled)
        {
            rows.Add("Solar energy per day", BastionRules.UpkeepPerDay.ToString("0"), "Spent while the dome is up." + InfoStats.SolarRule)
                .Add("Solar energy per hit point", BastionRules.SolarPerHp.ToString("0.##"), "Spent for each shield hit point recharged. Pair it with a Lunite stabilizer to keep it up through solar flares.");
        }
        return rows.Rows;
    }
}

// A vanilla interceptor that only runs while its Solar active effect is on, powered and not interrupted. Vanilla finds it
// through the projectile interceptor thing group, so no projectile code is patched.
public class CompAegiteShieldProjector : CompProjectileInterceptor, ISolarConsumer
{
    private CompSolarToggle toggle;
    public CompSolarToggle Toggle => toggle ??= parent.GetComp<CompSolarToggle>();

    // True until the first Solar pass, so a new or loaded dome is not down for a pass before the network is asked.
    public bool SolarPowered { get; set; } = true;

    public float SolarDrainPerTick => SolarRules.Enabled && parent.Spawned && currentHitPoints > 0 && Toggle.Running
        ? BastionRules.UpkeepPerDay / GenDate.TicksPerDay : 0;

    // Read by the Active postfix. Losing power or toggling off drops the dome without starting a reboot.
    public bool Allowed => Toggle.Active && SolarPowered;

    // Vanilla asks once per recharge interval. One point costs Solar energy, and none is gained without it.
    protected override int HitPointsPerInterval =>
        currentHitPoints < HitPointsMax && Toggle.Active && network.TryConsume(BastionRules.SolarPerHp) ? 1 : 0;

    private SolarNetwork network;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        network = SolarNetwork.For(parent.Map);
        network.Register(this);
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        SolarNetwork.For(map).Unregister(this);
        network = null;
        base.PostDeSpawn(map, mode);
    }

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
        CompAegiteShieldProjector best = null;
        var domes = map.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor);
        for (int i = 0; i < domes.Count; i++)
            if (domes[i].TryGetComp<CompAegiteShieldProjector>() is { Active: true, currentHitPoints: > 0 } dome && dome.Covers(cell) &&
                (best == null || dome.currentHitPoints > best.currentHitPoints)) best = dome;
        return best;
    }

    public static void Announce(CompAegiteShieldProjector dome, IntVec3 cell, string what)
    {
        FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(), dome.parent.Map, 2f);
        Messages.Message("The Aegite shield dome crushed " + what + (dome.currentHitPoints <= 0 ? " and broke." : "."), dome.parent,
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
        AegiteDome.Announce(dome, __instance.Position, "a hostile drop pod");
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
        AegiteDome.Announce(dome, loc, "a tunneling infestation");
    }
}
