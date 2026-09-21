using System.Collections.Generic;
using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation.Mobility.Crossings;
using Thaivia.Core.Simulation.Mobility.Routing;
using Xunit;

namespace Thaivia.Core.Tests.Simulation.Mobility;

/// <summary>
/// Proves a pedestrian crossing's AccessibleFlag actually changes which
/// walking route is computed -- not merely what an inspector displays.
///
///   NodeA (0,0) --(32m, the long way around)-- NodeB (2,0)
///
/// NodeA and NodeB sit right across a 2m gap with no direct road-graph
/// edge (same "canal" shape as AccessibilityGraphTests' adversarial
/// fixture); the only baseline path is a 32m loop.
/// </summary>
public class CrossingsTests
{
    private const long NodeA = 1;
    private const long NodeB = 2;
    private const long Detour1 = 3;
    private const long Detour2 = 4;

    private static RoadEdge MakeEdge(long wayId, long from, long to)
    {
        var nodeRefs = new List<long> { from, to };
        var coords = new List<Vec2> { new(0, 0), new(1, 1) };
        return new RoadEdge(wayId, new SourceTags(new Dictionary<string, string>()), new List<AssumptionRecord>(), new List<AssumptionRecord>(),
            nodeRefs, coords, coords, OnewayDirection.No, layer: 0, bridge: false, tunnel: false, gradeSeparated: false,
            accessModes: new Dictionary<string, string>(), fromNode: from, toNode: to);
    }

    private static RoadGraph BuildBaseGraph()
    {
        var nodes = new List<RoadGraphNode>
        {
            new(NodeA, 0, 0),
            new(Detour1, 0, 15),
            new(Detour2, 2, 15),
            new(NodeB, 2, 0),
        };

        var edges = new List<RoadEdge>
        {
            MakeEdge(1, NodeA, Detour1), // 15m
            MakeEdge(2, Detour1, Detour2), // 2m
            MakeEdge(3, Detour2, NodeB), // 15m
        };

        return new RoadGraph(nodes, edges, new HashSet<long>(), new List<TurnRestrictionRecord>(), new List<Gateway>(),
            new RoadGraphBoundary(new List<double> { 0, 0, 2, 15 }, 0, new Dictionary<string, int>()));
    }

    [Fact]
    public void WithoutACrossing_WalkingMustTakeTheLongDetour()
    {
        var graph = new MobilityGraph(BuildBaseGraph(), TravelMode.Walk);
        Assert.Equal(32.0, graph.ShortestDistanceMeters(NodeA, NodeB)!.Value, precision: 6);
    }

    /// <summary>The test the supervising engineer asked for by name: the
    /// accessible-path flag changes routing, not just display. A crossing
    /// that is NOT flagged accessible shortens the GENERAL walking route
    /// but must NOT be used by a traveller who needs an accessible path --
    /// they still take the long way.</summary>
    [Fact]
    public void InaccessibleCrossing_ShortensGeneralWalkingRoute_ButAccessibleNeedTravellerStillDetours()
    {
        var roadGraph = BuildBaseGraph();
        var crossing = new PedestrianCrossing("crossing-1", NodeA, NodeB, LengthMeters: 2.0, AccessibleFlag: false);
        var crossings = new List<PedestrianCrossing> { crossing };

        var generalWalk = new MobilityGraph(roadGraph, TravelMode.Walk, crossings: crossings, requireAccessibleCrossings: false);
        Assert.Equal(2.0, generalWalk.ShortestDistanceMeters(NodeA, NodeB)!.Value, precision: 6);

        var accessibleNeedWalk = new MobilityGraph(roadGraph, TravelMode.Walk, crossings: crossings, requireAccessibleCrossings: true);
        Assert.Equal(32.0, accessibleNeedWalk.ShortestDistanceMeters(NodeA, NodeB)!.Value, precision: 6);

        // The two routing results must differ -- proving the flag really
        // changes which route is computed, not just what is shown.
        Assert.NotEqual(generalWalk.ShortestDistanceMeters(NodeA, NodeB), accessibleNeedWalk.ShortestDistanceMeters(NodeA, NodeB));
    }

    [Fact]
    public void AccessibleCrossing_ShortensBothTheGeneralAndTheAccessibleNeedRoute()
    {
        var roadGraph = BuildBaseGraph();
        var crossing = new PedestrianCrossing("crossing-2", NodeA, NodeB, LengthMeters: 2.0, AccessibleFlag: true);
        var crossings = new List<PedestrianCrossing> { crossing };

        var generalWalk = new MobilityGraph(roadGraph, TravelMode.Walk, crossings: crossings, requireAccessibleCrossings: false);
        var accessibleNeedWalk = new MobilityGraph(roadGraph, TravelMode.Walk, crossings: crossings, requireAccessibleCrossings: true);

        Assert.Equal(2.0, generalWalk.ShortestDistanceMeters(NodeA, NodeB)!.Value, precision: 6);
        Assert.Equal(2.0, accessibleNeedWalk.ShortestDistanceMeters(NodeA, NodeB)!.Value, precision: 6);
    }

    [Fact]
    public void CrossingsAreIgnoredForVehicleMode_ACrossingIsNotARoad()
    {
        var roadGraph = BuildBaseGraph();
        var crossing = new PedestrianCrossing("crossing-3", NodeA, NodeB, LengthMeters: 2.0, AccessibleFlag: true);
        var crossings = new List<PedestrianCrossing> { crossing };

        var vehicleGraph = new MobilityGraph(roadGraph, TravelMode.Vehicle, crossings: crossings);
        Assert.Equal(32.0, vehicleGraph.ShortestDistanceMeters(NodeA, NodeB)!.Value, precision: 6);
    }
}
