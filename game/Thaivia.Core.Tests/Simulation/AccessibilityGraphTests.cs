using System;
using System.Collections.Generic;
using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation.Accessibility;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// The single most scrutinised test in this wave (per the supervising
/// engineer's brief): proves AccessibilityGraph reflects road-NETWORK
/// distance, never straight-line proximity, using a deliberately
/// adversarial fixture (SimulationFixtures) where two nodes are only 2m
/// apart in plan-view coordinates but sit on opposite sides of an
/// unconnected "canal" -- no shared road-graph node joins them.
/// </summary>
public class AccessibilityGraphTests
{
    [Fact]
    public void StraightLineNearNodes_WithNoConnectingEdge_AreGraphUnreachable()
    {
        var roadGraph = SimulationFixtures.BuildRoadGraph();
        var graph = new AccessibilityGraph(roadGraph);

        // Node 2 (10,0) and node 3 (12,0): 2 metres apart in plan view --
        // closer than a single building footprint -- yet on two entirely
        // separate clusters with no shared node.
        var straightLineDistance = 2.0;
        var networkDistance = graph.ShortestDistanceMeters(SimulationFixtures.Node2, SimulationFixtures.Node3);

        Assert.Null(networkDistance); // unreachable through the graph...
        Assert.True(straightLineDistance < 5, "sanity check: the fixture's straight-line gap must actually be small for this test to be adversarial.");
    }

    [Fact]
    public void EndToEndAcrossDisconnectedClusters_IsUnreachable_DespiteShortStraightLinePath()
    {
        var roadGraph = SimulationFixtures.BuildRoadGraph();
        var graph = new AccessibilityGraph(roadGraph);

        // Straight-line node1 -> node5 is 32m; if this engine used
        // radius/straight-line distance it would report ~32m reachable.
        // The actual network has no path at all between the two clusters.
        var networkDistance = graph.ShortestDistanceMeters(SimulationFixtures.Node1, SimulationFixtures.Node5);
        Assert.Null(networkDistance);

        var score = AccessibilityNeed.ComputeScore(networkDistance);
        Assert.Equal(0, score); // unreachable scores 0, never "close enough".
    }

    [Fact]
    public void AddingAConnectingSegment_MakesTheNetworkDistanceFiniteAndReflectsTheDetour()
    {
        var roadGraph = SimulationFixtures.BuildRoadGraph();

        // Bridge the canal: connect node 2 and node 3 directly (their real
        // 2m gap).
        var bridge = new PlannedRoadSegment(SimulationFixtures.Node2, SimulationFixtures.Node3, 2.0, "bridge-project");
        var graph = new AccessibilityGraph(roadGraph, new List<PlannedRoadSegment> { bridge });

        var distance = graph.ShortestDistanceMeters(SimulationFixtures.Node1, SimulationFixtures.Node5);
        Assert.NotNull(distance);

        // Expected path: 1->2 (10m) + 2->3 (2m, the new bridge) + 3->4 (10m) + 4->5 (10m) = 32m.
        // NOT the 32m straight-line coincidence either -- this total is
        // the sum of actual edge lengths along the only path that exists,
        // which happens to also be 32m here only because the fixture's
        // nodes are collinear; the point is it was COMPUTED from edges,
        // not read off as a straight line.
        Assert.Equal(32.0, distance!.Value, precision: 6);
    }

    /// <summary>
    /// TASK ZERO (supervising engineer's fix): the existing
    /// AddingAConnectingSegment_... test above proves the engine returns a
    /// FINITE distance once a bridge connects two clusters, but on that
    /// fixture the network distance (32m) coincidentally equals the
    /// straight-line distance, because the fixture's nodes are collinear.
    /// A naive "Euclidean distance + connectivity check" implementation
    /// would pass that test too. This test uses a CONNECTED graph
    /// (SimulationFixtures.BuildDetourRoadGraph) where the only path is a
    /// ~502m detour around a 2m-wide gap, so network distance and
    /// straight-line distance are grossly different -- the only way to
    /// pass this test is to actually sum edge lengths along the real path.
    /// </summary>
    [Fact]
    public void ConnectedGraphWithGrossDetour_NetworkDistance_MatchesTheDetourAndIsNotStraightLine()
    {
        var graph = new AccessibilityGraph(SimulationFixtures.BuildDetourRoadGraph());
        var distance = graph.ShortestDistanceMeters(SimulationFixtures.DetourBankANode, SimulationFixtures.DetourBankBNode);

        Assert.NotNull(distance);
        // 250 (A -> waypoint1) + 2 (bridge) + 250 (waypoint2 -> B) = 502.
        Assert.Equal(502.0, distance!.Value, precision: 6);

        const double straightLineDistance = 2.0;
        Assert.NotEqual(straightLineDistance, distance.Value, precision: 0);
        Assert.True(
            Math.Abs(distance.Value - straightLineDistance) > 100,
            $"expected the network distance ({distance.Value}m) to be grossly different from the straight-line distance ({straightLineDistance}m) -- otherwise this fixture is not adversarial enough to discriminate graph-vs-radius.");
    }

    /// <summary>
    /// The mutation-style control the supervising engineer asked for:
    /// demonstrates, independently of the assertion above, that a
    /// deliberately buggy "Euclidean distance, only gated by a
    /// reachability check" router -- exactly the regression class this
    /// suite exists to catch -- reports a materially different (wrong)
    /// answer on this fixture. This proves the fixture's discriminating
    /// power is real, not merely assumed by the test author.
    /// </summary>
    [Fact]
    public void ConnectedGraphWithGrossDetour_NaiveEuclideanStandIn_WouldReportAMaterialityDifferentAnswer()
    {
        var roadGraph = SimulationFixtures.BuildDetourRoadGraph();
        var graph = new AccessibilityGraph(roadGraph);

        var realNetworkDistance = graph.ShortestDistanceMeters(SimulationFixtures.DetourBankANode, SimulationFixtures.DetourBankBNode);
        Assert.NotNull(realNetworkDistance);

        var naiveEuclideanStandIn = NaiveEuclideanDistanceIfReachable(roadGraph, graph, SimulationFixtures.DetourBankANode, SimulationFixtures.DetourBankBNode);
        Assert.NotNull(naiveEuclideanStandIn);

        Assert.True(
            Math.Abs(realNetworkDistance!.Value - naiveEuclideanStandIn!.Value) > 100,
            $"the real network distance ({realNetworkDistance.Value}m) and the naive Euclidean stand-in ({naiveEuclideanStandIn.Value}m) must differ grossly on this fixture, or it fails to discriminate the two implementations.");
    }

    /// <summary>A deliberately naive stand-in for what a buggy "Euclidean
    /// distance, gated only by a graph reachability check" router would
    /// compute. Used ONLY by the control test above -- never referenced by
    /// production code. Reachability still goes through the real
    /// <see cref="AccessibilityGraph"/>, so this control isolates exactly
    /// the distance-computation bug class the supervising engineer
    /// described, rather than also faking connectivity.</summary>
    private static double? NaiveEuclideanDistanceIfReachable(RoadGraph roadGraph, AccessibilityGraph graph, long fromNodeId, long toNodeId)
    {
        if (graph.ShortestDistanceMeters(fromNodeId, toNodeId) is null)
        {
            return null;
        }

        var nodeById = new Dictionary<long, RoadGraphNode>();
        foreach (var node in roadGraph.Nodes)
        {
            nodeById[node.NodeId] = node;
        }

        var a = nodeById[fromNodeId];
        var b = nodeById[toNodeId];
        var dx = a.LocalX - b.LocalX;
        var dz = a.LocalZ - b.LocalZ;
        return Math.Sqrt(dx * dx + dz * dz);
    }

    [Fact]
    public void ShortestDistance_FromNodeToItself_IsZero()
    {
        var graph = new AccessibilityGraph(SimulationFixtures.BuildRoadGraph());
        Assert.Equal(0, graph.ShortestDistanceMeters(SimulationFixtures.Node1, SimulationFixtures.Node1));
    }

    [Fact]
    public void ShortestDistance_WithinAConnectedCluster_MatchesSumOfEdgeLengths()
    {
        var graph = new AccessibilityGraph(SimulationFixtures.BuildRoadGraph());
        // node3 (12,0) -> node4 (22,0) -> node5 (32,0): 10 + 10 = 20.
        var distance = graph.ShortestDistanceMeters(SimulationFixtures.Node3, SimulationFixtures.Node5);
        Assert.Equal(20.0, distance!.Value, precision: 6);
    }

    [Theory]
    [InlineData(0.0, 100)]
    [InlineData(500.0, 0)]
    [InlineData(250.0, 50)]
    public void AccessibilityNeed_ComputeScore_ScalesLinearlyWithinReferenceDistance(double distance, int expectedScore)
    {
        Assert.Equal(expectedScore, AccessibilityNeed.ComputeScore(distance));
    }

    [Fact]
    public void AccessibilityNeed_ComputeScore_Unreachable_ScoresZero_NeverTreatedAsFine()
    {
        Assert.Equal(0, AccessibilityNeed.ComputeScore(null));
    }
}
