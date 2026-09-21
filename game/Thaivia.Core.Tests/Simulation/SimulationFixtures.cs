using System.Collections.Generic;
using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Scenario;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// A small, hand-built (non-JSON, constructed directly via public
/// constructors) GeographyBase + RoadGraph used across the G3 simulation
/// test suite. Modelled as two road clusters that are close in
/// straight-line space but NOT connected by any shared road-graph node --
/// representing opposite banks of a canal with no bridge -- specifically
/// so accessibility tests can prove the engine follows the network, not
/// proximity. See AccessibilityGraphTests for the adversarial assertion
/// and TASKS/progress.md for why this fixture's shape was chosen.
///
///   Cluster A: node 1 --(10m)-- node 2
///                                  |  (2m straight-line gap, NO edge --
///                                  |   this is the "canal")
///   Cluster B: node 3 --(10m)-- node 4 --(10m)-- node 5
///
/// Building 1000 (Residential) sits at node 1's cluster.
/// Building 2000 (Office, jobs) sits at node 5's cluster.
/// </summary>
internal static class SimulationFixtures
{
    public const long Node1 = 1;
    public const long Node2 = 2;
    public const long Node3 = 3;
    public const long Node4 = 4;
    public const long Node5 = 5;

    // Adversarial fixture for AccessibilityGraphTests' Task Zero fix: two
    // nodes 2m apart in straight-line space, CONNECTED (unlike Node2/Node3
    // above) only via a long detour, so the discriminating assertion is not
    // "null vs finite" but "finite and grossly different from straight
    // line". See BuildDetourRoadGraph's doc comment.
    public const long DetourBankANode = 20;
    public const long DetourBankBNode = 21;
    public const long DetourWaypointNode1 = 22;
    public const long DetourWaypointNode2 = 23;

    public const long ResidentialBuildingId = 1000;

    // 2003, not 2000: AssignArchetype's deterministic hash of the source
    // id must land on a non-Residential archetype for this fixture to
    // mean what its comments say (see SimulationFixturesTests, which
    // asserts both ids hash to the archetypes this comment claims -- a
    // control test that would fail loudly if this constant were ever
    // changed without checking the hash again).
    public const long OfficeBuildingId = 2003;

    public static RoadGraph BuildRoadGraph()
    {
        var nodes = new List<RoadGraphNode>
        {
            new(Node1, 0, 0),
            new(Node2, 10, 0),
            new(Node3, 12, 0), // 2m straight-line from node 2 -- but no edge to it.
            new(Node4, 22, 0),
            new(Node5, 32, 0),
        };

        var edges = new List<RoadEdge>
        {
            BuildEdge(101, new long[] { Node1, Node2 }, new (double, double)[] { (0, 0), (10, 0) }),
            BuildEdge(102, new long[] { Node3, Node4, Node5 }, new (double, double)[] { (12, 0), (22, 0), (32, 0) }),
        };

        return new RoadGraph(nodes, edges, new HashSet<long>(), new List<TurnRestrictionRecord>(), new List<Gateway>(),
            new RoadGraphBoundary(new List<double> { 0, 0, 1, 1 }, 0, new Dictionary<string, int>()));
    }

    /// <summary>
    /// A CONNECTED graph (unlike <see cref="BuildRoadGraph"/>'s two
    /// disconnected clusters) where the only path between two nodes that
    /// are 2m apart in straight-line space is a ~502m detour "around the
    /// canal" via a bridge:
    ///
    ///   DetourBankANode (0,0) --(250m)-- DetourWaypointNode1 (0,250)
    ///                                        |
    ///                                     (2m bridge)
    ///                                        |
    ///   DetourBankBNode (2,0)  --(250m)-- DetourWaypointNode2 (2,250)
    ///
    /// Total network distance A-&gt;B: 250 + 2 + 250 = 502m, vs a 2m
    /// straight line. Added specifically because
    /// <see cref="BuildRoadGraph"/>'s existing bridge test happened to have
    /// network distance == straight-line distance by coincidence (nodes
    /// are collinear there), which meant a Euclidean-distance mutant would
    /// have passed every existing AccessibilityGraphTests case except the
    /// fully-disconnected ones. This fixture is connected AND has a grossly
    /// different network vs straight-line distance, so it actually
    /// discriminates "sum of real edge lengths along the only path" from
    /// "straight line, gated by a reachability check".
    /// </summary>
    public static RoadGraph BuildDetourRoadGraph()
    {
        var nodes = new List<RoadGraphNode>
        {
            new(DetourBankANode, 0, 0),
            new(DetourWaypointNode1, 0, 250),
            new(DetourWaypointNode2, 2, 250),
            new(DetourBankBNode, 2, 0),
        };

        var edges = new List<RoadEdge>
        {
            BuildEdge(201, new long[] { DetourBankANode, DetourWaypointNode1 }, new (double, double)[] { (0, 0), (0, 250) }),
            BuildEdge(202, new long[] { DetourWaypointNode1, DetourWaypointNode2 }, new (double, double)[] { (0, 250), (2, 250) }),
            BuildEdge(203, new long[] { DetourWaypointNode2, DetourBankBNode }, new (double, double)[] { (2, 250), (2, 0) }),
        };

        return new RoadGraph(nodes, edges, new HashSet<long>(), new List<TurnRestrictionRecord>(), new List<Gateway>(),
            new RoadGraphBoundary(new List<double> { 0, 0, 2, 250 }, 0, new Dictionary<string, int>()));
    }

    private static RoadEdge BuildEdge(long wayId, long[] nodeRefs, (double X, double Z)[] coords)
    {
        var coordList = new List<Vec2>();
        foreach (var (x, z) in coords)
        {
            coordList.Add(new Vec2(x, z));
        }

        return new RoadEdge(
            wayId,
            SourceTags.Empty,
            new List<AssumptionRecord>(),
            new List<AssumptionRecord>(),
            nodeRefs,
            coordList,
            coordList,
            OnewayDirection.No,
            layer: 0,
            bridge: false,
            tunnel: false,
            gradeSeparated: false,
            new Dictionary<string, string>(),
            fromNode: nodeRefs[0],
            toNode: nodeRefs[^1]);
    }

    public static GeographyBase BuildGeographyBase()
    {
        var buildings = new List<PolygonFeature>
        {
            BuildBuilding(ResidentialBuildingId, "residential", cx: 0, cz: 3),
            BuildBuilding(OfficeBuildingId, "office", cx: 32, cz: 3),
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

        return new PolygonFeature(
            "building",
            "way",
            sourceId,
            tags,
            new List<AssumptionRecord>(),
            new List<AssumptionRecord>(),
            new List<RingGroup> { ringGroup },
            new List<RingGroup> { ringGroup });
    }

    public static SimulationInitialization BuildSimulationInitialization()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("{}");
        return new SimulationInitialization("0.1.0", "G3 test fixture -- fictional, not real Thai statistics.", doc.RootElement.Clone());
    }

    /// <summary>A fresh WorldState over this fixture. Every archetype
    /// assignment is deterministic from source id (see
    /// WorldState.AssignArchetype), so callers that need the residential
    /// building to actually BE Residential must pick a source id whose
    /// hash lands on Residential -- <see cref="ResidentialBuildingId"/>/
    /// <see cref="OfficeBuildingId"/> below were checked to do so (see
    /// SimulationFixturesTests for the control assertion that proves
    /// this).</summary>
    public static WorldState BuildWorldState(long masterSeed = 42, long initialCashThb = 5_000_000) =>
        new(BuildGeographyBase(), BuildSimulationInitialization(), BuildRoadGraph(), new ScenarioConfig(masterSeed, initialCashThb));
}
