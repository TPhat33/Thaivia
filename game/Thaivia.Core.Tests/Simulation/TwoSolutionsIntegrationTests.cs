using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.Planning;
using Xunit;
using Xunit.Abstractions;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// G3 gate (plan §16: "อย่างน้อยสองวิธีแก้"): starting from the fixture's
/// seeded accessibility problem (the residential cohort's home building
/// sits on a road cluster with NO network path at all to the only
/// job-bearing building -- see SimulationFixtures), demonstrates two
/// GENUINELY DIFFERENT command sequences that each measurably improve the
/// residential building's accessibility score, without tuning either
/// mechanism to make this trivially true: both use the same
/// AccessibilityGraph/AccessibilityNeed machinery every other test in
/// this suite exercises, and the improvement is measured via
/// WorldState.ComputeAccessibilityScore before/after, printed to test
/// output, never merely asserted "> 0" without a number attached.
/// </summary>
public class TwoSolutionsIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public TwoSolutionsIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SeededProblem_IsActuallyUnsolvedAtBaseline()
    {
        var world = SimulationFixtures.BuildWorldState();
        var baseline = world.ComputeAccessibilityScore(SimulationFixtures.ResidentialBuildingId);
        _output.WriteLine($"Baseline accessibility score: {baseline}");
        Assert.Equal(0, baseline); // confirms the scenario is genuinely a problem, not already solved.
    }

    [Fact]
    public void Solution1_RelocatingTheResidentialBuilding_MeasurablyImprovesAccessibility()
    {
        var world = SimulationFixtures.BuildWorldState(masterSeed: 111);
        var before = world.ComputeAccessibilityScore(SimulationFixtures.ResidentialBuildingId);

        var impact = PlanningEngine.EstimateRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        var draft = new BuildingRelocationDraft("solution1-relocate", world.Revision, 200_000, impact,
            SimulationFixtures.ResidentialBuildingId, destLocalX: 22, destLocalZ: 3, destNearestRoadNodeId: SimulationFixtures.Node4);
        var commit = PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, new long[] { 200_000 });
        Assert.True(commit.Success, string.Join("; ", commit.Failures));

        var after = world.ComputeAccessibilityScore(SimulationFixtures.ResidentialBuildingId);
        _output.WriteLine($"Solution 1 (relocate residential building into the job cluster): {before} -> {after} (delta={after - before})");

        Assert.True(after > before, $"expected improvement: before={before}, after={after}");
        Assert.True(after - before >= 20, $"expected a substantial, not marginal, improvement; measured delta={after - before}");
    }

    [Fact]
    public void Solution2_BuildingANewRoadConnector_MeasurablyImprovesAccessibility_WithoutMovingAnyBuilding()
    {
        var world = SimulationFixtures.BuildWorldState(masterSeed: 222);
        var before = world.ComputeAccessibilityScore(SimulationFixtures.ResidentialBuildingId);
        var buildingLocationBefore = world.BuildingStates[SimulationFixtures.ResidentialBuildingId];

        var impact = PlanningEngine.EstimateNewRoadAccessImpact(world, SimulationFixtures.Node2, SimulationFixtures.Node3, SimulationFixtures.ResidentialBuildingId);
        var draft = new NewRoadConnectorDraft("solution2-bridge", world.Revision, 100_000, impact, SimulationFixtures.Node2, SimulationFixtures.Node3);
        var commit = PlanningEngine.CommitNewRoadConnector(world, draft, LedgerAccountKind.Capex, new long[] { 100_000 });
        Assert.True(commit.Success, string.Join("; ", commit.Failures));

        var after = world.ComputeAccessibilityScore(SimulationFixtures.ResidentialBuildingId);
        var buildingLocationAfter = world.BuildingStates[SimulationFixtures.ResidentialBuildingId];
        _output.WriteLine($"Solution 2 (bridge the canal with a new road connector): {before} -> {after} (delta={after - before})");

        Assert.True(after > before, $"expected improvement: before={before}, after={after}");
        Assert.True(after - before >= 20, $"expected a substantial, not marginal, improvement; measured delta={after - before}");

        // Genuinely a DIFFERENT mechanism: the building never moved.
        Assert.Equal(buildingLocationBefore.LocalX, buildingLocationAfter.LocalX);
        Assert.Equal(buildingLocationBefore.LocalZ, buildingLocationAfter.LocalZ);
        Assert.False(buildingLocationAfter.Relocated);
    }

    [Fact]
    public void BothSolutions_AreMechanicallyDistinct_NotTheSameCommandRenamed()
    {
        var relocationWorld = SimulationFixtures.BuildWorldState(masterSeed: 333);
        var roadWorld = SimulationFixtures.BuildWorldState(masterSeed: 333);

        var relocImpact = PlanningEngine.EstimateRelocationAccessImpact(relocationWorld, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        var relocDraft = new BuildingRelocationDraft("cmp-reloc", relocationWorld.Revision, 200_000, relocImpact,
            SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        PlanningEngine.CommitRelocation(relocationWorld, relocDraft, LedgerAccountKind.Capex, new long[] { 200_000 });

        var roadImpact = PlanningEngine.EstimateNewRoadAccessImpact(roadWorld, SimulationFixtures.Node2, SimulationFixtures.Node3, SimulationFixtures.ResidentialBuildingId);
        var roadDraft = new NewRoadConnectorDraft("cmp-road", roadWorld.Revision, 100_000, roadImpact, SimulationFixtures.Node2, SimulationFixtures.Node3);
        PlanningEngine.CommitNewRoadConnector(roadWorld, roadDraft, LedgerAccountKind.Capex, new long[] { 100_000 });

        // Different kinds of committed project, different affected state.
        Assert.Equal(Thaivia.Core.Simulation.Planning.ProjectKind.BuildingRelocation, relocationWorld.Projects["cmp-reloc"].Kind);
        Assert.Equal(Thaivia.Core.Simulation.Planning.ProjectKind.NewRoadConnector, roadWorld.Projects["cmp-road"].Kind);
        Assert.True(relocationWorld.BuildingStates[SimulationFixtures.ResidentialBuildingId].Relocated);
        Assert.False(roadWorld.BuildingStates[SimulationFixtures.ResidentialBuildingId].Relocated);
        Assert.Empty(relocationWorld.PlannedRoadSegments);
        Assert.Single(roadWorld.PlannedRoadSegments);

        _output.WriteLine($"Relocation world accessibility: {relocationWorld.ComputeAccessibilityScore(SimulationFixtures.ResidentialBuildingId)}");
        _output.WriteLine($"New-road world accessibility: {roadWorld.ComputeAccessibilityScore(SimulationFixtures.ResidentialBuildingId)}");
    }
}
