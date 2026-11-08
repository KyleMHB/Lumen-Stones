using System;
using System.Collections.Generic;

namespace LumenStones;

// Pure rules for weighted distinct fixture categories. No game types, so a test project can compile this file alone.
// The enum order is the order missing categories are listed in.
public enum FixtureCategory { None = -1, Column, Lamp, Pylon, Cluster }

public enum SupportKind { Room, Ritual }

public readonly record struct Treatment(float Healing, float Immunity, float InfectionPrevention);

// Each category counts once, at its highest weight, so duplicate fixtures never add support and the result is order independent.
public sealed class CategoryWeights
{
    public const int Count = 4;
    private readonly float[] best = new float[Count];

    public void Raise(FixtureCategory category, float weight)
    {
        if (category != FixtureCategory.None) best[(int)category] = Math.Max(best[(int)category], weight);
    }

    public void Clear() => Array.Clear(best);

    public float this[FixtureCategory category] => category == FixtureCategory.None ? 0 : best[(int)category];

    // Double accumulation, so the total does not depend on summing order.
    public float Total
    {
        get
        {
            double total = 0;
            for (int i = 0; i < Count; i++) total += best[i];
            return (float)total;
        }
    }
}

public static class SupportCore
{
    public static float EnclosureFactor(SupportKind kind, bool enclosed) => enclosed ? 1f : kind == SupportKind.Ritual ? 0.5f : 0f;

    // Clamp first, then scale, so an overflowing ritual total is half of 100%, not half of the raw sum.
    public static float Fraction(SupportKind kind, CategoryWeights weights, bool enclosed) =>
        Math.Clamp(weights.Total, 0f, 1f) * EnclosureFactor(kind, enclosed);

    // Active effects read the running-fixtures set only while the energy network is metered.
    public static bool UseActiveSet(bool activeOnly, bool metered) => activeOnly && metered;

    public static List<FixtureCategory> Missing(CategoryWeights weights, List<FixtureCategory> into = null)
    {
        into ??= new List<FixtureCategory>();
        into.Clear();
        for (var c = FixtureCategory.Column; c <= FixtureCategory.Cluster; c++)
            if (weights[c] <= 0) into.Add(c);
        return into;
    }

    // The one Vitalite occupant formula. Room fixtures scale the caps by their support fractions; a bed adds its own bonuses,
    // and its healing and infection prevention only while its active effect is permitted. Infection prevention needs an
    // enclosed room, and combines the bed and the room multiplicatively.
    public static Treatment Vitalite(float passive, float active, bool enclosed, float bedHealing, float bedImmunity, float bedInfection,
        bool bedActive, float healingCap, float immunityCap, float infectionCap)
    {
        float healing = active * healingCap + (bedActive ? bedHealing : 0);
        float immunity = passive * immunityCap + bedImmunity;
        float infection = !enclosed ? 0 : 1 - (1 - (bedActive ? Math.Clamp(bedInfection, 0f, 1f) : 0)) * (1 - active * infectionCap);
        return new Treatment(healing, immunity, infection);
    }
}
