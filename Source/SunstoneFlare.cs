using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace LumenStones;

public class CompProperties_InventoryFlare : CompProperties
{
    public CompProperties_InventoryFlare() => compClass = typeof(CompInventoryFlare);
}

// The item owns its verb, including while in a pawn's inventory. No equipment slot or DLC ability tracker is needed.
public class CompInventoryFlare : ThingComp, IVerbOwner
{
    private VerbTracker tracker;
    public CompInventoryFlare() => tracker = new VerbTracker(this);
    public Pawn Holder => (parent.ParentHolder as Pawn_InventoryTracker)?.pawn;
    public VerbTracker VerbTracker => tracker;
    public List<VerbProperties> VerbProperties => parent.def.Verbs;
    public List<Tool> Tools => null;
    public ImplementOwnerTypeDef ImplementOwnerTypeDef => ImplementOwnerTypeDefOf.Weapon;
    public Thing ConstantCaster => Holder;
    public string UniqueVerbOwnerID() => "InventoryFlare_" + parent.ThingID;
    public bool VerbsStillUsableBy(Pawn pawn) => Holder == pawn && !parent.Destroyed;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Deep.Look(ref tracker, "inventoryFlareVerbs", this);
    }

    public Command Command(Pawn pawn)
    {
        var verb = tracker.PrimaryVerb;
        verb.caster = pawn;
        var command = new Command_VerbTarget
        {
            defaultLabel = "Throw Sunstone flare", defaultDesc = parent.def.description,
            icon = parent.def.uiIcon, ownerThing = parent, verb = verb
        };
        if (pawn.Downed || pawn.stances?.FullBodyBusy == true || pawn.WorkTagIsDisabled(WorkTags.Violent))
            command.Disable("Cannot throw a flare right now.");
        return command;
    }

    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        foreach (var option in base.CompFloatMenuOptions(selPawn)) yield return option;
        if (!parent.Spawned || !selPawn.CanReach(parent, PathEndMode.Touch, Danger.Deadly)) yield break;
        yield return new FloatMenuOption("Take Sunstone flares (" + parent.stackCount + ")", () =>
        {
            var group = DefDatabase<InventoryStockGroupDef>.GetNamed("KM_SunstoneFlares");
            selPawn.inventoryStock?.SetCountForGroup(group, selPawn.inventory.Count(parent.def) + parent.stackCount);
            var job = JobMaker.MakeJob(JobDefOf.TakeInventory, parent);
            job.count = parent.stackCount;
            selPawn.jobs.TryTakeOrderedJob(job);
        });
    }
}

[HarmonyPatch(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.GetGizmos))]
public static class InventoryFlareCommands
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn_InventoryTracker __instance)
    {
        foreach (var gizmo in __result) yield return gizmo;
        var pawn = __instance.pawn;
        if (!pawn.IsColonistPlayerControlled || !pawn.Drafted || !pawn.Spawned) yield break;
        foreach (var item in __instance.innerContainer)
            if (item.TryGetComp<CompInventoryFlare>() is { } flare)
            {
                yield return flare.Command(pawn);
                yield break;
            }
    }
}

[HarmonyPatch(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.InventoryTrackerTick))]
public static class InventoryFlareTick
{
    public static void Postfix(Pawn_InventoryTracker __instance)
    {
        for (int i = __instance.innerContainer.Count - 1; i >= 0; i--)
            __instance.innerContainer[i].TryGetComp<CompInventoryFlare>()?.VerbTracker.VerbsTick();
    }
}

public class Verb_ThrowSunstoneFlare : Verb
{
    public override bool Available() => base.Available() && verbTracker.directOwner is CompInventoryFlare flare && flare.VerbsStillUsableBy(CasterPawn);

    protected override bool TryCastShot()
    {
        if (!Available() || !TryFindShootLineFromTo(caster.Position, currentTarget, out var line)) return false;
        var flare = (CompInventoryFlare)verbTracker.directOwner;
        var projectile = (Projectile)GenSpawn.Spawn(verbProps.defaultProjectile, line.Source, caster.Map);
        projectile.Launch(caster, caster.DrawPos, currentTarget.Cell, currentTarget, ProjectileHitFlags.IntendedTarget, false, flare.parent);
        flare.parent.SplitOff(1).Destroy();
        return true;
    }

    public override float HighlightFieldRadiusAroundTarget(out bool needLOSToCenter)
    {
        needLOSToCenter = true;
        return 2.9f;
    }
}

public class Projectile_SunstoneFlare : Projectile
{
    protected override void Impact(Thing hitThing, bool blockedByShield = false)
    {
        var map = Map;
        var center = Position;
        FleckMaker.Static(center, map, FleckDefOf.ExplosionFlash, 2.9f);
        foreach (var cell in GenRadial.RadialCellsAround(center, 2.9f, true))
        {
            if (!cell.InBounds(map) || !GenSight.LineOfSight(center, cell, map, true)) continue;
            foreach (var thing in cell.GetThingList(map).ToArray())
            {
                if (thing is not Pawn { Dead: false } pawn) continue;
                var def = DefDatabase<HediffDef>.GetNamed("KM_SolarBlindness");
                var blindness = pawn.health.hediffSet.GetFirstHediffOfDef(def) ?? pawn.health.AddHediff(def);
                blindness.TryGetComp<HediffComp_Disappears>().ticksToDisappear = 300;
                pawn.stances?.stunner?.StunFor(60, launcher);
                if (SunstoneAxe.Unnatural(pawn)) pawn.TakeDamage(new DamageInfo(DamageDefOf.Burn, 15f, instigator: launcher));
            }
        }
        Destroy();
    }
}
