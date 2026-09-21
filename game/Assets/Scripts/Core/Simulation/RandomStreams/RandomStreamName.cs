// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.RandomStreams;

/// <summary>Named, independent PRNG streams (spec §11: "seeded PRNG แยก
/// streams"). Every value here gets its own <see cref="DeterministicRandom"/>
/// instance in <see cref="NamedRandomStreams"/> -- draws from one never
/// touch another's state, by construction (separate objects), not by
/// convention.</summary>
public enum RandomStreamName
{
    /// <summary>Vehicle/queue variation (consumed by future G4 mobility
    /// work; used in G3 only for the minimal per-tick draw in
    /// WorldState.SimulateTick, see its doc comment).</summary>
    Traffic,

    /// <summary>Event/incident timing and severity (event system is G4
    /// scope; this stream exists now so seeding/save format do not need
    /// to change shape later).</summary>
    Incidents,

    /// <summary>Household-count and other per-cohort jitter when seeding
    /// SimulationInitialization scenario data from GeographyBase
    /// buildings (see WorldState's cohort seeding).</summary>
    CohortVariation,
}
