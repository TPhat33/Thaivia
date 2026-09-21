using System.Collections.Generic;
using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation.Mobility.Demand;
using Thaivia.Core.Simulation.Mobility.Queues;
using Thaivia.Core.Simulation.Mobility.Routing;
using Xunit;

namespace Thaivia.Core.Tests.Simulation.Mobility;

/// <summary>
/// Answers the g5 supervisor brief's honesty question directly, with a
/// measured example rather than a hand-wave: does
/// <see cref="NetworkDemandAssignment"/> staying all-or-nothing (no
/// capacity-aware rerouting within a pass, see ADR-0022) materially
/// distort results now that queues feed back into the integrated loop?
///
/// YES, when a genuine alternate route exists: this fixture gives node A
/// to node B two paths -- a SHORT one with LOW capacity, and a LONGER one
/// with HIGH (never-exhausted) capacity. Every real-world router with any
/// congestion awareness would eventually shift some demand onto the
/// under-used longer path once the short one backs up. All-or-nothing
/// assignment recomputes the SAME shortest (by free-flow distance,
/// ignoring current queue state) path every tick, so 100% of demand keeps
/// landing on the short/low-capacity way forever -- an unbounded queue
/// forms there while the alternate way sits at zero, even though total
/// network capacity would easily clear all demand if it were split. This
/// is a real, measurable distortion, not a rare edge case: it happens
/// wherever a real network has more than one route between two points and
/// one is even slightly shorter, which the pilot-scale fixture already
/// reproduces at 5 ways.
/// </summary>
public class AllOrNothingAssignmentDistortionTests
{
    private const long NodeA = 1;
    private const long NodeB = 2;
    private const long NodeDetour = 3;

    private const long ShortLowCapacityWay = 901; // A-B direct, 200m, 1 lane -> 2 veh/tick.
    private const long LongHighCapacityWay1 = 902; // A-Detour, part of the alternate route.
    private const long LongHighCapacityWay2 = 903; // Detour-B, part of the alternate route.

    private static RoadGraph BuildTwoRouteGraph()
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

    [Fact]
    public void AllOrNothingAssignment_NeverShiftsDemandOntoAnIdleAlternateRoute_EvenAsTheShortRouteBacksUpWithoutBound()
    {
        var roadGraph = BuildTwoRouteGraph();
        var vehicleGraph = new MobilityGraph(roadGraph, TravelMode.Vehicle);
        var queues = new LinkQueueSimulator();
        var shortLink = new LinkKey(ShortLowCapacityWay, Forward: true);
        var detourLink1 = new LinkKey(LongHighCapacityWay1, Forward: true);
        var detourLink2 = new LinkKey(LongHighCapacityWay2, Forward: true);

        const long vehiclesPerTickDemand = 6; // comfortably more than the short route's 2/tick capacity, comfortably less than the alternate route's 20/tick.
        const int ticks = 200;

        for (var tick = 0; tick < ticks; tick++)
        {
            var batches = new[] { new OdBatch(NodeA, NodeB, TravelMode.Vehicle, vehiclesPerTickDemand, "distortion-test-cohort") };
            var arrivalsByWay = NetworkDemandAssignment.AssignToWays(vehicleGraph, batches);

            queues.Step(shortLink, arrivalsByWay.TryGetValue(ShortLowCapacityWay, out var sa) ? sa : 0, capacityVehPerTick: 2);
            queues.Step(detourLink1, arrivalsByWay.TryGetValue(LongHighCapacityWay1, out var da1) ? da1 : 0, capacityVehPerTick: 20);
            queues.Step(detourLink2, arrivalsByWay.TryGetValue(LongHighCapacityWay2, out var da2) ? da2 : 0, capacityVehPerTick: 20);
        }

        var shortQueue = queues.QueueLengthOf(shortLink);
        var detourArrived1 = queues.TotalArrived.TryGetValue(detourLink1, out var t1) ? t1 : 0;
        var detourArrived2 = queues.TotalArrived.TryGetValue(detourLink2, out var t2) ? t2 : 0;

        // The measured distortion: an unbounded, ever-growing backlog on
        // the short route (exactly (6-2)*200 = 800 -- integer-exact, no
        // rerouting ever kicked in)...
        Assert.Equal((vehiclesPerTickDemand - 2) * ticks, shortQueue);

        // ...while the alternate route received ZERO arrivals across all
        // 200 ticks, despite having 10x the spare capacity that would have
        // cleared 100% of demand with room to spare.
        Assert.Equal(0, detourArrived1);
        Assert.Equal(0, detourArrived2);
    }
}
