using System.Collections.Generic;
using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Mobility.Incidents;
using Thaivia.Core.Simulation.Mobility.Signals;
using Thaivia.Core.Simulation.Mobility.Transit;
using Thaivia.Core.Simulation.Scenario;

namespace Thaivia.Core.Tests.Simulation.Mobility;

/// <summary>
/// A procedurally generated grid network, used ONLY to measure per-tick
/// performance (spec §15). This is explicitly NOT the real pilot AOI --
/// no real Thai OSM data has been imported yet (ADR-0003) -- so every
/// number this fixture's benchmark produces is scoped to "a synthetic
/// network of this size", stated plainly in the benchmark's own output
/// and in docs/progress.md, never presented as a measurement of the real
/// pilot area.
///
/// Grid shape: <see cref="GridSize"/> x <see cref="GridSize"/> nodes,
/// 20m spacing, one edge per horizontal/vertical neighbour pair (a
/// Manhattan grid, not a realistic street layout -- chosen because it is
/// cheap to generate deterministically at any size and gives every node
/// 2-4 incident ways, close to what a real dense urban grid gives a
/// routing graph to chew on). A residential or office building sits on
/// every other node in a checkerboard pattern, so roughly half the nodes
/// seed a household cohort and the other half carry jobs -- this is what
/// drives OD demand volume/shape at benchmark time, since
/// TripDemandGenerator/NetworkDemandAssignment cost scales with
/// (cohort count x job-bearing building count) Dijkstra runs per tick.
/// </summary>
internal static class PerformanceBenchmarkFixtures
{
    public const int GridSize = 8; // 64 nodes, 112 edges -- see BuildFullyPopulatedWorldState's doc comment for why this size.
    public const double SpacingMeters = 20.0;

    private static long NodeId(int i, int j) => 1000L + (i * GridSize) + j;

    private static long HorizontalWayId(int i, int j) => 200_000L + (i * GridSize) + j; // edge (i,j)-(i,j+1).
    private static long VerticalWayId(int i, int j) => 300_000L + (i * GridSize) + j; // edge (i,j)-(i+1,j).

    public static RoadGraph BuildRoadGraph()
    {
        var nodes = new List<RoadGraphNode>();
        for (var i = 0; i < GridSize; i++)
        {
            for (var j = 0; j < GridSize; j++)
            {
                nodes.Add(new RoadGraphNode(NodeId(i, j), i * SpacingMeters, j * SpacingMeters));
            }
        }

        var edges = new List<RoadEdge>();
        for (var i = 0; i < GridSize; i++)
        {
            for (var j = 0; j < GridSize; j++)
            {
                if (j + 1 < GridSize)
                {
                    edges.Add(BuildEdge(HorizontalWayId(i, j), NodeId(i, j), NodeId(i, j + 1), lanes: (i + j) % 3 == 0 ? 2 : 1));
                }

                if (i + 1 < GridSize)
                {
                    edges.Add(BuildEdge(VerticalWayId(i, j), NodeId(i, j), NodeId(i + 1, j), lanes: (i + j) % 4 == 0 ? 2 : 1));
                }
            }
        }

        // One boundary gateway at a corner, with real (if synthetic)
        // simulation_assumption demand -- see IntegratedTickFixtures for
        // the same conversion convention.
        var gateway = new Gateway(
            nodeId: NodeId(0, 0), wayId: HorizontalWayId(0, 0), lon: 100.5, lat: 13.7,
            inboundDemandVehPerHour: 72_000, outboundDemandVehPerHour: 72_000,
            externalCapacityVehPerHour: 36_000,
            rule: "open", @namespace: "simulation_assumption");

        var maxCoord = (GridSize - 1) * SpacingMeters;
        return new RoadGraph(nodes, edges, new HashSet<long>(), new List<TurnRestrictionRecord>(), new List<Gateway> { gateway },
            new RoadGraphBoundary(new List<double> { 0, 0, maxCoord, maxCoord }, 0, new Dictionary<string, int>()));
    }

    private static RoadEdge BuildEdge(long wayId, long fromNode, long toNode, int lanes)
    {
        var tags = new SourceTags(new Dictionary<string, string> { ["lanes"] = lanes.ToString() });
        var coords = new List<Vec2> { new(0, 0), new(0, 0) }; // geometry is not read for this benchmark's routing (only node coordinates are, via RoadGraphNode) -- placeholder values are fine here.
        return new RoadEdge(wayId, tags, new List<AssumptionRecord>(), new List<AssumptionRecord>(),
            new long[] { fromNode, toNode }, coords, coords,
            OnewayDirection.No, layer: 0, bridge: false, tunnel: false, gradeSeparated: false,
            new Dictionary<string, string>(), fromNode, toNode);
    }

    public static GeographyBase BuildGeographyBase()
    {
        var buildings = new List<PolygonFeature>();
        for (var i = 0; i < GridSize; i++)
        {
            for (var j = 0; j < GridSize; j++)
            {
                if ((i + j) % 2 != 0)
                {
                    continue; // checkerboard: only every other node carries a building.
                }

                var sourceId = 900_000L + (i * GridSize) + j;
                var use = (i * GridSize + j) % 5 == 0 ? "residential" : "office"; // mostly office/jobs, some residential -- tag itself is not read by AssignArchetype (which hashes source id), only used for realism of the fixture's own tags.
                buildings.Add(BuildBuilding(sourceId, use, cx: i * SpacingMeters, cz: j * SpacingMeters));
            }
        }

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

    public static SimulationInitialization BuildSimulationInitialization()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("{}");
        return new SimulationInitialization("0.1.0", "g5 performance benchmark fixture -- fictional, not real Thai statistics, not the real pilot AOI.", doc.RootElement.Clone());
    }

    /// <summary>A WorldState over this grid with a modest number of
    /// signals/bus routes/incident sites registered -- proportional to
    /// grid size, never a fixed constant, so this stays representative if
    /// GridSize changes.</summary>
    public static WorldState BuildFullyPopulatedWorldState(long masterSeed = 2026, long initialCashThb = 50_000_000)
    {
        var world = new WorldState(BuildGeographyBase(), BuildSimulationInitialization(), BuildRoadGraph(), new ScenarioConfig(masterSeed, initialCashThb));

        // Signals at every third intersection along the diagonal-ish interior.
        var signalCount = 0;
        for (var i = 1; i < GridSize - 1; i += 3)
        {
            for (var j = 1; j < GridSize - 1; j += 3)
            {
                world.AddSignal(new SignalInstance($"signal-{i}-{j}", NodeId(i, j), SignalPlanKind.Adaptive, cycleTicks: 20, configValue: 4, dischargeRatePerGreenTick: 3));
                signalCount++;
            }
        }

        // One long bus route across the grid's main diagonal-ish spine.
        var stops = new List<long>();
        for (var i = 0; i < GridSize; i += 2)
        {
            stops.Add(NodeId(i, i % GridSize == GridSize - 1 ? GridSize - 2 : i));
        }

        if (stops.Count < 2)
        {
            stops = new List<long> { NodeId(0, 0), NodeId(GridSize - 1, GridSize - 1) };
        }

        world.AddBusRoute(new BusRoute("bus-spine", stops, dwellTicksPerStop: 2, vehicleCount: 4, capacityPerVehicle: 30));

        // A handful of incident sites spread across the grid.
        world.AddIncidentSite(new IncidentSite(NodeId(1, 1).ToString(), IncidentStrand.NightDisorder));
        world.AddIncidentSite(new IncidentSite(NodeId(GridSize - 2, GridSize - 2).ToString(), IncidentStrand.StreetRacing));
        world.AddIncidentSite(new IncidentSite(NodeId(GridSize / 2, GridSize / 2).ToString(), IncidentStrand.NightDisorder));

        return world;
    }
}
