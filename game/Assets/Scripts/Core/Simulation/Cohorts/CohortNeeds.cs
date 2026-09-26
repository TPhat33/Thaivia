// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Cohorts;

/// <summary>A cohort's needs (spec §11: "sleep / access / safety /
/// economy, driven by the activity clock"), each 0-100. Simulation-layer
/// numbers by construction, never a source fact.
///
/// <see cref="Utilities"/> (G6-06) defaults to 100 ("fully covered") for
/// every existing call site that does not compute a real utility-network
/// score -- the same documented-baseline discipline
/// <see cref="CohortNeedsCalculator.BaselineSafety"/> already established
/// for Safety before G4's incident system existed, not a claim that
/// utilities are actually modeled for a cohort unless a caller passes a
/// real score from <see cref="WorldState.ComputeUtilityCoverageScore"/>.</summary>
public readonly record struct CohortNeeds(int Sleep, int Access, int Safety, int Economy, int Utilities = 100);
