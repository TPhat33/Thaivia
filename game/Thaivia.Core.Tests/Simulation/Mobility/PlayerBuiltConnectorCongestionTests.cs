using System.Collections.Generic;
using System.Linq;
using Thaivia.Core.Simulation.Accessibility;
using Thaivia.Core.Simulation.Mobility.Demand;
using Thaivia.Core.Simulation.Mobility.Queues;
using Thaivia.Core.Simulation.Mobility.Routing;
using Xunit;
using Xunit.Abstractions;

namespace Thaivia.Core.Tests.Simulation.Mobility;

/// <summary>
/// Task 2 (wave 9, ADR-0040): re-runs the exact demand-split shape
/// <see cref="CongestionAwareAssignmentTests"/> already proved for two REAL
/// ways, but with the high-capacity alternative built as a player
/// <see cref="PlannedRoadSegment"/> instead -- proving the fix closes the
/// gap ADR-0022/ADR-0031 documented ("a connector's synthetic way id is
/// never charged capacity/congestion, so it looks like a free, infinite-
/// capacity road"). Same network shape as <see cref="TwoRouteAssignmentFixtures"/>
/// (a short low-capacity direct way vs. a longer, higher-capacity
/// alternative) so the before/after story is directly comparable.
/// </summary>
public class PlayerBuiltConnectorCongestionTests
{
    private readonly ITestOutputHelper _output;

    public PlayerBuiltConnectorCongestionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const long NodeA = TwoRouteAssignmentFixtures.NodeA;
    private const long NodeB = TwoRouteAssignmentFixtures.NodeB;
    private static readonly LinkKey ShortLink = new(TwoRouteAssignmentFixtures.ShortLowCapacityWay, Forward: true);

    /// <summary>Short real way A-B (200m, 1 lane -&gt; 2 veh/tick, same
    /// numbers as <see cref="TwoRouteAssignmentFixtures"/>) plus ONE
    /// player-built <see cref="PlannedRoadSegment"/> also connecting A-B
    /// directly, slightly LONGER (220m, so it is not preferred at zero
    /// congestion) but built with <see cref="RoadPreset.Arterial"/> (4
    /// lanes -&gt; 8 veh/tick, 4x the short way's capacity).</summary>
    private static (MobilityGraph VehicleGraph, PlannedRoadSegment Connector) BuildGraphWithPlayerConnector()
    {
        var roadGraph = TwoRouteAssignmentFixtures.BuildTwoRouteGraphShortWayOnly();
        var connector = new PlannedRoadSegment(NodeA, NodeB, LengthMeters: 220, ProjectId: "player-road-1", Preset: RoadPreset.Arterial);
        var vehicleGraph = new MobilityGraph(roadGraph, TravelMode.Vehicle, new[] { connector });
        return (vehicleGraph, connector);
    }

    private static Dictionary<long, int> CapacityByWayId(MobilityGraph vehicleGraph)
    {
        var capacity = new Dictionary<long, int> { [TwoRouteAssignmentFixtures.ShortLowCapacityWay] = 2 };
        foreach (var (wayId, segment) in vehicleGraph.PlannedRoadSegmentWayIds)
        {
            capacity[wayId] = RoadPresetCatalog.CapacityVehPerTick(segment.Preset);
        }

        return capacity;
    }

    [Fact]
    public void PlayerBuiltConnector_NowParticipatesInAssignmentAndCapacity_NotExcludedAsSynthetic()
    {
        var (vehicleGraph, connector) = BuildGraphWithPlayerConnector();
        var connectorWayId = vehicleGraph.PlannedRoadSegmentWayIds.Keys.Single();
        Assert.True(connectorWayId < 0); // still a synthetic id -- that part is unchanged.

        var capacityByWayId = CapacityByWayId(vehicleGraph);
        Assert.Equal(8, capacityByWayId[connectorWayId]); // RoadPreset.Arterial: 4 lanes x 2 veh/tick/lane.

        var batches = new[] { new OdBatch(NodeA, NodeB, TravelMode.Vehicle, VehicleCount: 6, CohortId: "solo-cohort") };
        var priorQueueLengthByWay = new Dictionary<long, long> { [TwoRouteAssignmentFixtures.ShortLowCapacityWay] = 0, [connectorWayId] = 0 };
        var arrivalsByWay = NetworkDemandAssignment.AssignToWaysCongestionAware(vehicleGraph, batches, capacityByWayId, priorQueueLengthByWay);

        // The connector actually received arrivals -- it is a real,
        // chargeable route option, not silently dropped.
        Assert.True(arrivalsByWay.ContainsKey(connectorWayId));
        Assert.Equal(6, arrivalsByWay[TwoRouteAssignmentFixtures.ShortLowCapacityWay] + arrivalsByWay[connectorWayId]); // conservation.
    }

    /// <summary>The actual congestion proof: demand well above the short
    /// way's capacity but within the player connector's must produce a
    /// REAL, measured diversion onto the connector, and -- the specific
    /// failure this gap caused -- the connector must actually accumulate a
    /// queue backlog once ITS OWN capacity is exceeded, rather than
    /// silently absorbing unlimited traffic for free.</summary>
    [Fact]
    public void PlayerBuiltConnector_CarriesAMeaningfulShareOfDemand_AndQueuesWhenOverloaded_LikeASourceWayWould()
    {
        var (vehicleGraph, _) = BuildGraphWithPlayerConnector();
        var connectorWayId = vehicleGraph.PlannedRoadSegmentWayIds.Keys.Single();
        var connectorLink = new LinkKey(connectorWayId, Forward: true);
        var capacityByWayId = CapacityByWayId(vehicleGraph);
        var queues = new LinkQueueSimulator();

        // 12 veh/tick: well above the short way's 2/tick AND above the
        // connector's own 8/tick -- so BOTH must eventually queue, but the
        // connector should absorb the larger share (it has 4x capacity).
        const long vehiclesPerTickDemand = 12;
        const int ticks = 200;

        for (var tick = 0; tick < ticks; tick++)
        {
            var priorQueueLengthByWay = new Dictionary<long, long>
            {
                [TwoRouteAssignmentFixtures.ShortLowCapacityWay] = queues.QueueLengthOf(ShortLink),
                [connectorWayId] = queues.QueueLengthOf(connectorLink),
            };

            var batches = new[] { new OdBatch(NodeA, NodeB, TravelMode.Vehicle, vehiclesPerTickDemand, "player-connector-cohort") };
            var arrivalsByWay = NetworkDemandAssignment.AssignToWaysCongestionAware(vehicleGraph, batches, capacityByWayId, priorQueueLengthByWay);

            queues.Step(ShortLink, arrivalsByWay.TryGetValue(TwoRouteAssignmentFixtures.ShortLowCapacityWay, out var sa) ? sa : 0, capacityVehPerTick: 2);
            queues.Step(connectorLink, arrivalsByWay.TryGetValue(connectorWayId, out var ca) ? ca : 0, capacityVehPerTick: 8);
        }

        var shortQueue = queues.QueueLengthOf(ShortLink);
        var connectorQueue = queues.QueueLengthOf(connectorLink);
        var shortArrived = queues.TotalArrived.TryGetValue(ShortLink, out var s) ? s : 0;
        var connectorArrived = queues.TotalArrived.TryGetValue(connectorLink, out var c) ? c : 0;
        var totalDemand = vehiclesPerTickDemand * ticks;

        _output.WriteLine($"Total demand over {ticks} ticks: {totalDemand}");
        _output.WriteLine($"Short real way: {shortArrived} arrivals, ending backlog {shortQueue}");
        _output.WriteLine($"Player-built connector: {connectorArrived} arrivals, ending backlog {connectorQueue}");
        _output.WriteLine($"Connector share of total demand: {(double)connectorArrived / totalDemand:P1}");

        // Conservation: nothing lost, nothing invented.
        Assert.Equal(totalDemand, shortArrived + connectorArrived);

        // The fix: the player-built connector carries a real, meaningful
        // (majority, given its 4x capacity) share of demand.
        Assert.True(connectorArrived > totalDemand / 2, $"expected the higher-capacity player connector to carry the majority of demand, got {connectorArrived}/{totalDemand}.");

        // The specific bug this closes: the connector is NOT free/infinite
        // capacity -- once demand exceeds ITS 8/tick too, it must actually
        // accumulate a real backlog, exactly like a source way would (see
        // GatewayConservationTests/CongestionAwareAssignmentTests for the
        // same "a queue is real state, not a cosmetic number" discipline
        // applied elsewhere).
        Assert.True(connectorQueue > 0, "expected the player-built connector to accumulate a real queue backlog once its own capacity was exceeded -- a zero backlog here would mean it is still being treated as free/infinite capacity.");
    }
}
