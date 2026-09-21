using System.Collections.Generic;
using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Mobility.Incidents;
using Thaivia.Core.Simulation.Mobility.Queues;
using Thaivia.Core.Simulation.Mobility.RoadWorks;
using Thaivia.Core.Simulation.Mobility.Signals;
using Thaivia.Core.Simulation.Mobility.Transit;
using Thaivia.Core.Simulation.Save;
using Thaivia.Core.Tests.Simulation;
using Xunit;

namespace Thaivia.Core.Tests.Simulation.Mobility;

/// <summary>
/// The save round-trip test the supervising engineer asked for by name:
/// proves G4's new state (network queues, gateway conservation ledgers,
/// road works, bus routes/ridership, signal state, incident state) all
/// survive a save/restore round trip exactly -- not just that the JSON
/// parses, but that every field is bit-for-bit identical afterward.
/// </summary>
public class MobilitySaveRoundTripTests
{
    private const string MapId = "test-map";
    private const string MapContentHash = "sha256:deadbeef";

    /// <summary>Same shape as SimulationFixtures.BuildRoadGraph, but with
    /// one Gateway added at node 5 with a high enough hourly capacity to
    /// produce a nonzero per-tick capacity (WorldState derives
    /// GatewayFlow capacity from this at construction time).</summary>
    private static RoadGraph BuildRoadGraphWithGateway()
    {
        var nodes = new List<RoadGraphNode>
        {
            new(SimulationFixtures.Node1, 0, 0),
            new(SimulationFixtures.Node2, 10, 0),
            new(SimulationFixtures.Node3, 12, 0),
            new(SimulationFixtures.Node4, 22, 0),
            new(SimulationFixtures.Node5, 32, 0),
        };

        var coordsAB = new List<Vec2> { new(0, 0), new(10, 0) };
        var coordsCDE = new List<Vec2> { new(12, 0), new(22, 0), new(32, 0) };
        var edges = new List<RoadEdge>
        {
            new(101, SourceTags.Empty, new List<AssumptionRecord>(), new List<AssumptionRecord>(),
                new long[] { SimulationFixtures.Node1, SimulationFixtures.Node2 }, coordsAB, coordsAB,
                OnewayDirection.No, layer: 0, bridge: false, tunnel: false, gradeSeparated: false,
                new Dictionary<string, string>(), SimulationFixtures.Node1, SimulationFixtures.Node2),
            new(102, SourceTags.Empty, new List<AssumptionRecord>(), new List<AssumptionRecord>(),
                new long[] { SimulationFixtures.Node3, SimulationFixtures.Node4, SimulationFixtures.Node5 }, coordsCDE, coordsCDE,
                OnewayDirection.No, layer: 0, bridge: false, tunnel: false, gradeSeparated: false,
                new Dictionary<string, string>(), SimulationFixtures.Node3, SimulationFixtures.Node5),
        };

        var gateway = new Gateway(
            nodeId: SimulationFixtures.Node5, wayId: 102, lon: 100.5, lat: 13.7,
            inboundDemandVehPerHour: 100, outboundDemandVehPerHour: 100,
            externalCapacityVehPerHour: 36000, // -> 2 veh/tick at 5Hz (18000 ticks/hour).
            rule: "open", @namespace: "simulation_assumption");

        return new RoadGraph(nodes, edges, new HashSet<long>(), new List<TurnRestrictionRecord>(), new List<Gateway> { gateway },
            new RoadGraphBoundary(new List<double> { 0, 0, 32, 0 }, 0, new Dictionary<string, int>()));
    }

    private static WorldState BuildWorldState(long masterSeed) =>
        new(SimulationFixtures.BuildGeographyBase(), SimulationFixtures.BuildSimulationInitialization(), BuildRoadGraphWithGateway(),
            new Thaivia.Core.Simulation.Scenario.ScenarioConfig(masterSeed, 5_000_000));

    private static void PopulateMobilityState(WorldState world)
    {
        // Network queues: back up a real backlog.
        var link = new LinkKey(101, Forward: true);
        for (var i = 0; i < 5; i++)
        {
            world.LinkQueues.Step(link, arrivals: 9, capacityVehPerTick: 4);
        }

        // Gateway: generate demand, close it partway through so a real
        // backlog forms (see ADR-0019).
        var gateway = world.GatewayFlows[SimulationFixtures.Node5];
        gateway.GenerateOutboundDemand(50);
        gateway.GenerateInboundDemand(10);
        gateway.Step();
        gateway.Close();
        gateway.Step();
        gateway.Step();

        // Road works.
        world.AddRoadWorksZone(new RoadWorksZone("works-1", wayId: 101, startTick: 2, durationTicks: 20, capacityMultiplierDuringConstruction: 0.4, projectId: "proj-rw"));

        // Bus route + ridership.
        var route = new BusRoute("route-1", new long[] { SimulationFixtures.Node1, SimulationFixtures.Node2 }, dwellTicksPerStop: 2, vehicleCount: 3, capacityPerVehicle: 30);
        world.AddBusRoute(route);
        world.RecordBusRidership("route-1", 17);
        world.RecordBusRidership("route-1", 5);

        // Signal.
        var signal = new SignalInstance("signal-1", SimulationFixtures.Node2, SignalPlanKind.Adaptive, cycleTicks: 20, configValue: 4, dischargeRatePerGreenTick: 4);
        signal.Step(arrivalsA: 5, arrivalsB: 1);
        signal.Step(arrivalsA: 5, arrivalsB: 1);
        signal.Step(arrivalsA: 5, arrivalsB: 1);
        world.AddSignal(signal);

        // Incident site, mid-Warning (not yet Active) so the intermediate
        // state itself has to survive, not just a "resting" Idle state.
        var site = new IncidentSite("site-1", IncidentStrand.NightDisorder);
        var thresholds = new IncidentThresholds(RiskThreshold: 0.5, WarningLeadTicks: 5, DurationTicks: 10, CooldownTicks: 20);
        IncidentEngine.Step(site, risk: 0.9, thresholds); // Idle -> Warning.
        IncidentEngine.Step(site, risk: 0.9, thresholds); // advance within Warning.
        world.AddIncidentSite(site);
    }

    [Fact]
    public void AllG4MobilityState_SurvivesASaveRestoreRoundTrip_FieldByField()
    {
        var world = BuildWorldState(masterSeed: 777);
        PopulateMobilityState(world);

        var save = world.CaptureSave(MapId, MapContentHash, "0.1.0", "0.1.0");
        var json = SaveSerializer.Serialize(save);
        var roundTripped = SaveSerializer.Deserialize(json);
        var restored = WorldState.Restore(roundTripped, SimulationFixtures.BuildGeographyBase(), SimulationFixtures.BuildSimulationInitialization(), BuildRoadGraphWithGateway(),
            new Thaivia.Core.Simulation.Scenario.ScenarioConfig(777, 5_000_000));

        // Network queues.
        var link = new LinkKey(101, Forward: true);
        Assert.Equal(world.LinkQueues.QueueLengthOf(link), restored.LinkQueues.QueueLengthOf(link));
        Assert.Equal(world.LinkQueues.TotalCompleted[link], restored.LinkQueues.TotalCompleted[link]);
        Assert.Equal(world.LinkQueues.TotalArrived[link], restored.LinkQueues.TotalArrived[link]);
        Assert.Equal(25, restored.LinkQueues.QueueLengthOf(link)); // sanity: (9-4)*5.

        // Gateway conservation ledger.
        var originalGateway = world.GatewayFlows[SimulationFixtures.Node5];
        var restoredGateway = restored.GatewayFlows[SimulationFixtures.Node5];
        Assert.Equal(originalGateway.IsOpen, restoredGateway.IsOpen);
        Assert.False(restoredGateway.IsOpen); // sanity: it was closed before saving.
        Assert.Equal(originalGateway.OutboundLedger, restoredGateway.OutboundLedger);
        Assert.Equal(originalGateway.InboundLedger, restoredGateway.InboundLedger);
        Assert.True(restoredGateway.OutboundLedger.IsConserved); // conservation survives the round trip too.

        // Road works.
        Assert.Equal(world.RoadWorksZones.Count, restored.RoadWorksZones.Count);
        var restoredZone = restored.RoadWorksZones[0];
        Assert.Equal("works-1", restoredZone.Id);
        Assert.Equal(101, restoredZone.WayId);
        Assert.Equal(2, restoredZone.StartTick);
        Assert.Equal(20, restoredZone.DurationTicks);
        Assert.Equal(0.4, restoredZone.CapacityMultiplierDuringConstruction, precision: 6);

        // Bus route + cumulative ridership.
        Assert.Equal(world.BusRoutes.Count, restored.BusRoutes.Count);
        var restoredRoute = restored.BusRoutes[0];
        Assert.Equal("route-1", restoredRoute.Id);
        Assert.Equal(3, restoredRoute.VehicleCount);
        Assert.Equal(22, restored.BusRidershipOf("route-1")); // 17 + 5.
        Assert.Equal(world.BusRidershipOf("route-1"), restored.BusRidershipOf("route-1"));

        // Signal (adaptive, mid-cycle state).
        Assert.Equal(world.Signals.Count, restored.Signals.Count);
        var originalSignal = world.Signals[0];
        var restoredSignal = restored.Signals[0];
        Assert.Equal(originalSignal.Id, restoredSignal.Id);
        Assert.Equal(originalSignal.Kind, restoredSignal.Kind);
        Assert.Equal(originalSignal.Simulator.QueueA, restoredSignal.Simulator.QueueA);
        Assert.Equal(originalSignal.Simulator.QueueB, restoredSignal.Simulator.QueueB);
        Assert.Equal(originalSignal.Simulator.TicksSimulated, restoredSignal.Simulator.TicksSimulated);
        Assert.Equal(originalSignal.Simulator.CumulativeQueueTicksA, restoredSignal.Simulator.CumulativeQueueTicksA);
        // Continuing to step both in lockstep from here must keep matching exactly.
        originalSignal.Step(3, 2);
        restoredSignal.Step(3, 2);
        Assert.Equal(originalSignal.Simulator.QueueA, restoredSignal.Simulator.QueueA);
        Assert.Equal(originalSignal.Simulator.CurrentGreenTicksA, restoredSignal.Simulator.CurrentGreenTicksA);

        // Incident site -- must resume exactly mid-Warning, not reset to Idle.
        Assert.Equal(world.IncidentSites.Count, restored.IncidentSites.Count);
        var originalSite = world.IncidentSites[0];
        var restoredSite = restored.IncidentSites[0];
        Assert.Equal(IncidentPhase.Warning, restoredSite.Phase);
        Assert.Equal(originalSite.Phase, restoredSite.Phase);
        Assert.Equal(originalSite.TicksInPhase, restoredSite.TicksInPhase);
        Assert.Equal(originalSite.WarningsIssued, restoredSite.WarningsIssued);
        Assert.Equal(originalSite.Strand, restoredSite.Strand);

        // And the overall structural hash agrees too -- the single number
        // everything else in this codebase's determinism tests key off.
        Assert.Equal(world.ComputeStructuralHash(), restored.ComputeStructuralHash());
    }

    /// <summary>Determinism must hold with G4 state in play too: two
    /// worlds built from the same seed, fed the exact same mobility
    /// commands, must hash identically.</summary>
    [Fact]
    public void TwoWorlds_SameSeedSameMobilityCommands_ProduceIdenticalStructuralHash()
    {
        var worldA = BuildWorldState(masterSeed: 4242);
        var worldB = BuildWorldState(masterSeed: 4242);

        PopulateMobilityState(worldA);
        PopulateMobilityState(worldB);

        Assert.Equal(worldA.ComputeStructuralHash(), worldB.ComputeStructuralHash());
    }
}
