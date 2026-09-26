// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Cohorts;

/// <summary>
/// Turns a noise index, an accessibility score, and a safety score into a
/// <see cref="CohortNeeds"/> reading. G3 originally left `Safety` a flat
/// baseline because no incident/night-disorder system existed yet (see
/// git history for that wave's honest note); G4 added a real incident
/// system (<see cref="Mobility.Incidents"/>), and
/// <see cref="WorldState.ComputeSafetyScore"/> now derives a live safety
/// number from currently-Active incident sites reachable from a cohort's
/// home -- the 4-argument overload below is what a caller with that real
/// number passes. The 3-argument overload keeps
/// <see cref="BaselineSafety"/> as an explicit, documented fallback for
/// scenarios that have not seeded any incident sites at all (never a
/// number presented as measured).
/// </summary>
public static class CohortNeedsCalculator
{
    public const int BaselineSafety = 70;

    /// <summary>Documented baseline for <see cref="CohortNeeds.Utilities"/>
    /// (G6-06), mirroring <see cref="BaselineSafety"/>'s precedent: used
    /// by every overload below that is not given a real utility-coverage
    /// score, i.e. "not modeled for this call site", never a claim that
    /// coverage is actually good.</summary>
    public const int BaselineUtilitiesCoverage = 100;

    public static CohortNeeds Compute(int noiseIndex, int accessibilityScore, int nearbyJobsReachable, int safety, int utilitiesCoverage)
    {
        var sleep = Clamp(100 - noiseIndex);
        var access = Clamp(accessibilityScore);
        var economy = Clamp(nearbyJobsReachable * 5);
        return new CohortNeeds(sleep, access, Clamp(safety), economy, Clamp(utilitiesCoverage));
    }

    public static CohortNeeds Compute(int noiseIndex, int accessibilityScore, int nearbyJobsReachable, int safety) =>
        Compute(noiseIndex, accessibilityScore, nearbyJobsReachable, safety, BaselineUtilitiesCoverage);

    public static CohortNeeds Compute(int noiseIndex, int accessibilityScore, int nearbyJobsReachable) =>
        Compute(noiseIndex, accessibilityScore, nearbyJobsReachable, BaselineSafety, BaselineUtilitiesCoverage);

    private static int Clamp(int value) => Math.Clamp(value, 0, 100);
}
