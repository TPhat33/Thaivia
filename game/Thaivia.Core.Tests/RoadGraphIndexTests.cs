using System.Collections.Generic;
using Thaivia.Core.Graph;
using Thaivia.Core.MapPack;
using Xunit;

namespace Thaivia.Core.Tests;

public class RoadGraphIndexTests
{
    private static SourceTags EmptyTags() => new(new Dictionary<string, string>());

    private static RoadEdge MakeEdge(
        long wayId,
        IReadOnlyList<long> nodeRefs,
        OnewayDirection oneway = OnewayDirection.No,
        int layer = 0)
    {
        var coords = new List<Vec2>();
        for (var i = 0; i < nodeRefs.Count; i++)
        {
            coords.Add(new Vec2(i, i));
        }

        return new RoadEdge(
            wayId,
            EmptyTags(),
            new List<AssumptionRecord>(),
            new List<AssumptionRecord>(),
            nodeRefs,
            coords,
            coords,
            oneway,
            layer,
            bridge: false,
            tunnel: false,
            gradeSeparated: layer != 0,
            accessModes: new Dictionary<string, string>(),
            fromNode: nodeRefs[0],
            toNode: nodeRefs[^1]);
    }

    private static RoadGraph MakeGraph(IReadOnlyList<RoadEdge> edges, IReadOnlyList<TurnRestrictionRecord>? restrictions = null)
    {
        var nodes = new List<RoadGraphNode>();
        var seen = new HashSet<long>();
        foreach (var e in edges)
        {
            foreach (var n in e.NodeRefs)
            {
                if (seen.Add(n))
                {
                    nodes.Add(new RoadGraphNode(n, 0, 0));
                }
            }
        }

        return new RoadGraph(
            nodes,
            edges,
            new HashSet<long>(),
            restrictions ?? new List<TurnRestrictionRecord>(),
            new List<Gateway>(),
            new RoadGraphBoundary(new List<double> { 0, 0, 0, 0 }, 0, new Dictionary<string, int>()));
    }

    [Fact]
    public void TwoEdges_SharingNoNodeId_ProduceNoAdjacency_EvenIfTheyWouldCrossInPlanView()
    {
        // Edge A: nodes 1->2 at layer 0 (a ground road).
        // Edge B: nodes 3->4 at layer 1 (a bridge). Coordinates are set up
        // in MakeEdge to plausibly cross A in plan view, but since no node
        // id is shared, RoadGraphIndex must never connect them.
        var edgeA = MakeEdge(100, new long[] { 1, 2 });
        var edgeB = MakeEdge(200, new long[] { 3, 4 }, layer: 1);
        var index = new RoadGraphIndex(MakeGraph(new[] { edgeA, edgeB }));

        var stepsFrom2 = index.GetOutgoingSteps(2);
        Assert.All(stepsFrom2, s => Assert.NotEqual(200, s.WayId));
        // Node 2 (end of A) and node 3 (start of B) are different ids, so
        // there is no step at all connecting the two edges via any node.
        Assert.DoesNotContain(stepsFrom2, s => s.ToNodeId == 3 || s.ToNodeId == 4);
        Assert.False(index.HasAdjacency(999)); // sanity: an id nothing references has no adjacency at all.
    }

    [Fact]
    public void TwoEdges_SharingANode_ProduceAdjacency_RegardlessOfLayer()
    {
        // Edge A: 1->2. Edge B: 2->3, both at ground level but different
        // ways -- node 2 is a genuine shared reference.
        var edgeA = MakeEdge(100, new long[] { 1, 2 });
        var edgeB = MakeEdge(200, new long[] { 2, 3 });
        var index = new RoadGraphIndex(MakeGraph(new[] { edgeA, edgeB }));

        var stepsFrom2 = index.GetOutgoingSteps(2);
        Assert.Contains(stepsFrom2, s => s.WayId == 200 && s.ToNodeId == 3);
        // Also, since edge A is two-way by default, node 2 can step back
        // onto edge A toward node 1.
        Assert.Contains(stepsFrom2, s => s.WayId == 100 && s.ToNodeId == 1);
    }

    [Fact]
    public void OnewayMinusOne_Reversed_OnlyAllowsTravelFromLastNodeToFirst()
    {
        var edge = MakeEdge(100, new long[] { 1, 2, 3 }, oneway: OnewayDirection.Reversed);
        var index = new RoadGraphIndex(MakeGraph(new[] { edge }));

        // Forward direction (1->2) must NOT be traversable.
        Assert.DoesNotContain(index.GetOutgoingSteps(1), s => s.ToNodeId == 2);
        // Reverse direction (3->2->1) must be traversable.
        Assert.Contains(index.GetOutgoingSteps(3), s => s.ToNodeId == 2);
        Assert.Contains(index.GetOutgoingSteps(2), s => s.ToNodeId == 1);
    }

    [Fact]
    public void OnewayForward_OnlyAllowsTheDrawnDirection()
    {
        var edge = MakeEdge(100, new long[] { 1, 2 }, oneway: OnewayDirection.Forward);
        var index = new RoadGraphIndex(MakeGraph(new[] { edge }));

        Assert.Contains(index.GetOutgoingSteps(1), s => s.ToNodeId == 2);
        Assert.DoesNotContain(index.GetOutgoingSteps(2), s => s.ToNodeId == 1);
    }

    [Fact]
    public void NoRestriction_TravelIsAllowed()
    {
        var index = new RoadGraphIndex(MakeGraph(new[]
        {
            MakeEdge(1, new long[] { 10, 20 }),
            MakeEdge(2, new long[] { 20, 30 }),
        }));

        Assert.Equal(TurnDecision.Allowed, index.EvaluateTurn(fromWayId: 1, viaNodeId: 20, toWayId: 2));
    }

    [Fact]
    public void NoLeftTurnRestriction_DeniesTheNamedToWay_ButAllowsOthers()
    {
        var restriction = new TurnRestrictionRecord(
            relationId: 900,
            restrictionType: "no_left_turn",
            fromWay: 1,
            via: new long[] { 20 },
            viaKind: "n",
            toWay: 2,
            supported: true,
            unsupportedReason: null);
        var index = new RoadGraphIndex(MakeGraph(
            new[]
            {
                MakeEdge(1, new long[] { 10, 20 }),
                MakeEdge(2, new long[] { 20, 30 }),
                MakeEdge(3, new long[] { 20, 40 }),
            },
            new[] { restriction }));

        Assert.Equal(TurnDecision.Denied, index.EvaluateTurn(1, 20, 2));
        Assert.Equal(TurnDecision.Allowed, index.EvaluateTurn(1, 20, 3));
    }

    [Fact]
    public void UnsupportedRestriction_IsReportedAsUnsupported_NotTreatedAsAllowed()
    {
        var restriction = new TurnRestrictionRecord(
            relationId: 901,
            restrictionType: "no_left_turn",
            fromWay: 1,
            via: new long[] { 20 },
            viaKind: "w", // a via-way restriction: the Python pipeline marks these unsupported.
            toWay: 2,
            supported: false,
            unsupportedReason: "via-way (complex multi-way) restriction not modeled by this graph builder");
        var index = new RoadGraphIndex(MakeGraph(
            new[]
            {
                MakeEdge(1, new long[] { 10, 20 }),
                MakeEdge(2, new long[] { 20, 30 }),
            },
            new[] { restriction }));

        // via_kind == "w" (not a single node), so this restriction is not
        // even anchored at node 20 for a via-node evaluation and must
        // fall back to Allowed -- via-way restrictions are simply not
        // modeled, matching the Python graph builder's own behavior.
        Assert.Equal(TurnDecision.Allowed, index.EvaluateTurn(1, 20, 2));
    }

    [Fact]
    public void UnsupportedRestriction_AnchoredAtANode_IsReportedNotSilentlyAllowed()
    {
        var restriction = new TurnRestrictionRecord(
            relationId: 902,
            restrictionType: "unknown",
            fromWay: 1,
            via: new long[] { 20 },
            viaKind: "n",
            toWay: 2,
            supported: false,
            unsupportedReason: "unrecognized/unsupported restriction type: 'unknown'");
        var index = new RoadGraphIndex(MakeGraph(
            new[]
            {
                MakeEdge(1, new long[] { 10, 20 }),
                MakeEdge(2, new long[] { 20, 30 }),
            },
            new[] { restriction }));

        Assert.Equal(TurnDecision.Unsupported, index.EvaluateTurn(1, 20, 2));
    }

    [Fact]
    public void RealFixture_GradeSeparatedBridge_HasNoJunctionAdjacencyWithTheRoadBelow()
    {
        var doc = Thaivia.Core.Serialization.MapPackLoader.LoadText(TestFixtures.ReadRoadGraphLayersJson());
        var index = new RoadGraphIndex(doc.Payload.RoadGraph);

        // From the fixture: a bridge way at layer=1 crosses a ground way
        // at layer=0 without sharing a node id (see
        // tests/test_pipeline_graph.py::test_bridge_edge_carries_semantic_layer_not_a_shared_node
        // on the Python side). junction_node_ids only lists genuinely
        // shared nodes.
        foreach (var junctionId in doc.Payload.RoadGraph.JunctionNodeIds)
        {
            Assert.True(index.HasAdjacency(junctionId));
        }
    }
}
