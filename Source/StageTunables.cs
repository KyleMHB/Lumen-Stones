using System;
using RimWorld;
using Verse;

namespace LumenStones;

// Settings rows for the numbers a hediff stage or a thought carries in XML, so effects defined there can be tuned without
// knowing their fields. Used by implant parts and by the stones' own hediffs and thoughts.
public static class StageTunables
{
    // Every effect the stage sets, keyed "<key>.<field>".
    public static void Declare(TunableScope part, string key, string prefix, HediffStage stage)
    {
        if (stage == null) return;
        void Factor(string name, string label, Func<float> get, Action<float> set)
        {
            if (get() != 1f) part.Add(key + "." + name, "Lumen_Stage_Percent".Translate(prefix, label.Translate()), get(), set, Limit.Percent(10));
        }
        void Offset(string name, string label, Func<float> get, Action<float> set)
        {
            if (get() != 0f) part.Add(key + "." + name, "Lumen_Stage_Percent".Translate(prefix, label.Translate()), get(), set, Limit.Percent(10, -10));
        }
        Factor("pain", "Lumen_Stage_PainFactor", () => stage.painFactor, v => stage.painFactor = v);
        Offset("painOffset", "Lumen_Stage_PainOffset", () => stage.painOffset, v => stage.painOffset = v);
        Factor("bleeding", "Lumen_Stage_BleedingFactor", () => stage.totalBleedFactor, v => stage.totalBleedFactor = v);
        Factor("hunger", "Lumen_Stage_HungerFactor", () => stage.hungerRateFactor, v => stage.hungerRateFactor = v);
        Offset("hungerOffset", "Lumen_Stage_HungerOffset", () => stage.hungerRateFactorOffset, v => stage.hungerRateFactorOffset = v);
        Factor("restFall", "Lumen_Stage_RestFallFactor", () => stage.restFallFactor, v => stage.restFallFactor = v);
        Factor("fertility", "Lumen_Stage_FertilityFactor", () => stage.fertilityFactor, v => stage.fertilityFactor = v);
        Factor("foodPoisoning", "Lumen_Stage_FoodPoisoningFactor", () => stage.foodPoisoningChanceFactor, v => stage.foodPoisoningChanceFactor = v);
        if (stage.statOffsets != null)
            foreach (var mod in stage.statOffsets)
            {
                bool percent = mod.stat.toStringStyle.ToString().StartsWith("Percent");
                part.Add(key + ".offset." + mod.stat.defName, (percent ? "Lumen_Stage_OffsetPercent" : "Lumen_Stage_Offset").Translate(prefix, mod.stat.label), mod.value,
                    v => mod.value = v, percent ? Limit.Percent(1000, -1000) : Limit.Number(-1000, 1000));
            }
        if (stage.statFactors != null)
            foreach (var mod in stage.statFactors)
                part.Add(key + ".factor." + mod.stat.defName, "Lumen_Stage_FactorPercent".Translate(prefix, mod.stat.label), mod.value, v => mod.value = v, Limit.Percent(10));
        if (stage.capMods == null) return;
        foreach (var mod in stage.capMods)
        {
            if (mod.offset != 0f)
                part.Add(key + ".cap." + mod.capacity.defName, "Lumen_Stage_OffsetPercent".Translate(prefix, mod.capacity.label), mod.offset,
                    v => mod.offset = v, Limit.Percent(1, -1));
            if (mod.postFactor != 1f)
                part.Add(key + ".capFactor." + mod.capacity.defName, "Lumen_Stage_FactorPercent".Translate(prefix, mod.capacity.label), mod.postFactor,
                    v => mod.postFactor = v, Limit.Percent(10));
        }
    }

    // One row per effect of each stage, keyed "<HediffDef>.<stageIndex>.<field>", in a section named after the hediff.
    public static void Hediff(TunableScope s, string defName)
    {
        var def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
        if (def?.stages == null) return;
        var section = s.In(def.LabelCap).PerItem();
        for (int i = 0; i < def.stages.Count; i++)
        {
            var stage = def.stages[i];
            string name = stage.label.NullOrEmpty() ? def.stages.Count > 1 ? "Lumen_Stage_Number".Translate(i + 1).ToString() : def.LabelCap.ToString() : stage.label.CapitalizeFirst();
            Declare(section, defName + "." + i, name, stage);
        }
    }

    // Each stage's mood, keyed "<ThoughtDef>.mood.<stageIndex>", and a memory's duration as "<ThoughtDef>.days".
    public static void Thought(TunableScope s, string defName)
    {
        var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(defName);
        if (def?.stages == null) return;
        var rows = s.In("Lumen_Section_Moods".Translate()).PerItem();
        for (int i = 0; i < def.stages.Count; i++)
        {
            var stage = def.stages[i];
            if (stage == null) continue;
            string label = (stage.label ?? def.label).CapitalizeFirst();
            rows.Add(defName + ".mood." + i, "Lumen_Stage_Mood".Translate(label), stage.baseMoodEffect, v => stage.baseMoodEffect = v, Limit.Number(-100, 100));
        }
        if (def.durationDays > 0)
            rows.Add(defName + ".days", "Lumen_Stage_MemoryDays".Translate(def.label), def.durationDays, v => def.durationDays = v, Limit.Number(0.01f, 1000));
    }
}
