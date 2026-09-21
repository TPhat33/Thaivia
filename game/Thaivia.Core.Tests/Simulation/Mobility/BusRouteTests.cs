using System;
using System.Collections.Generic;
using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation.Cohorts;
using Thaivia.Core.Simulation.Mobility.Routing;
using Thaivia.Core.Simulation.Mobility.Transit;
using Xunit;

namespace Thaivia.Core.Tests.Simulation.Mobility;

public class BusRouteTests
{
    private static RoadEdge MakeEdge(long wayId, long from, long to)
    {
        var nodeRefs = new List<long> { from, to };
        var coords = new List<Vec2> { new(0, 0), new(1, 1) };
        return new RoadEdge(wayId, new SourceTags(new Dictionary<string, string>()), new List<AssumptionRecord>(), new List<AssumptionRecord>(),
            nodeRefs, coords, coords, OnewayDirection.No, layer: 0, bridge: false, tunnel: false, gradeSeparated: false,
            accessModes: new Dictionary<string, string>(), fromNode: from, toNode: to);
    }

    /// <summary>Three stops on a straight line: 0 -(100m)- 1 -(100m)- 2.</summary>
    private static RoadGraph BuildLineGraph()
    {
        var nodes = new List<RoadGraphNode> { new(1, 0, 0), new(2, 100, 0), new(3, 200, 0) };
        var edges = new List<RoadEdge> { MakeEdge(1, 1, 2), MakeEdge(2, 2, 3) };
        return new RoadGraph(nodes, edges, new HashSet<long>(), new List<TurnRestrictionRecord>(), new List<Gateway>(),
            new RoadGraphBoundary(new List<double> { 0, 0, 200, 0 }, 0, new Dictionary<string, int>()));
    }

    [Fact]
    public void VehicleCount_MustBeAPositiveFiniteCap()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BusRoute("r1", new long[] { 1, 2, 3 }, dwellTicksPerStop: 2, vehicleCount: 0, capacityPerVehicle: 20));
    }

    [Fact]
    public void RoundTripTicks_ThrowsWhenAStopIsUnreachableOnTheVehicleGraph()
    {
        var graph = BuildLineGraph();
        var route = new BusRoute("r1", new long[] { 1, 3, 999 }, dwellTicksPerStop: 2, vehicleCount: 2, capacityPerVehicle: 20);
        var vehicleGraph = new MobilityGraph(graph, TravelMode.Vehicle);
        Assert.Throws<InvalidOperationException>(() => BusRouteScheduler.RoundTripTicks(route, vehicleGraph));
    }

    /// <summary>The test the supervising engineer asked for: dwell time
    /// adds real cost -- a longer dwell strictly increases round-trip
    /// ticks and therefore strictly decreases throughput per tick.</summary>
    [Fact]
    public void IncreasingDwellTime_IncreasesRoundTripTicks_AndDecreasesThroughput()
    {
        var graph = BuildLineGraph();
        var vehicleGraph = new MobilityGraph(graph, TravelMode.Vehicle);
        var shortDwell = new BusRoute("r1", new long[] { 1, 2, 3 }, dwellTicksPerStop: 1, vehicleCount: 2, capacityPerVehicle: 20);
        var longDwell = new BusRoute("r2", new long[] { 1, 2, 3 }, dwellTicksPerStop: 10, vehicleCount: 2, capacityPerVehicle: 20);

        var shortRoundTrip = BusRouteScheduler.RoundTripTicks(shortDwell, vehicleGraph);
        var longRoundTrip = BusRouteScheduler.RoundTripTicks(longDwell, vehicleGraph);
        Assert.True(longRoundTrip > shortRoundTrip);

        var shortThroughput = BusRouteScheduler.ThroughputPerTick(shortDwell, shortRoundTrip);
        var longThroughput = BusRouteScheduler.ThroughputPerTick(longDwell, longRoundTrip);
        Assert.True(longThroughput < shortThroughput);
    }

    [Fact]
    public void Ridership_IsDrawnFromCohortPopulationBatches_NotPerAgentSpawning()
    {
        var cohorts = new List<HouseholdCohort>
        {
            new("cohort-a", homeBuildingSourceId: 1, householdCount: 100, peoplePerHousehold: 3, jobsHeld: 0),
            new("cohort-b", homeBuildingSourceId: 2, householdCount: 50, peoplePerHousehold: 4, jobsHeld: 0),
        };

        // Only cohort-a is "within walking reach" for this test.
        var demand = BusRidership.ComputeEligibleDemandThisTick(cohorts, c => c.Id == "cohort-a", modeShare: 0.1);

        // 100 households * 3 people * 0.1 mode share = 30 -- derived from
        // the cohort's POPULATION total, never from a count of spawned
        // rider objects (there are none here at all).
        Assert.Equal(30, demand);
    }

    [Fact]
    public void Ridership_IsBoundedByFleetCapacity_OverflowIsReportedNotDropped()
    {
        var (boarded, overflow) = BusRidership.AssignRidership(demandThisTick: 500, throughputPerTick: 12.5);
        Assert.Equal(12, boarded); // floor(12.5).
        Assert.Equal(488, overflow);
        Assert.Equal(500, boarded + overflow); // nothing vanished.
    }

    [Fact]
    public void Ridership_BelowCapacity_AllDemandBoards()
    {
        var (boarded, overflow) = BusRidership.AssignRidership(demandThisTick: 5, throughputPerTick: 12.5);
        Assert.Equal(5, boarded);
        Assert.Equal(0, overflow);
    }
}
