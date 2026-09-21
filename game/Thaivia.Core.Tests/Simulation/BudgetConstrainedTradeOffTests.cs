using System.Collections.Generic;
using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.Planning;
using Thaivia.Core.Simulation.Scenario;
using Xunit;
using Xunit.Abstractions;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// A GENUINE trade-off scenario (g5 supervisor brief), distinct from G3's
/// two-solutions gate: that gate's baseline accessibility was 0 because
/// the graph was fully disconnected, so both "solutions" merely restored
/// connectivity -- interesting mechanically, but not a real decision
/// (nothing was actually foreclosed by either choice). This scenario is a
/// real budget trade-off: TWO cohorts are each disconnected from the only
/// job-bearing building, each fixable by its own new road connector, and
/// the player's cash is enough for EXACTLY ONE of the two -- committing
/// either one MEASURABLY improves that cohort's real needs AND MEASURABLY
/// forecloses the other cohort's fix (the second commit attempt fails on
/// insufficient budget, not a contrived rule). Both directions are proven
/// with real numbers from the same mechanism PlanningEngineTests/
/// AccessibilityGraphTests already use -- nothing here is a new metric
/// invented for this scenario.
/// </summary>
public class BudgetConstrainedTradeOffTests
{
    private const long NodeHub = 1; // job hub -- office building sits here.
    private const long NodeCohortA = 2; // cohort A's residential building sits here.
    private const long NodeCohortB = 3; // cohort B's residential building sits here.

    // Deterministic archetype hashes verified against WorldState.AssignArchetype (see git history for the scratch check that found these).
    private const long OfficeBuildingId = 3009; // hashes to BuildingArchetype.Office.
    private const long CohortABuildingId = 3000; // hashes to BuildingArchetype.Residential.
    private const long CohortBBuildingId = 3015; // hashes to BuildingArchetype.Residential.

    private const long RoadCostA = 200_000;
    private const long RoadCostB = 250_000;
    private const long InitialCash = 300_000; // >= either cost alone, < the sum of both -- the scarcity that makes this a real trade-off.

    private readonly ITestOutputHelper _output;

    public BudgetConstrainedTradeOffTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Three nodes, ZERO road_graph edges -- the hub and both
    /// cohorts start with no path between any of them at all (worse than
    /// SimulationFixtures' canal: there is no pre-existing local cluster
    /// to be "merely reconnected" here, matching AGENTS.md rule 1's
    /// discipline of never faking connectivity that is not there). Only a
    /// committed NewRoadConnector can ever create a path.</summary>
    private static RoadGraph BuildRoadGraph()
    {
        var nodes = new List<RoadGraphNode>
        {
            new(NodeHub, 0, 0),
            new(NodeCohortA, 300, 0),
            new(NodeCohortB, -300, 0),
        };

        return new RoadGraph(nodes, new List<RoadEdge>(), new HashSet<long>(), new List<TurnRestrictionRecord>(), new List<Gateway>(),
            new RoadGraphBoundary(new List<double> { -300, 0, 300, 0 }, 0, new Dictionary<string, int>()));
    }

    private static GeographyBase BuildGeographyBase()
    {
        var buildings = new List<PolygonFeature>
        {
            BuildBuilding(OfficeBuildingId, "office", cx: 0, cz: 3),
            BuildBuilding(CohortABuildingId, "residential", cx: 300, cz: 3),
            BuildBuilding(CohortBBuildingId, "residential", cx: -300, cz: 3),
        };

        return new GeographyBase(buildings, new List<PolygonFeature>(), new List<PolygonFeature>(), new List<LineFeature>(), new List<LineFeature>());
    }

    private static PolygonFeature BuildBuilding(long sourceId, string use, double cx, double cz)
    {
        var outer = new List<Vec2> { new(cx - 1, cz - 1), new(cx + 1, cz - 1), new(cx + 1, cz + 1), new(cx - 1, cz + 1) };
        var ringGroup = new RingGroup(outer, new List<IReadOnlyList<Vec2>>());
        var tags = new SourceTags(new Dictionary<string, string> { ["building"] = use });
        return new PolygonFeature("building", "way", sourceId, tags, new List<AssumptionRecord>(), new List<AssumptionRecord>(),
            new List<RingGroup> { ringGroup }, new List<RingGroup> { ringGroup });
    }

    private static WorldState BuildWorld()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("{}");
        var simInit = new SimulationInitialization("0.1.0", "trade-off test fixture -- fictional, not real Thai statistics.", doc.RootElement.Clone());
        return new WorldState(BuildGeographyBase(), simInit, BuildRoadGraph(), new ScenarioConfig(masterSeed: 7, InitialCash));
    }

    private static string CohortIdFor(WorldState world, long homeBuildingSourceId)
    {
        foreach (var (id, cohort) in world.Cohorts)
        {
            if (cohort.HomeBuildingSourceId == homeBuildingSourceId)
            {
                return id;
            }
        }

        throw new System.InvalidOperationException($"No cohort found for building {homeBuildingSourceId}.");
    }

    [Fact]
    public void CommittingCohortAsRoad_MeasurablyHelpsA_AndForecloses_MeasurablyLeavesBUnhelped()
    {
        var world = BuildWorld();
        var cohortAId = CohortIdFor(world, CohortABuildingId);
        var cohortBId = CohortIdFor(world, CohortBBuildingId);

        var baselineA = world.ComputeCohortNeeds(cohortAId, hourOfDay: 12);
        var baselineB = world.ComputeCohortNeeds(cohortBId, hourOfDay: 12);
        _output.WriteLine($"BEFORE -- cohort A: Access={baselineA.Access}, Economy={baselineA.Economy} | cohort B: Access={baselineB.Access}, Economy={baselineB.Economy}");
        Assert.Equal(0, baselineA.Access);
        Assert.Equal(0, baselineB.Access);

        var draftA = new NewRoadConnectorDraft("road-to-a", world.Revision, RoadCostA,
            PlanningEngine.EstimateNewRoadAccessImpact(world, NodeHub, NodeCohortA, CohortABuildingId), NodeHub, NodeCohortA);
        var commitA = PlanningEngine.CommitNewRoadConnector(world, draftA, LedgerAccountKind.Capex, new long[] { RoadCostA });
        Assert.True(commitA.Success, string.Join("; ", commitA.Failures));

        var afterA_A = world.ComputeCohortNeeds(cohortAId, hourOfDay: 12);
        var afterA_B = world.ComputeCohortNeeds(cohortBId, hourOfDay: 12);
        _output.WriteLine($"AFTER committing A's road -- cohort A: Access={afterA_A.Access}, Economy={afterA_A.Economy} | cohort B: Access={afterA_B.Access}, Economy={afterA_B.Economy}");
        _output.WriteLine($"Budget remaining: {world.Ledger.Available} (B's road needs {RoadCostB}).");

        // The helped side: measurably better, from the same
        // AccessibilityNeed/AccessibilityGraph mechanism every other
        // accessibility test in this suite uses.
        Assert.True(afterA_A.Access > baselineA.Access, "cohort A's Access must measurably improve once its road is committed.");
        Assert.True(afterA_A.Economy > baselineA.Economy);

        // The foreclosed side: cohort B is completely unaffected --
        // committing A's road touches nothing about B's world state.
        Assert.Equal(baselineB.Access, afterA_B.Access);
        Assert.Equal(baselineB.Economy, afterA_B.Economy);
        Assert.Equal(0, afterA_B.Access);

        // The foreclosure itself: B's road is now UNAFFORDABLE, not
        // merely "not chosen" -- a real, mechanical consequence of
        // spending the shared budget on A, proven by actually attempting
        // the commit and getting a real budget-shaped failure.
        var draftB = new NewRoadConnectorDraft("road-to-b", world.Revision, RoadCostB,
            PlanningEngine.EstimateNewRoadAccessImpact(world, NodeHub, NodeCohortB, CohortBBuildingId), NodeHub, NodeCohortB);
        var commitB = PlanningEngine.CommitNewRoadConnector(world, draftB, LedgerAccountKind.Capex, new long[] { RoadCostB });

        Assert.False(commitB.Success, "B's road should be foreclosed by A's spend -- if this succeeds, there was no real scarcity in this scenario.");
        Assert.Contains(commitB.Failures, f => f.Contains("insufficient available budget"));
        _output.WriteLine($"Attempting B's road after A was committed: FAILED as expected -- {string.Join("; ", commitB.Failures)}");

        var finalB = world.ComputeCohortNeeds(cohortBId, hourOfDay: 12);
        Assert.Equal(0, finalB.Access); // the foreclosed cohort's real, measured outcome stays exactly at baseline.
    }

    /// <summary>The symmetric case, on a FRESH world (same seed, same
    /// starting budget) -- proves this is a real two-way trade-off, not an
    /// artifact of which project happens to be tried first: committing B
    /// instead measurably helps B and forecloses A, with the mirror-image
    /// numbers of the test above.</summary>
    [Fact]
    public void CommittingCohortBsRoadInstead_MeasurablyHelpsB_AndForecloses_MeasurablyLeavesAUnhelped()
    {
        var world = BuildWorld();
        var cohortAId = CohortIdFor(world, CohortABuildingId);
        var cohortBId = CohortIdFor(world, CohortBBuildingId);

        var baselineA = world.ComputeCohortNeeds(cohortAId, hourOfDay: 12);
        var baselineB = world.ComputeCohortNeeds(cohortBId, hourOfDay: 12);

        var draftB = new NewRoadConnectorDraft("road-to-b", world.Revision, RoadCostB,
            PlanningEngine.EstimateNewRoadAccessImpact(world, NodeHub, NodeCohortB, CohortBBuildingId), NodeHub, NodeCohortB);
        var commitB = PlanningEngine.CommitNewRoadConnector(world, draftB, LedgerAccountKind.Capex, new long[] { RoadCostB });
        Assert.True(commitB.Success, string.Join("; ", commitB.Failures));

        var afterB_B = world.ComputeCohortNeeds(cohortBId, hourOfDay: 12);
        var afterB_A = world.ComputeCohortNeeds(cohortAId, hourOfDay: 12);
        _output.WriteLine($"AFTER committing B's road instead -- cohort B: Access={afterB_B.Access}, Economy={afterB_B.Economy} | cohort A: Access={afterB_A.Access}, Economy={afterB_A.Economy}");
        _output.WriteLine($"Budget remaining: {world.Ledger.Available} (A's road needs {RoadCostA}).");

        Assert.True(afterB_B.Access > baselineB.Access);
        Assert.Equal(baselineA.Access, afterB_A.Access);
        Assert.Equal(0, afterB_A.Access);

        var draftA = new NewRoadConnectorDraft("road-to-a", world.Revision, RoadCostA,
            PlanningEngine.EstimateNewRoadAccessImpact(world, NodeHub, NodeCohortA, CohortABuildingId), NodeHub, NodeCohortA);
        var commitA = PlanningEngine.CommitNewRoadConnector(world, draftA, LedgerAccountKind.Capex, new long[] { RoadCostA });

        Assert.False(commitA.Success);
        Assert.Contains(commitA.Failures, f => f.Contains("insufficient available budget"));
        _output.WriteLine($"Attempting A's road after B was committed: FAILED as expected -- {string.Join("; ", commitA.Failures)}");
    }

    /// <summary>Control: with a budget large enough for BOTH roads, there
    /// is no foreclosure at all -- proves the scarcity above comes from
    /// the deliberately chosen InitialCash, not from some hidden rule that
    /// always blocks a second project.</summary>
    [Fact]
    public void WithSufficientBudgetForBoth_NeitherIsForeclosed_ControlCase()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("{}");
        var simInit = new SimulationInitialization("0.1.0", "trade-off control fixture.", doc.RootElement.Clone());
        var world = new WorldState(BuildGeographyBase(), simInit, BuildRoadGraph(), new ScenarioConfig(masterSeed: 7, RoadCostA + RoadCostB));

        var draftA = new NewRoadConnectorDraft("road-to-a", world.Revision, RoadCostA,
            PlanningEngine.EstimateNewRoadAccessImpact(world, NodeHub, NodeCohortA, CohortABuildingId), NodeHub, NodeCohortA);
        var commitA = PlanningEngine.CommitNewRoadConnector(world, draftA, LedgerAccountKind.Capex, new long[] { RoadCostA });
        Assert.True(commitA.Success);

        var draftB = new NewRoadConnectorDraft("road-to-b", world.Revision, RoadCostB,
            PlanningEngine.EstimateNewRoadAccessImpact(world, NodeHub, NodeCohortB, CohortBBuildingId), NodeHub, NodeCohortB);
        var commitB = PlanningEngine.CommitNewRoadConnector(world, draftB, LedgerAccountKind.Capex, new long[] { RoadCostB });
        Assert.True(commitB.Success, string.Join("; ", commitB.Failures));

        var cohortAId = CohortIdFor(world, CohortABuildingId);
        var cohortBId = CohortIdFor(world, CohortBBuildingId);
        Assert.True(world.ComputeCohortNeeds(cohortAId, 12).Access > 0);
        Assert.True(world.ComputeCohortNeeds(cohortBId, 12).Access > 0);
        Assert.Equal(0, world.Ledger.Available);
    }
}
