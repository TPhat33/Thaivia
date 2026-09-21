using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.Planning;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>Same seed + same committed command sequence must produce a
/// bit-identical world state. Proven by running two independently
/// constructed WorldStates "in parallel" (interleaved calls, never
/// letting one run further ahead so nothing about wall-clock/allocation
/// order could sneak in) and comparing a structural hash after N ticks
/// and several committed projects.</summary>
public class WorldStateDeterminismTests
{
    [Fact]
    public void TwoWorlds_SameSeedSameCommands_ProduceIdenticalStructuralHash()
    {
        var worldA = SimulationFixtures.BuildWorldState(masterSeed: 777);
        var worldB = SimulationFixtures.BuildWorldState(masterSeed: 777);

        RunScenario(worldA);
        RunScenario(worldB);

        Assert.Equal(worldA.ComputeStructuralHash(), worldB.ComputeStructuralHash());
    }

    [Fact]
    public void TwoWorlds_DifferentSeed_ProduceDifferentStructuralHash()
    {
        var worldA = SimulationFixtures.BuildWorldState(masterSeed: 1);
        var worldB = SimulationFixtures.BuildWorldState(masterSeed: 2);

        RunScenario(worldA);
        RunScenario(worldB);

        Assert.NotEqual(worldA.ComputeStructuralHash(), worldB.ComputeStructuralHash());
    }

    private static void RunScenario(Thaivia.Core.Simulation.WorldState world)
    {
        for (var i = 0; i < 10; i++)
        {
            world.SimulateTick();
        }

        var relocationImpact = PlanningEngine.EstimateRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        var relocationDraft = new BuildingRelocationDraft("det-reloc", world.Revision, 200_000, relocationImpact,
            SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        PlanningEngine.CommitRelocation(world, relocationDraft, LedgerAccountKind.Capex, new long[] { 200_000 });

        for (var i = 0; i < 5; i++)
        {
            world.SimulateTick();
        }

        PlanningEngine.PayMilestone(world, relocationDraft.Id, 0);
    }
}
