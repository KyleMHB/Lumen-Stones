using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LumenStones;

public class CompProperties_Ferrite : CompProperties, ITunableProps
{
    public int tier = 1, interval = 300;
    public float radius = 8f, repairBudget = 0.00045f;
    public bool buildings, looseItems, wornEquipment, personal;
    public CompProperties_Ferrite() => compClass = typeof(CompFerrite);

    public void DeclareTunables(TunableScope s, Def owner)
    {
        string name = owner.defName;
        if (!personal)
            s.Add(name + ".radius", "Lumen_Ferrite_Radius".Translate(), radius, v => radius = v, Limit.Number(max: 100));
        s.Add(name + ".interval", "Lumen_Ferrite_Interval".Translate(), interval, v => interval = (int)v, Limit.Integer(1));
        s.Add(name + ".budget", "Lumen_Ferrite_BudgetSetting".Translate(), repairBudget, v => repairBudget = v, Limit.Percent());
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
        string budget = "Lumen_Ferrite_BudgetRule".Translate();
        var rows = new InfoStats();
        if (personal)
            rows.Add(Effects.FerriteHammerMending, "Lumen_Ferrite_Mends".Translate(), "Lumen_Ferrite_MendsSelf".Translate(), "Lumen_Ferrite_MendsSelfDesc".Translate() + " " + budget);
        else
        {
            rows.Add("Lumen_Ferrite_Radius".Translate(), InfoStats.Tiles(radius), "Lumen_Ferrite_RadiusDesc".Translate() + " " + budget);
            if (buildings) rows.Add(Effects.FerriteBuildings, "Lumen_Ferrite_MendsFree".Translate(), "Lumen_Ferrite_Buildings".Translate(), "Lumen_Ferrite_BuildingsDesc".Translate() + " " + budget);
            if (looseItems)
                rows.Add(Effects.FerriteItems, InfoStats.Solar("Lumen_Ferrite_MendsItems".Translate()), "Lumen_Ferrite_LooseItems".Translate(),
                    "Lumen_Ferrite_LooseItemsDesc".Translate() + InfoStats.SolarRule + " " + budget);
            if (wornEquipment)
                rows.Add(Effects.FerriteGear, InfoStats.Solar("Lumen_Ferrite_MendsGear".Translate()), "Lumen_Ferrite_ColonistGear".Translate(),
                    "Lumen_Ferrite_ColonistGearDesc".Translate() +
                    InfoStats.SolarRule + " " + budget);
            if ((looseItems || wornEquipment) && SolarRules.Enabled) rows.Add("Lumen_Ferrite_SolarPerHp".Translate(), FerriteSolar.CostPerHp.ToString("0.##"), "Lumen_Ferrite_SolarPerHpDesc".Translate());
        }
        rows.Add("Lumen_Aegite_PulseInterval".Translate(), InfoStats.Seconds(interval), budget)
            .Add("Lumen_Ferrite_Budget".Translate(), (repairBudget * 100).ToString("0.####") + "%", "Lumen_Ferrite_BudgetDesc".Translate() + " " + budget);
        return rows.Rows;
    }
}

public class FerriteRepairCredit : IExposable
{
    public Thing target;
    public double hitPoints;
    public int nextServiceTick;

    public void ExposeData()
    {
        Scribe_References.Look(ref target, "target");
        Scribe_Values.Look(ref hitPoints, "hitPoints");
        Scribe_Values.Look(ref nextServiceTick, "nextServiceTick");
    }
}

public static class FerriteSolar
{
    public static float CostPerHp = 0.5f;

    internal static void DeclareTunables(TunableScope s) =>
        s.In("Lumen_Section_SolarEnergy".Translate()).Add("Ferrite.solarCostPerHp", "Lumen_Ferrite_SolarPerHpSetting".Translate(), CostPerHp, v => CostPerHp = v);
}

public class FerriteField : MapComponent
{
    private const int PersonalPulseCheck = 60;
    public readonly List<CompFerrite> fixtures = new();
    private List<FerriteRepairCredit> credits = new();
    private Dictionary<Thing, FerriteRepairCredit> byTarget = new();
    public FerriteField(Map map) : base(map) { }

    public static bool Damaged(Thing target) => target != null && !target.Destroyed && target.def.useHitPoints &&
        target.HitPoints > 0 && target.HitPoints < target.MaxHitPoints &&
        (target.TryGetComp<CompRottable>() is not { } rot || rot.Stage == RotStage.Fresh);

    // Each kind of target has its own effect switch. A switched-off kind is never assigned, so it takes no share of a budget.
    public static bool Supports(CompFerrite fixture, Thing target)
    {
        if (fixture.Props.personal)
            return Effects.FerriteHammerMending.On && fixture.Wearer is { Spawned: true } wearer &&
                (target.ParentHolder is Pawn_ApparelTracker worn && worn.pawn == wearer ||
                 target.ParentHolder is Pawn_EquipmentTracker held && held.pawn == wearer && target.def.IsWeapon);
        if (target is Building) return fixture.Props.buildings && Effects.FerriteBuildings.On && target.Spawned &&
            target.Faction == Faction.OfPlayer && target.def.building.repairable;
        if (target.Spawned) return fixture.Props.looseItems && Effects.FerriteItems.On && target.def.category == ThingCategory.Item;
        if (!fixture.Props.wornEquipment || !Effects.FerriteGear.On) return false;
        return target.ParentHolder is Pawn_ApparelTracker apparel && apparel.pawn.Spawned && OurPawn(apparel.pawn) ||
            target.ParentHolder is Pawn_EquipmentTracker equipment && equipment.pawn.Spawned && OurPawn(equipment.pawn) && target.def.IsWeapon;
    }

    // Whether any switched-on effect could still repair the target, so credits for switched-off kinds are dropped.
    private static bool KindOn(Thing target) => target is Building ? Effects.FerriteBuildings.On
        : target.Spawned ? Effects.FerriteItems.On : Effects.FerriteGear.On || Effects.FerriteHammerMending.On;

    // Colonists, slaves and prisoners. Enemy gear is never repaired.
    public static bool OurPawn(Pawn pawn) => pawn.Faction == Faction.OfPlayer || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony;

    private readonly List<CompFerrite> personal = new();
    private int personalTick = -1;

    // Worn or wielded Ferrite found by walking every pawn's apparel and weapon. Ownership checks read it once per damaged target, so the walk
    // runs at most once a tick.
    private List<CompFerrite> PersonalSources
    {
        get
        {
            int now = Find.TickManager.TicksGame;
            if (now == personalTick) return personal;
            personalTick = now;
            personal.Clear();
            foreach (var pawn in map.mapPawns.AllPawnsSpawned)
            {
                var worn = pawn.apparel?.WornApparel;
                if (worn != null)
                    for (int i = 0; i < worn.Count; i++)
                        if (worn[i].TryGetComp<CompFerrite>() is { Props.personal: true } source) personal.Add(source);
                if (pawn.equipment?.Primary?.TryGetComp<CompFerrite>() is { Props.personal: true } held) personal.Add(held);
            }
            return personal;
        }
    }

    public static IEnumerable<Thing> WornGear(Pawn pawn)
    {
        if (pawn.apparel != null)
            foreach (var apparel in pawn.apparel.WornApparel) yield return apparel;
        if (pawn.equipment != null)
            foreach (var weapon in pawn.equipment.AllEquipmentListForReading)
                if (weapon.def.IsWeapon) yield return weapon;
    }

    public CompFerrite Owner(Thing target)
    {
        CompFerrite best = null;
        foreach (var fixture in fixtures) best = Better(best, fixture, target);
        foreach (var fixture in PersonalSources) best = Better(best, fixture, target);
        return best;
    }

    private CompFerrite Better(CompFerrite best, CompFerrite fixture, Thing target)
    {
        if (!fixture.Active || fixture.Props.repairBudget <= 0 || !Supports(fixture, target) || target.MapHeld != map ||
            target is not Building && !fixture.Props.personal && !fixture.CanPay ||
            !fixture.Props.personal && fixture.parent.Position.DistanceToSquared(target.PositionHeld) > fixture.Props.radius * fixture.Props.radius) return best;
        float rate = fixture.Props.repairBudget / fixture.Props.interval;
        float bestRate = best == null ? 0 : best.Props.repairBudget / best.Props.interval;
        return best == null || rate > bestRate || rate == bestRate && (fixture.Props.tier > best.Props.tier ||
            fixture.Props.tier == best.Props.tier && fixture.parent.thingIDNumber < best.parent.thingIDNumber) ? fixture : best;
    }

    // Only cells in range, taking each thing at its own position so a multi-cell building is counted once.
    private IEnumerable<Thing> ThingsInRadius(CompFerrite fixture)
    {
        var center = fixture.parent.Position;
        float limit = fixture.Props.radius * fixture.Props.radius;
        foreach (var cell in CellRect.CenteredOn(center, Mathf.CeilToInt(fixture.Props.radius)).ClipInsideMap(map))
        {
            if (cell.DistanceToSquared(center) > limit) continue;
            var things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i].Position == cell) yield return things[i];
        }
    }

    public void Pulse(CompFerrite fixture)
    {
        // Assignment includes every source, not only sources whose pulse is due. This
        // prevents duplicate or slower installations from supplying extra service.
        if (!fixture.AnyKindOn) return;
        IEnumerable<Thing> candidates = fixture.Props.personal ? WornGear(fixture.Wearer) : ThingsInRadius(fixture);
        if (fixture.Props.wornEquipment && !fixture.Props.personal && Effects.FerriteGear.On)
            candidates = candidates.Concat(map.mapPawns.AllPawnsSpawned.Where(OurPawn).SelectMany(WornGear));
        var targets = candidates.Where(Damaged).Distinct().Where(t => Owner(t) == fixture).ToList();
        if (targets.Count == 0) return;
        double share = (double)fixture.Props.repairBudget / targets.Count;
        int now = Find.TickManager.TicksGame;
        foreach (var target in targets)
        {
            if (!byTarget.TryGetValue(target, out var credit))
            {
                credit = new FerriteRepairCredit { target = target };
                credits.Add(credit);
                byTarget.Add(target, credit);
            }
            // Keep one cadence per target even when ownership changes between pulses.
            if (now < credit.nextServiceTick) continue;
            credit.nextServiceTick = now + fixture.Props.interval;
            credit.hitPoints += share * target.MaxHitPoints;
            int repair = Math.Min(target.MaxHitPoints - target.HitPoints, (int)Math.Floor(credit.hitPoints));
            if (repair == 0) continue;
            // Items and worn gear cost Solar energy per HP. The credit is kept if the cost cannot be paid.
            if (target is not Building && !fixture.Props.personal && !fixture.TryPay(repair * FerriteSolar.CostPerHp)) continue;
            target.HitPoints += repair;
            credit.hitPoints -= repair;
            if (target.HitPoints == target.MaxHitPoints) credit.hitPoints = 0;
        }
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref credits, "ferriteRepairCredits", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            credits ??= new List<FerriteRepairCredit>();
            credits.RemoveAll(c => c.target == null || c.target.Destroyed);
            byTarget = credits.ToDictionary(c => c.target);
        }
    }

    public override void MapComponentTick()
    {
        // Apparel is held rather than spawned. Discover current wearers here instead of
        // relying on building spawn notifications or an apparel tick implementation.
        // Scan only on the shared pulse tick; this runs for every pawn's apparel.
        int now = Find.TickManager.TicksGame;
        if (now % PersonalPulseCheck == 0 && Effects.FerriteHammerMending.On)
            foreach (var source in PersonalSources)
                if (now % source.Props.interval == 0) Pulse(source);
        if (now % 600 != 0) return;
        credits.RemoveAll(c =>
        {
            bool remove = c.target == null || c.target.Destroyed || c.target.MapHeld != map ||
                c.target.HitPoints >= c.target.MaxHitPoints && now >= c.nextServiceTick || !KindOn(c.target);
            if (remove && c.target != null) byTarget.Remove(c.target);
            return remove;
        });
    }
}

public class CompFerrite : ThingComp
{
    public CompProperties_Ferrite Props => (CompProperties_Ferrite)props;
    public Pawn Wearer => parent is Apparel apparel ? apparel.Wearer : (parent.ParentHolder as Pawn_EquipmentTracker)?.pawn;
    public bool Active => Props.personal ? Wearer?.Spawned == true : parent.Spawned;

    // Whether any of the kinds this source repairs is switched on.
    public bool AnyKindOn => Props.personal ? Effects.FerriteHammerMending.On
        : Props.buildings && Effects.FerriteBuildings.On || Props.looseItems && Effects.FerriteItems.On || Props.wornEquipment && Effects.FerriteGear.On;

    private int payTick = -1;
    private bool payOk;

    // Whether item and gear repair may run this tick. Cached so ownership checks do not re-read the network per target.
    public bool CanPay
    {
        get
        {
            int now = Find.TickManager.TicksGame;
            if (now == payTick) return payOk;
            payTick = now;
            // Without a toggle, repair is free only while Solar energy is unmetered.
            return payOk = parent.GetComp<CompSolarToggle>()?.CanPay(FerriteSolar.CostPerHp) ?? !ActiveEffectKind.Solar.Metered;
        }
    }

    public bool TryPay(float amount) => parent.GetComp<CompSolarToggle>()?.TryPay(amount) ?? true;
    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        if (!Props.personal) MapState.Of<FerriteField>(parent.Map).fixtures.Add(this);
    }
    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        if (!Props.personal) MapState.Of<FerriteField>(map).fixtures.Remove(this);
    }

    public override void CompTick()
    {
        if (Props.personal || !parent.Spawned) return;
        if (parent.IsHashIntervalTick(Props.interval)) MapState.Of<FerriteField>(parent.Map).Pulse(this);
    }

}

public class PlaceWorker_FerriteRange : PlaceWorker
{
    public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null) =>
        GenDraw.DrawRadiusRing(center, def.GetCompProperties<CompProperties_Ferrite>().radius, new Color(0.9f, 0.6f, 0.4f));
}
