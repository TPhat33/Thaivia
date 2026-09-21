// Pure C# -- no UnityEngine reference. See game/README.md.
using Thaivia.Core.Simulation.Cohorts;

namespace Thaivia.Core.Simulation.Reporting;

/// <summary>One cohort's needs before and after whatever player projects
/// have been committed to the "current" world -- see
/// <see cref="BeforeAfterComparison"/> for how "before" is obtained. Every
/// delta is a plain subtraction over <see cref="Cohorts.CohortNeeds"/>'s
/// existing 0-100 fields; this type invents no new metric of its own.</summary>
public readonly record struct CohortNeedsComparison(string CohortId, CohortNeeds Baseline, CohortNeeds Current)
{
    public int SleepDelta => Current.Sleep - Baseline.Sleep;
    public int AccessDelta => Current.Access - Baseline.Access;
    public int SafetyDelta => Current.Safety - Baseline.Safety;
    public int EconomyDelta => Current.Economy - Baseline.Economy;
}

/// <summary>
/// Makes "before vs after" a first-class, always-available operation
/// (spec §12/§19: the base map and baseline metrics must remain queryable
/// after any number of player projects) rather than something only
/// possible if a caller happened to snapshot metrics before the first
/// project was committed.
///
/// The key fact this relies on (already enforced elsewhere -- see
/// ImmutabilityTests, LayerSeparationTests,
/// PlanningEngineTests.Relocation_ConservesPopulationAndJobs_AndDoesNotMutateGeographyBase):
/// EVERY <see cref="WorldState"/> is built from
/// (<see cref="WorldState.GeographyBase"/>,
/// <see cref="WorldState.SimulationInitialization"/>,
/// <see cref="WorldState.RoadGraph"/>, <see cref="WorldState.Scenario"/>),
/// none of which a WorldState instance -- or any project committed
/// through it -- ever mutates. <see cref="RebuildBaseline"/> therefore
/// always produces a fresh, zero-project world seeded identically to how
/// `current` itself was originally seeded (same master seed -> same
/// deterministic archetype/household draws, see
/// WorldState.SeedFromGeography), no matter how many ticks have run or
/// projects have been committed to `current` in the meantime. This is
/// what makes a before/after VIEW possible at any point in a session, not
/// only once, right after session start.
/// </summary>
public static class BeforeAfterComparison
{
    /// <summary>Rebuilds the zero-project baseline world `current` would
    /// have started from -- same GeographyBase/SimulationInitialization/
    /// RoadGraph/ScenarioConfig REFERENCES (never a copy, so
    /// <c>ReferenceEquals(baseline.GeographyBase, current.GeographyBase)</c>
    /// always holds -- see BeforeAfterComparisonTests), fresh Clock/RNG/
    /// ledger/cohorts/buildings/mobility state. Pure: never mutates
    /// `current` or any of the four references it reads.</summary>
    public static WorldState RebuildBaseline(WorldState current) =>
        new(current.GeographyBase, current.SimulationInitialization, current.RoadGraph, current.Scenario);

    public static CohortNeedsComparison CompareCohortNeeds(WorldState current, WorldState baseline, string cohortId, int hourOfDay) =>
        new(cohortId, baseline.ComputeCohortNeeds(cohortId, hourOfDay), current.ComputeCohortNeeds(cohortId, hourOfDay));

    public static int CompareAccessibilityScore(WorldState current, WorldState baseline, long buildingSourceId, out int baselineScore, out int currentScore)
    {
        baselineScore = baseline.ComputeAccessibilityScore(buildingSourceId);
        currentScore = current.ComputeAccessibilityScore(buildingSourceId);
        return currentScore - baselineScore;
    }
}
