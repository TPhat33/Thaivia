using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Archetypes;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>Control test for SimulationFixtures' claims about which
/// archetype each fixture building id deterministically hashes to -- if
/// this ever fails, every other G3 test that assumes "1000 is
/// Residential" / "2003 is Office (has jobs)" needs re-checking, so it is
/// asserted explicitly here rather than only implicitly relied on.</summary>
public class SimulationFixturesTests
{
    [Fact]
    public void FixtureBuildingIds_HashToTheArchetypesTheFixtureAssumes()
    {
        Assert.Equal(BuildingArchetype.Residential, WorldState.AssignArchetype(SimulationFixtures.ResidentialBuildingId));
        Assert.NotEqual(BuildingArchetype.Residential, WorldState.AssignArchetype(SimulationFixtures.OfficeBuildingId));
    }

    [Fact]
    public void BuildWorldState_SeedsExactlyOneCohortForTheResidentialBuilding()
    {
        var world = SimulationFixtures.BuildWorldState();

        Assert.Single(world.Cohorts);
        var cohort = world.Cohorts["cohort-" + SimulationFixtures.ResidentialBuildingId];
        Assert.Equal(SimulationFixtures.ResidentialBuildingId, cohort.HomeBuildingSourceId);
        Assert.True(cohort.HouseholdCount >= 2 && cohort.HouseholdCount <= 12);

        var office = world.BuildingStates[SimulationFixtures.OfficeBuildingId];
        Assert.True(office.JobsCount > 0);
        var residential = world.BuildingStates[SimulationFixtures.ResidentialBuildingId];
        Assert.Equal(0, residential.JobsCount);
    }
}
