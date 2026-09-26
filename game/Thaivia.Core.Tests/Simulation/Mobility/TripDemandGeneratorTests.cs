using System.Collections.Generic;
using Thaivia.Core.Simulation.Archetypes;
using Thaivia.Core.Simulation.Buildings;
using Thaivia.Core.Simulation.Cohorts;
using Thaivia.Core.Simulation.Mobility.Demand;
using Thaivia.Core.Simulation.Mobility.Routing;
using Thaivia.Core.Tests.Simulation;
using Xunit;

namespace Thaivia.Core.Tests.Simulation.Mobility;

public class TripDemandGeneratorTests
{
    [Fact]
    public void OneCohort_ProducesExactlyOneBatch_NotOnePerPerson()
    {
        var home = new BuildingSimState(SimulationFixtures.ResidentialBuildingId, BuildingArchetype.Residential, 0, 0, SimulationFixtures.Node1, jobsCount: 0, relocated: false);
        var office = new BuildingSimState(SimulationFixtures.OfficeBuildingId, BuildingArchetype.Office, 32, 0, SimulationFixtures.Node5, jobsCount: 20, relocated: false);
        var buildingStates = new Dictionary<long, BuildingSimState> { [home.SourceId] = home, [office.SourceId] = office };

        // A large cohort -- 500 households x 4 people = 2000 people.
        var cohort = new HouseholdCohort("cohort-big", home.SourceId, householdCount: 500, peoplePerHousehold: 4, jobsHeld: 0);
        var cohorts = new List<HouseholdCohort> { cohort };

        // Bridge the two clusters so a route exists at all (reuse the
        // AccessibilityGraphTests-style adjacency via a planned connector).
        var connector = new Thaivia.Core.Simulation.Accessibility.PlannedRoadSegment(SimulationFixtures.Node2, SimulationFixtures.Node3, 2.0, "bridge");
        var vehicleGraph = new MobilityGraph(SimulationFixtures.BuildRoadGraph(), TravelMode.Vehicle, new[] { connector });

        var batches = TripDemandGenerator.GenerateCommuteBatches(cohorts, buildingStates, vehicleGraph, hourOfDay: 8);

        Assert.Single(batches); // one batch, not 2000 individual records.
        var batch = batches[0];
        Assert.Equal(SimulationFixtures.Node1, batch.OriginNodeId);
        Assert.Equal(SimulationFixtures.Node5, batch.DestinationNodeId);
        Assert.Equal("cohort-big", batch.CohortId);
        Assert.True(batch.VehicleCount > 0);
        // The batch's size is derived from population (2000), never equal
        // to a per-agent count of 1.
        Assert.True(batch.VehicleCount < 2000);
    }

    [Fact]
    public void UnreachableDestination_ProducesNoBatch_NotAZeroDistanceGuess()
    {
        var home = new BuildingSimState(SimulationFixtures.ResidentialBuildingId, BuildingArchetype.Residential, 0, 0, SimulationFixtures.Node1, jobsCount: 0, relocated: false);
        var office = new BuildingSimState(SimulationFixtures.OfficeBuildingId, BuildingArchetype.Office, 32, 0, SimulationFixtures.Node5, jobsCount: 20, relocated: false);
        var buildingStates = new Dictionary<long, BuildingSimState> { [home.SourceId] = home, [office.SourceId] = office };
        var cohort = new HouseholdCohort("cohort-1", home.SourceId, householdCount: 10, peoplePerHousehold: 3, jobsHeld: 0);

        // No connector this time -- the two clusters stay disconnected.
        var vehicleGraph = new MobilityGraph(SimulationFixtures.BuildRoadGraph(), TravelMode.Vehicle);
        var batches = TripDemandGenerator.GenerateCommuteBatches(new[] { cohort }, buildingStates, vehicleGraph, hourOfDay: 8);

        Assert.Empty(batches);
    }

    [Fact]
    public void NetworkDemandAssignment_AddsTheBatchsFullVehicleCount_ToEveryWayOnItsShortestPath()
    {
        var connector = new Thaivia.Core.Simulation.Accessibility.PlannedRoadSegment(SimulationFixtures.Node2, SimulationFixtures.Node3, 2.0, "bridge");
        var vehicleGraph = new MobilityGraph(SimulationFixtures.BuildRoadGraph(), TravelMode.Vehicle, new[] { connector });

        var batches = new List<OdBatch> { new(SimulationFixtures.Node1, SimulationFixtures.Node5, TravelMode.Vehicle, VehicleCount: 40, CohortId: "cohort-1") };
        var arrivals = NetworkDemandAssignment.AssignToWaysAllOrNothing(vehicleGraph, batches);

        // The path node1->node2 (way 101) -> node3->node4->node5 (way 102)
        // -- the synthetic bridge connector's negative id is excluded.
        Assert.Equal(40, arrivals[101]);
        Assert.Equal(40, arrivals[102]);
        Assert.DoesNotContain(arrivals.Keys, k => k < 0);
    }

    [Fact]
    public void NetworkDemandAssignment_MultipleBatchesOnTheSameWay_Accumulate()
    {
        var vehicleGraph = new MobilityGraph(SimulationFixtures.BuildRoadGraph(), TravelMode.Vehicle);
        var batches = new List<OdBatch>
        {
            new(SimulationFixtures.Node1, SimulationFixtures.Node2, TravelMode.Vehicle, 10, "cohort-a"),
            new(SimulationFixtures.Node2, SimulationFixtures.Node1, TravelMode.Vehicle, 15, "cohort-b"),
        };

        var arrivals = NetworkDemandAssignment.AssignToWaysAllOrNothing(vehicleGraph, batches);
        Assert.Equal(25, arrivals[101]); // both batches use way 101, opposite directions, aggregated per-way.
    }
}
