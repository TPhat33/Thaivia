// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Cohorts;

/// <summary>A cohort's four needs (spec §11: "sleep / access / safety /
/// economy, driven by the activity clock"), each 0-100. Simulation-layer
/// numbers by construction, never a source fact.</summary>
public readonly record struct CohortNeeds(int Sleep, int Access, int Safety, int Economy);
