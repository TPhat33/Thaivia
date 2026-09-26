using System.Collections.Generic;
using Thaivia.Core.MapPack;

namespace Thaivia.Core.Tests.Simulation.Mobility;

/// <summary>
/// Shared two-route network: node A to node B has a SHORT, LOW-capacity
/// direct way and a LONGER, HIGH-capacity detour through node "Detour".
/// Used by both <see cref="AllOrNothingAssignmentDistortionTests"/> (which
/// documents the pre-ADR-0027 distortion this shape exposes) and
/// <see cref="CongestionAwareAssignmentTests"/> (which proves ADR-0027's
/// replacement actually fixes it) -- kept as ONE shared fixture so a
/// reader can be sure both tests are describing the exact same network,
/// not two similar-looking ones that happen to support each side's
/// story.
/// </summary>
internal static class TwoRouteAssignmentFixtures
{
    public const long NodeA = 1;
    public const long NodeB = 2;
    public const long NodeDetour = 3;

    public const long ShortLowCapacityWay = 901; // A-B direct, 200m, 1 lane -> 2 veh/tick.
    public const long LongHighCapacityWay1 = 902; // A-Detour, part of the alternate route.
    public const long LongHighCapacityWay2 = 903; // Detour-B, part of the alternate route.

    public static RoadGraph BuildTwoRouteGraph()
    {
        var nodes = new List<RoadGraphNode>
        {
            new(NodeA, 0, 0),
            new(NodeB, 200, 0),
            new(NodeDetour, 100, 150), // makes the detour route ~360m, strictly longer than the 200m direct route.
        };

        var edges = new List<RoadEdge>
        {
            BuildEdge(ShortLowCapacityWay, NodeA, NodeB, lanes: 1, (0, 0), (200, 0)),
            BuildEdge(LongHighCapacityWay1, NodeA, NodeDetour, lanes: 10, (0, 0), (100, 150)),
            BuildEdge(LongHighCapacityWay2, NodeDetour, NodeB, lanes: 10, (100, 150), (200, 0)),
        };

        return new RoadGraph(nodes, edges, new HashSet<long>(), new List<TurnRestrictionRecord>(), new List<Gateway>(),
            new RoadGraphBoundary(new List<double> { 0, 0, 200, 150 }, 0, new Dictionary<string, int>()));
    }

    private static RoadEdge BuildEdge(long wayId, long fromNode, long toNode, int lanes, (double X, double Z) from, (double X, double Z) to)
    {
        var tags = new SourceTags(new Dictionary<string, string> { ["lanes"] = lanes.ToString() });
        var coords = new List<Vec2> { new(from.X, from.Z), new(to.X, to.Z) };
        return new RoadEdge(wayId, tags, new List<AssumptionRecord>(), new List<AssumptionRecord>(),
            new long[] { fromNode, toNode }, coords, coords,
            OnewayDirection.No, layer: 0, bridge: false, tunnel: false, gradeSeparated: false,
            new Dictionary<string, string>(), fromNode, toNode);
    }

    /// <summary>Effective per-tick capacity for each way in this fixture --
    /// the SAME numbers <see cref="AllOrNothingAssignmentDistortionTests"/>
    /// passes to <see cref="Thaivia.Core.Simulation.Mobility.Queues.LinkQueueSimulator"/>
    /// directly, exposed here as a dictionary for
    /// <see cref="Thaivia.Core.Simulation.Mobility.Demand.NetworkDemandAssignment.AssignToWaysCongestionAware"/>'s
    /// capacity input.</summary>
    public static IReadOnlyDictionary<long, int> CapacityByWayId() => new Dictionary<long, int>
    {
        [ShortLowCapacityWay] = 2,
        [LongHighCapacityWay1] = 20,
        [LongHighCapacityWay2] = 20,
    };
}
