using System;
using System.Linq;
using RimWorld;
using Verse;

namespace LumenStones;

public static partial class StoneSettings
{
    // The Implants tab: shared rules, the portable cells, then one section per implant or prosthetic.
    private static void AddImplants()
    {
        const string group = "Implants", shared = "Shared rules";
        var recipes = DefDatabase<RecipeDef>.AllDefsListForReading
            .Where(r => r.products?.Count == 1 && r.products[0].thingDef.GetModExtension<CrystalPartItem>() != null).ToList();
        if (recipes.Count == 0) return;
        foreach (bool prosthetic in new[] { false, true })
        {
            var matching = recipes.Where(r => r.products[0].thingDef.GetModExtension<CrystalPartItem>().prosthetic == prosthetic).ToList();
            if (matching.Count == 0) continue;
            Add(group, shared, prosthetic ? "Prosthetics.stoneCost" : "Implants.stoneCost", (prosthetic ? "Prosthetic" : "Implant") + " stone cost",
                matching[0].ingredients[0].GetBaseCount(), v => matching.ForEach(r => r.ingredients[0].SetBaseCount(v)), min: 1, integer: true);
        }
        Add(group, shared, "Prosthetics.dormantEfficiency", "Dormant prosthetic efficiency (%)", ImplantRules.DormantEfficiency,
            v => ImplantRules.DormantEfficiency = v, max: 5, percent: true);
        Add(group, shared, "Implants.attunementCooldownDays", "Attunement cooldown (days)", ImplantRules.AttunementCooldownDays,
            v => ImplantRules.AttunementCooldownDays = v);
        if (ModsConfig.IdeologyActive)
            Add(group, shared, "Implants.bindingCooldownDays", "Binding cooldown (days)", ImplantRules.BindingCooldownDays,
                v => ImplantRules.BindingCooldownDays = v);
        Add(group, shared, "Implants.drainPerDay", "Solar or Warp energy per active part per day", ImplantRules.DrainPerDay,
            v => ImplantRules.DrainPerDay = v);
        Add(group, shared, "Implants.reliefFactor", "Drawback reduction while energy is paid (%)", ImplantRules.ReliefFactor,
            v => ImplantRules.ReliefFactor = v, max: 1, percent: true);
        Add(group, shared, "Implants.pairedBenefitFactor", "Benefit with both a light and a dark implant (%)", ImplantRules.PairedBenefitFactor,
            v => ImplantRules.PairedBenefitFactor = v, max: 1, percent: true);

        // One capacity for both cells, so a Twinstone cell's Solar pool matches a Sunstone cell's.
        var cells = TwinstoneCell.Defs.Select(d => d.GetCompProperties<CompProperties_Twinstone>()).ToList();
        if (cells.Count > 0)
            Add(group, "Portable cells", "Twinstone.capacity", "Capacity of each pool (Sunstone and Twinstone cells)", cells[0].capacity,
                v => cells.ForEach(c => c.capacity = v), min: 1);

        foreach (var recipe in recipes)
        {
            var item = recipe.products[0].thingDef;
            // A part needs its stone, so it follows that stone's availability toggles.
            var stone = recipe.ingredients[0].FixedIngredient;
            if (stone != null && items.TryGetValue(stone.defName, out var owner)) items.TryAdd(item.defName, (owner.Group, false));
            var hediff = DefDatabase<HediffDef>.AllDefsListForReading.FirstOrDefault(h => h.spawnThingOnRemoved == item);
            var props = hediff?.CompProps<HediffCompProperties_Attunement>();
            if (props == null) continue;
            string section = hediff.LabelCap, key = hediff.defName;
            if (hediff.addedPartProps != null)
                Add(group, section, key + ".efficiency", "Part efficiency (%)", hediff.addedPartProps.partEfficiency,
                    v => hediff.addedPartProps.partEfficiency = v, max: 5, percent: true);
            AddStage(group, section, key + ".benefit", "Benefit", props.benefit);
            if (props.reserve?.GetModExtension<SunlightReserveExtension>() is { } reserve)
            {
                Add(group, section, key + ".hoursToFill", "Sunlight reserve: hours of clear sun to fill", reserve.hoursToFill, v => reserve.hoursToFill = v, min: 0.1f);
                Add(group, section, key + ".daysToDrain", "Sunlight reserve: days without sun to empty", reserve.daysToDrain, v => reserve.daysToDrain = v, min: 0.1f);
                Add(group, section, key + ".startReserve", "Sunlight reserve on waking (%)", reserve.startSeverity, v => reserve.startSeverity = v, max: 1, percent: true);
                Add(group, section, key + ".overlightRate", "Fill rate under Verdite overlight (% of clear sun)", reserve.overlightRate, v => reserve.overlightRate = v, max: 1, percent: true);
                foreach (var stage in props.reserve.stages)
                    Add(group, section, key + ".hunger." + stage.label.Replace(" ", "").Replace("-", ""), "Hunger while " + stage.label + " (%)", stage.hungerRateFactor,
                        v => stage.hungerRateFactor = v, max: 10, percent: true);
            }
            AddStage(group, section, key + ".drawback", "Drawback", props.drawback);
            AddStage(group, section, key + ".drawbackNoBiotech", "Drawback without Biotech", ModsConfig.BiotechActive ? null : props.drawbackWithoutBiotech);
            if (hediff.CompProps<HediffCompProperties_DreadAura>() is { } dread)
                Add(group, section, key + ".dreadRadius", "Dread radius (tiles)", dread.radius, v => dread.radius = v, min: 1, max: 50);
            if (hediff.CompProps<HediffCompProperties_ScarMending>() is { } scars)
                Add(group, section, key + ".daysPerScar", "Days to heal a scar", scars.daysPerScar, v => scars.daysPerScar = v, min: 0.1f);
            if (hediff.CompProps<HediffCompProperties_ApparelMending>() is { } apparel)
                Add(group, section, key + ".apparelRepair", "Apparel hit points repaired per day", apparel.hitPointsPerDay, v => apparel.hitPointsPerDay = v);
            if (hediff.CompProps<HediffCompProperties_PartMending>() is { } mending)
                Add(group, section, key + ".partHealing", "Injury healed on the part per day", mending.healPerDay, v => mending.healPerDay = v);
            var thought = DefDatabase<ThoughtDef>.AllDefsListForReading.FirstOrDefault(t => t.hediff == hediff && t.workerClass == typeof(ThoughtWorker_StoneImplant));
            if (thought != null)
            {
                var stage = thought.stages[0];
                Add(group, section, key + ".mood", "Benefit: mood", stage.baseMoodEffect, v => stage.baseMoodEffect = v, min: -100, max: 100);
            }
        }
    }

    // Lists every effect the stage sets, so each implant's numbers can be tuned without knowing its fields.
    private static void AddStage(string group, string section, string key, string prefix, HediffStage stage)
    {
        if (stage == null) return;
        void Factor(string name, string label, Func<float> get, Action<float> set)
        {
            if (get() != 1f) Add(group, section, key + "." + name, prefix + ": " + label + " (%)", get(), set, max: 10, percent: true);
        }
        Factor("pain", "pain factor", () => stage.painFactor, v => stage.painFactor = v);
        Factor("bleeding", "bleeding factor", () => stage.totalBleedFactor, v => stage.totalBleedFactor = v);
        Factor("hunger", "hunger factor", () => stage.hungerRateFactor, v => stage.hungerRateFactor = v);
        Factor("restFall", "rest fall factor", () => stage.restFallFactor, v => stage.restFallFactor = v);
        Factor("fertility", "fertility factor", () => stage.fertilityFactor, v => stage.fertilityFactor = v);
        Factor("foodPoisoning", "food poisoning chance factor", () => stage.foodPoisoningChanceFactor, v => stage.foodPoisoningChanceFactor = v);
        if (stage.statOffsets != null)
            foreach (var mod in stage.statOffsets)
            {
                bool percent = mod.stat.toStringStyle.ToString().StartsWith("Percent");
                Add(group, section, key + ".offset." + mod.stat.defName, prefix + ": " + mod.stat.label + " offset" + (percent ? " (%)" : ""), mod.value,
                    v => mod.value = v, min: -1000, max: 1000, percent: percent);
            }
        if (stage.statFactors != null)
            foreach (var mod in stage.statFactors)
                Add(group, section, key + ".factor." + mod.stat.defName, prefix + ": " + mod.stat.label + " factor (%)", mod.value, v => mod.value = v, max: 10, percent: true);
        foreach (var mod in stage.capMods)
        {
            if (mod.offset != 0f)
                Add(group, section, key + ".cap." + mod.capacity.defName, prefix + ": " + mod.capacity.label + " offset (%)", mod.offset,
                    v => mod.offset = v, min: -1, max: 1, percent: true);
            if (mod.postFactor != 1f)
                Add(group, section, key + ".capFactor." + mod.capacity.defName, prefix + ": " + mod.capacity.label + " factor (%)", mod.postFactor,
                    v => mod.postFactor = v, max: 10, percent: true);
        }
    }
}
