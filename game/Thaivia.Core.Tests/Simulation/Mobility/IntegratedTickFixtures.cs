using System.Collections.Generic;
using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Mobility.Incidents;
using Thaivia.Core.Simulation.Mobility.Signals;
using Thaivia.Core.Simulation.Mobility.Transit;
using Thaivia.Core.Simulation.Scenario;
using Thaivia.Core.Tests.Simulation;

namespace Thaivia.Core.Tests.Simulation.Mobility;

/// <summary>
/// A richer, CONNECTED fixture built specifically for Task Zero's
/// integrated tick-loop tests (see g5 supervisor brief) -- unlike
/// <see cref="SimulationFixtures"/>'s two disconnected clusters (built for
/// the G3 accessibility "unreachable" case), every node here is reachable
/// from every other, with a real four-way intersection, a lane-tagged
/// bottleneck, a boundary gateway with nonzero simulation_assumption
/// demand, and enough population for OD demand to be nonzero across a
/// realistic range of hours-of-day:
///
///           N50 (200,200)
///              |  way1004 (1 lane)
///   N10 --way1001-- N20 --way1002-- N30 --way1003-- N40 (gateway)
/// (home,       (2 ln)  |(1 ln,      (2 ln)          (office, jobs)
///  residential)        |  bottleneck)
///              |  way1005 (1 lane)
///           N60 (200,-200)
///
/// Building 1000 (residential, see SimulationFixtures.ResidentialBuildingId
/// -- same id, so its archetype hash is already verified by
/// SimulationFixturesTests) sits at N10. Building 2003 (office, jobs; same
/// id as SimulationFixtures.OfficeBuildingId) sits at N40.
/// </summary>
internal static class IntegratedTickFixtures
{
    public const long N10 = 10;
    public const long N20 = 20; // the four-way intersection.
    public const long N30 = 30;
    public const long N40 = 40; // office + gateway.
    public const long N50 = 50; // north branch.
    public const long N60 = 60; // south branch.

    public const long Way1001 = 1001; // N10-N20, 2 lanes.
    public const long Way1002 = 1002; // N20-N30, 1 lane -- the bottleneck on the home->office path.
    public const long Way1003 = 1003; // N30-N40, 2 lanes.
    public const long Way1004 = 1004; // N20-N50, 1 lane.
    public const long Way1005 = 1005; // N20-N60, 1 lane.

    public const long ResidentialBuildingId = SimulationFixtures.ResidentialBuildingId;
    public const long OfficeBuildingId = SimulationFixtures.OfficeBuildingId;

    /// <summary>Signal at the four-way intersection. Its two approaches
    /// are WorldState's real topology split of N20's incident ways
    /// (sorted [1001,1002,1004,1005] -> A=[1001,1004], B=[1002,1005] --
    /// see WorldState.SplitSignalApproachWays' doc comment), not a
    /// fabricated grouping.</summary>
    public const string SignalId = "signal-intersection";

    public const string NightDisorderSiteId = "20"; // parses as N20 -- the intersection.
    public const string StreetRacingSiteId = "30"; // parses as N30 -- the open bottleneck stretch.

    public const string BusRouteId = "bus-home-office";

    public static RoadGraph BuildRoadGraph()
    {
        var nodes = new List<RoadGraphNode>
        {
            new(N10, 0, 0),
            new(N20, 200, 0),
            new(N30, 400, 0),
            new(N40, 600, 0),
            new(N50, 200, 200),
            new(N60, 200, -200),
        };

        var edges = new List<RoadEdge>
        {
            BuildEdge(Way1001, N10, N20, lanes: 2),
            BuildEdge(Way1002, N20, N30, lanes: 1),
            BuildEdge(Way1003, N30, N40, lanes: 2),
            BuildEdge(Way1004, N20, N50, lanes: 1),
            BuildEdge(Way1005, N20, N60, lanes: 1),
        };

        // Nonzero, real (well, simulation_assumption-real) demand and
        // capacity -- see Gateway's doc comment: this is documented as
        // never a source fact. 90,000 veh/hour = 5/tick at 5Hz
        // (18,000 ticks/hour); 36,000 veh/hour = 2/tick capacity, so a
        // real backlog forms rather than the gateway trivially draining
        // every tick's demand.
        var gateway = new Gateway(
            nodeId: N40, wayId: Way1003, lon: 100.6, lat: 13.75,
            inboundDemandVehPerHour: 90_000, outboundDemandVehPerHour: 90_000,
            externalCapacityVehPerHour: 36_000,
            rule: "open", @namespace: "simulation_assumption");

        var minX = 0.0;
        var minZ = -200.0;
        var maxX = 600.0;
        var maxZ = 200.0;
        return new RoadGraph(nodes, edges, new HashSet<long>(), new List<TurnRestrictionRecord>(), new List<Gateway> { gateway },
            new RoadGraphBoundary(new List<double> { minX, minZ, maxX, maxZ }, 0, new Dictionary<string, int>()));
    }

    private static RoadEdge BuildEdge(long wayId, long fromNode, long toNode, int lanes)
    {
        var tags = new SourceTags(new Dictionary<string, string> { ["lanes"] = lanes.ToString() });
        var byNode = NodeCoords();
        var coords = new List<Vec2> { byNode[fromNode], byNode[toNode] };
        return new RoadEdge(wayId, tags, new List<AssumptionRecord>(), new List<AssumptionRecord>(),
            new long[] { fromNode, toNode }, coords, coords,
            OnewayDirection.No, layer: 0, bridge: false, tunnel: false, gradeSeparated: false,
            new Dictionary<string, string>(), fromNode, toNode);
    }

    private static Dictionary<long, Vec2> NodeCoords() => new()
    {
        [N10] = new Vec2(0, 0),
        [N20] = new Vec2(200, 0),
        [N30] = new Vec2(400, 0),
        [N40] = new Vec2(600, 0),
        [N50] = new Vec2(200, 200),
        [N60] = new Vec2(200, -200),
    };

    public static GeographyBase BuildGeographyBase()
    {
        var buildings = new List<PolygonFeature>
        {
            BuildBuilding(ResidentialBuildingId, "residential", cx: 0, cz: 3),
            BuildBuilding(OfficeBuildingId, "office", cx: 600, cz: 3),
        };

        return new GeographyBase(buildings, new List<PolygonFeature>(), new List<PolygonFeature>(), new List<LineFeature>(), new List<LineFeature>());
    }

    private static PolygonFeature BuildBuilding(long sourceId, string use, double cx, double cz)
    {
        var outer = new List<Vec2>
        {
            new(cx - 1, cz - 1),
            new(cx + 1, cz - 1),
            new(cx + 1, cz + 1),
            new(cx - 1, cz + 1),
        };

        var ringGroup = new RingGroup(outer, new List<IReadOnlyList<Vec2>>());
        var tags = new SourceTags(new Dictionary<string, string> { ["building"] = use });

        return new PolygonFeature("building", "way", sourceId, tags, new List<AssumptionRecord>(), new List<AssumptionRecord>(),
            new List<RingGroup> { ringGroup }, new List<RingGroup> { ringGroup });
    }

    public static SimulationInitialization BuildSimulationInitialization() => SimulationFixtures.BuildSimulationInitialization();

    /// <summary>A fresh WorldState over this fixture, with NOTHING else
    /// registered yet (no signal/bus/incident sites) -- see
    /// <see cref="BuildFullyPopulatedWorldState"/> for one with every G4
    /// subsystem actually registered, which is what the tick-loop
    /// integration tests use.</summary>
    public static WorldState BuildWorldState(long masterSeed = 42, long initialCashThb = 5_000_000) =>
        new(BuildGeographyBase(), BuildSimulationInitialization(), BuildRoadGraph(), new ScenarioConfig(masterSeed, initialCashThb));

    /// <summary>A WorldState with every G4 subsystem actually registered
    /// (signal at the intersection, a bus route home->office, two incident
    /// sites) so a single SimulateTick call exercises the full sequence
    /// end to end -- this is the fixture the Task Zero integration tests
    /// build on.</summary>
    public static WorldState BuildFullyPopulatedWorldState(long masterSeed = 42, long initialCashThb = 5_000_000)
    {
        var world = BuildWorldState(masterSeed, initialCashThb);

        world.AddSignal(new SignalInstance(SignalId, N20, SignalPlanKind.Adaptive, cycleTicks: 20, configValue: 4, dischargeRatePerGreenTick: 3));
        world.AddBusRoute(new BusRoute(BusRouteId, new long[] { N10, N20, N40 }, dwellTicksPerStop: 2, vehicleCount: 2, capacityPerVehicle: 20));
        world.AddIncidentSite(new IncidentSite(NightDisorderSiteId, IncidentStrand.NightDisorder));
        world.AddIncidentSite(new IncidentSite(StreetRacingSiteId, IncidentStrand.StreetRacing));

        return world;
    }
}
