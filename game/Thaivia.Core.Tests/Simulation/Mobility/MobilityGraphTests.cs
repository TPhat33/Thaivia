using System.Collections.Generic;
using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation.Mobility.Routing;
using Xunit;

namespace Thaivia.Core.Tests.Simulation.Mobility;

public class MobilityGraphTests
{
    private static SourceTags EmptyTags() => new(new Dictionary<string, string>());

    private static RoadEdge MakeEdge(
        long wayId,
        long fromNode,
        long toNode,
        OnewayDirection oneway = OnewayDirection.No,
        IReadOnlyDictionary<string, string>? accessModes = null)
    {
        var nodeRefs = new List<long> { fromNode, toNode };
        var coords = new List<Vec2> { new(0, 0), new(1, 1) };
        return new RoadEdge(
            wayId,
            EmptyTags(),
            new List<AssumptionRecord>(),
            new List<AssumptionRecord>(),
            nodeRefs,
            coords,
            coords,
            oneway,
            layer: 0,
            bridge: false,
            tunnel: false,
            gradeSeparated: false,
            accessModes: accessModes ?? new Dictionary<string, string>(),
            fromNode: fromNode,
            toNode: toNode);
    }

    /// <summary>
    /// Node 10 -> 20 (way1, 10m) -> a "no left turn" from way1 into way2 at
    /// node 20. way2 (20 -> 30, 10m) is the SHORT direct continuation;
    /// way3 (20 -> 40, 24m) + way4 (40 -> 30, 26m) is the legal detour
    /// (3-4-5-scaled triangle for an exact integer hypotenuse). Direct =
    /// 20m, detour = 60m.
    /// </summary>
    private static RoadGraph BuildTurnRestrictedGraph()
    {
        var nodes = new List<RoadGraphNode>
        {
            new(10, 0, 0),
            new(20, 10, 0),
            new(30, 20, 0),
            new(40, 10, 24),
        };

        var edges = new List<RoadEdge>
        {
            MakeEdge(1, 10, 20),
            MakeEdge(2, 20, 30),
            MakeEdge(3, 20, 40),
            MakeEdge(4, 40, 30),
        };

        var restriction = new TurnRestrictionRecord(
            relationId: 5001,
            restrictionType: "no_left_turn",
            fromWay: 1,
            via: new long[] { 20 },
            viaKind: "n",
            toWay: 2,
            supported: true,
            unsupportedReason: null);

        return new RoadGraph(nodes, edges, new HashSet<long>(), new List<TurnRestrictionRecord> { restriction }, new List<Gateway>(),
            new RoadGraphBoundary(new List<double> { 0, 0, 20, 24 }, 0, new Dictionary<string, int>()));
    }

    /// <summary>The flagship test the supervising engineer asked to see by
    /// name: vehicle routing must not ignore turn restrictions (unlike
    /// G3's AccessibilityGraph, which deliberately does -- see its own doc
    /// comment). Walk mode, by contrast, does not evaluate `type=restriction`
    /// relations at all (see MobilityGraph's class doc comment for why),
    /// so it takes the short direct path the restriction would have
    /// blocked for a car.</summary>
    [Fact]
    public void VehicleMode_RespectsTurnRestriction_TakesTheLegalDetour_WhereWalkModeIgnoresItAndGoesDirect()
    {
        var graph = BuildTurnRestrictedGraph();

        var vehicleRoute = new MobilityGraph(graph, TravelMode.Vehicle).ShortestRoute(10, 30);
        Assert.NotNull(vehicleRoute);
        Assert.Equal(60.0, vehicleRoute!.Value.DistanceMeters, precision: 6); // forced onto the detour.
        Assert.DoesNotContain(2L, vehicleRoute.Value.WayIdsInOrder); // way2 (the restricted turn) is never used.

        var walkRoute = new MobilityGraph(graph, TravelMode.Walk).ShortestRoute(10, 30);
        Assert.NotNull(walkRoute);
        Assert.Equal(20.0, walkRoute!.Value.DistanceMeters, precision: 6); // takes the short direct path.

        // The whole point: the same graph gives a materially different
        // answer per mode, because only Vehicle evaluates the restriction.
        Assert.True(vehicleRoute.Value.DistanceMeters > walkRoute.Value.DistanceMeters + 30);
    }

    [Fact]
    public void FreightMode_AlsoRespectsTheSameTurnRestriction()
    {
        var graph = BuildTurnRestrictedGraph();
        var freightRoute = new MobilityGraph(graph, TravelMode.Freight).ShortestRoute(10, 30);
        Assert.NotNull(freightRoute);
        Assert.Equal(60.0, freightRoute!.Value.DistanceMeters, precision: 6);
    }

    [Fact]
    public void VehicleMode_RespectsOneway_ButWalkModeTreatsTheRoadAsBidirectional()
    {
        var nodes = new List<RoadGraphNode> { new(1, 0, 0), new(2, 30, 0) };
        var edges = new List<RoadEdge> { MakeEdge(100, 1, 2, OnewayDirection.Forward) };
        var graph = new RoadGraph(nodes, edges, new HashSet<long>(), new List<TurnRestrictionRecord>(), new List<Gateway>(),
            new RoadGraphBoundary(new List<double> { 0, 0, 30, 0 }, 0, new Dictionary<string, int>()));

        // Against the oneway direction: a vehicle cannot go 2 -> 1 at all.
        Assert.Null(new MobilityGraph(graph, TravelMode.Vehicle).ShortestDistanceMeters(2, 1));
        // With the oneway direction: fine.
        Assert.Equal(30.0, new MobilityGraph(graph, TravelMode.Vehicle).ShortestDistanceMeters(1, 2)!.Value, precision: 6);

        // A pedestrian is not bound by a car's one-way restriction.
        Assert.Equal(30.0, new MobilityGraph(graph, TravelMode.Walk).ShortestDistanceMeters(2, 1)!.Value, precision: 6);
    }

    [Fact]
    public void ModeAccess_ExplicitFootNo_ExcludesWalkOnly()
    {
        var nodes = new List<RoadGraphNode> { new(1, 0, 0), new(2, 10, 0) };
        var accessModes = new Dictionary<string, string> { ["foot"] = "no" };
        var edges = new List<RoadEdge> { MakeEdge(100, 1, 2, accessModes: accessModes) };
        var graph = new RoadGraph(nodes, edges, new HashSet<long>(), new List<TurnRestrictionRecord>(), new List<Gateway>(),
            new RoadGraphBoundary(new List<double> { 0, 0, 10, 0 }, 0, new Dictionary<string, int>()));

        Assert.Null(new MobilityGraph(graph, TravelMode.Walk).ShortestDistanceMeters(1, 2));
        Assert.Equal(10.0, new MobilityGraph(graph, TravelMode.Vehicle).ShortestDistanceMeters(1, 2)!.Value, precision: 6);
    }

    [Fact]
    public void ModeAccess_UnstatedTag_DefaultsOpen_AsADocumentedAssumption_NotASourceFact()
    {
        var nodes = new List<RoadGraphNode> { new(1, 0, 0), new(2, 10, 0) };
        var edges = new List<RoadEdge> { MakeEdge(100, 1, 2) }; // no access tags at all.
        var graph = new RoadGraph(nodes, edges, new HashSet<long>(), new List<TurnRestrictionRecord>(), new List<Gateway>(),
            new RoadGraphBoundary(new List<double> { 0, 0, 10, 0 }, 0, new Dictionary<string, int>()));

        foreach (var mode in new[] { TravelMode.Walk, TravelMode.Vehicle, TravelMode.Freight })
        {
            Assert.NotNull(new MobilityGraph(graph, mode).ShortestDistanceMeters(1, 2));
        }
    }
}
