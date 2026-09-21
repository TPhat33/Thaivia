// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Scenario;

/// <summary>
/// The "scenario config" spec §7/AGENTS.md rule 3 requires alongside a
/// MapPack's `simulation_initialization` section to seed
/// households/jobs/activity -- SimulationInitialization-layer data,
/// entirely fictional by construction, never presented as real Thai
/// statistics. The G1/G2 pipeline still bakes an empty
/// `simulation_initialization.seed` ({}) into every MapPack (see
/// map_pipeline.pipeline.mappack.run_pipeline); this type's defaults are
/// therefore what actually seeds G3 gameplay this wave. A populated
/// `seed` JSON element from a future pipeline version could override
/// these defaults, but no such producer exists yet -- stated here
/// honestly rather than pretended (see docs/progress.md Session 4).
/// </summary>
public sealed class ScenarioConfig
{
    public ScenarioConfig(
        long masterSeed,
        long initialCashThb,
        int minHouseholdsPerResidentialBuilding = 2,
        int maxHouseholdsPerResidentialBuilding = 12,
        int peoplePerHousehold = 3,
        int jobsPerNonResidentialBuilding = 4)
    {
        if (initialCashThb < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCashThb));
        }

        if (minHouseholdsPerResidentialBuilding < 0 || maxHouseholdsPerResidentialBuilding < minHouseholdsPerResidentialBuilding)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHouseholdsPerResidentialBuilding));
        }

        if (peoplePerHousehold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(peoplePerHousehold));
        }

        if (jobsPerNonResidentialBuilding < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(jobsPerNonResidentialBuilding));
        }

        MasterSeed = masterSeed;
        InitialCashThb = initialCashThb;
        MinHouseholdsPerResidentialBuilding = minHouseholdsPerResidentialBuilding;
        MaxHouseholdsPerResidentialBuilding = maxHouseholdsPerResidentialBuilding;
        PeoplePerHousehold = peoplePerHousehold;
        JobsPerNonResidentialBuilding = jobsPerNonResidentialBuilding;
    }

    public long MasterSeed { get; }
    public long InitialCashThb { get; }
    public int MinHouseholdsPerResidentialBuilding { get; }
    public int MaxHouseholdsPerResidentialBuilding { get; }
    public int PeoplePerHousehold { get; }
    public int JobsPerNonResidentialBuilding { get; }

    public static ScenarioConfig Default(long masterSeed) => new(masterSeed, initialCashThb: 5_000_000);
}
