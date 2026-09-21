// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Cohorts;

/// <summary>
/// Turns a noise index and an accessibility score into a
/// <see cref="CohortNeeds"/> reading. `Safety` is a flat baseline this
/// wave: G3 does not model incidents/night-disorder yet (that is G4
/// scope, see IMPLEMENTATION_PLAN.th.md §16 gate G4) -- deliberately
/// documented as deferred here rather than silently wired to a fake
/// number that looks measured.
/// </summary>
public static class CohortNeedsCalculator
{
    public const int BaselineSafety = 70;

    public static CohortNeeds Compute(int noiseIndex, int accessibilityScore, int nearbyJobsReachable)
    {
        var sleep = Clamp(100 - noiseIndex);
        var access = Clamp(accessibilityScore);
        var economy = Clamp(nearbyJobsReachable * 5);
        return new CohortNeeds(sleep, access, BaselineSafety, economy);
    }

    private static int Clamp(int value) => Math.Clamp(value, 0, 100);
}
