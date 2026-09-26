// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Utilities;

/// <summary>
/// Turns a NETWORK distance to the nearest same-kind
/// <see cref="UtilitySource"/> (never a straight-line radius -- plan
/// §11 is explicit that a radius lets service "cross a canal"; see
/// <see cref="Accessibility.AccessibilityGraph"/>'s identical discipline,
/// which this type's caller reuses directly) plus that source's current
/// load-vs-capacity into a single 0-100 coverage score, exactly the way
/// <see cref="Accessibility.AccessibilityNeed"/> turns a network distance
/// into an access score -- with an added capacity term
/// <see cref="Accessibility.AccessibilityNeed"/> does not need (a job
/// building does not have a per-tick "vehicle discharge limit"; a utility
/// source does).
/// </summary>
public static class UtilityCoverage
{
    /// <summary>The network distance treated as "fully in reach" before
    /// any capacity penalty (score 100 at distance 0, degrading to 0 at
    /// this distance and beyond) -- a documented game-design constant for
    /// utility infrastructure reach, deliberately a SEPARATE number from
    /// <see cref="Accessibility.AccessibilityNeed.ReferenceDistanceMeters"/>
    /// (a resident's own walking-access reference) since a pipe/cable
    /// network's practical reach is a different real-world quantity from
    /// a walkable distance -- never measured, never presented as a source
    /// fact.</summary>
    public const double ReferenceDistanceMeters = 400.0;

    /// <summary>Reach-only score (no capacity term), mirroring
    /// <see cref="Accessibility.AccessibilityNeed.ComputeScore"/> exactly:
    /// null (unreachable through the network, OR no source of this kind
    /// exists at all -- both are "no data", never "assumed absent" as a
    /// distinct case, since this type has no way to distinguish them and
    /// does not pretend to) scores 0, the worst possible score, never
    /// treated as fine.</summary>
    public static int ComputeReachScore(double? networkDistanceMeters)
    {
        if (networkDistanceMeters is null)
        {
            return 0;
        }

        var score = 100.0 * Math.Max(0.0, 1.0 - networkDistanceMeters.Value / ReferenceDistanceMeters);
        return (int)Math.Clamp(Math.Round(score), 0, 100);
    }

    /// <summary>Fraction of a source's capacity consumed this tick.
    /// <paramref name="capacityUnitsPerTick"/> is never treated as
    /// infinite: a capacity of 0 with any demand at all is treated as
    /// maximally over capacity (never as "free"/"no limit").</summary>
    public static double UtilizationFraction(long demandUnitsThisTick, int capacityUnitsPerTick)
    {
        if (demandUnitsThisTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(demandUnitsThisTick));
        }

        if (capacityUnitsPerTick <= 0)
        {
            return demandUnitsThisTick > 0 ? double.PositiveInfinity : 0.0;
        }

        return (double)demandUnitsThisTick / capacityUnitsPerTick;
    }

    public static bool IsExhausted(long demandUnitsThisTick, int capacityUnitsPerTick) =>
        demandUnitsThisTick > capacityUnitsPerTick;

    /// <summary>The combined score <see cref="Simulation.WorldState.ComputeUtilityCoverageScore"/>
    /// reports: the reach score, degraded further once the nearest
    /// source is over capacity (never improved by spare capacity beyond
    /// 100 -- reach is still the ceiling). A source at or under capacity
    /// applies no penalty at all -- exhaustion only ever pulls the score
    /// down, exactly like <see cref="Mobility.Queues.LinkQueueSimulator"/>'s
    /// backlog only ever grows once arrivals exceed capacity, never
    /// "helps" while under it.</summary>
    public static int ComputeScore(double? networkDistanceMeters, long demandUnitsThisTick, int capacityUnitsPerTick)
    {
        var reach = ComputeReachScore(networkDistanceMeters);
        if (reach == 0)
        {
            return 0;
        }

        var utilization = UtilizationFraction(demandUnitsThisTick, capacityUnitsPerTick);
        if (utilization <= 1.0)
        {
            return reach;
        }

        if (double.IsPositiveInfinity(utilization))
        {
            return 0;
        }

        var penalized = reach / utilization;
        return (int)Math.Clamp(Math.Round(penalized), 0, 100);
    }
}
