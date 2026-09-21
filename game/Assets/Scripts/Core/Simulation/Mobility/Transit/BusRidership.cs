// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;
using Thaivia.Core.Simulation.Cohorts;

namespace Thaivia.Core.Simulation.Mobility.Transit;

/// <summary>
/// Draws a bus route's ridership from cohort POPULATION BATCHES (spec §11:
/// "ประชากรเป็น household/cohort records; รถ/คนที่วาดเป็น sample ไม่ใช่ตัว
/// กำหนดประชากรจริง") -- never by spawning one rider object per person.
/// Demand is a fraction of a cohort's total population, and boarding is
/// capped by the route's real fleet capacity from
/// <see cref="BusRouteScheduler.ThroughputPerTick"/>; anything over
/// capacity is reported as overflow, never silently absorbed.
/// </summary>
public static class BusRidership
{
    /// <summary>Fraction of an eligible cohort's population assumed to
    /// consider this route for one tick's trip generation -- a documented
    /// simulation_assumption scenario number, not measured ridership
    /// data.</summary>
    public const double DefaultModeShare = 0.05;

    /// <summary>Total batch demand this tick from every cohort the caller
    /// judges to be within walking reach of a stop on this route.
    /// <paramref name="isWithinWalkingReachOfAStop"/> is caller-supplied
    /// (typically backed by a Walk-mode <see cref="Routing.MobilityGraph"/>
    /// distance check against the route's stops) so this type stays a pure
    /// aggregation over cohorts, not a second copy of walking-routing
    /// logic.</summary>
    public static long ComputeEligibleDemandThisTick(
        IEnumerable<HouseholdCohort> cohorts,
        Func<HouseholdCohort, bool> isWithinWalkingReachOfAStop,
        double modeShare = DefaultModeShare)
    {
        long total = 0;
        foreach (var cohort in cohorts)
        {
            if (isWithinWalkingReachOfAStop(cohort))
            {
                total += (long)(cohort.PopulationCount * modeShare);
            }
        }

        return total;
    }

    /// <summary>Boards as many of <paramref name="demandThisTick"/> as the
    /// route's <paramref name="throughputPerTick"/> allows; the rest is
    /// overflow (never vanishes from the caller's accounting -- a caller
    /// wiring this into trip demand should route the overflow onward, e.g.
    /// to Vehicle/Walk demand, not discard it).</summary>
    public static (long Boarded, long Overflow) AssignRidership(long demandThisTick, double throughputPerTick)
    {
        if (demandThisTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(demandThisTick));
        }

        var capacityThisTick = (long)Math.Floor(throughputPerTick);
        var boarded = Math.Min(demandThisTick, capacityThisTick);
        return (boarded, demandThisTick - boarded);
    }
}
