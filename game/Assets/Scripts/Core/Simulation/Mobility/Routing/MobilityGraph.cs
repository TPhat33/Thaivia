// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;
using Thaivia.Core.Graph;
using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation.Accessibility;

namespace Thaivia.Core.Simulation.Mobility.Routing;

/// <summary>
/// A directed, mode-aware shortest-path graph over the loaded MapPack's
/// road_graph (spec §9/§11: "directed multimodal graph"). Unlike
/// <see cref="AccessibilityGraph"/> (which is intentionally
/// oneway/turn-restriction-agnostic and only used for the coarse G3
/// accessibility SCORE), this type is the G4 routing primitive and:
///
///   - honors <see cref="RoadEdge.Oneway"/> (including
///     <see cref="OnewayDirection.Reversed"/>, i.e. `oneway=-1`) for
///     <see cref="TravelMode.Vehicle"/>/<see cref="TravelMode.Freight"/>;
///   - honors <see cref="RoadEdge.AccessModes"/> per mode via
///     <see cref="ModeAccess"/>;
///   - honors turn restrictions for Vehicle/Freight via
///     <see cref="RoadGraphIndex.EvaluateTurn"/> +
///     <see cref="GameplayTurnPolicy"/> -- the fail-safe-deny policy the G3
///     accessibility layer explicitly deferred (see G3's progress.md and
///     ADR-0016) is now actually consumed by a routing consumer, closing
///     that gap;
///   - treats <see cref="TravelMode.Walk"/> as bidirectional regardless of
///     a road's vehicle oneway tag (a pedestrian is not restricted by a
///     one-way car lane) and does not evaluate turn restrictions for
///     Walk (OSM `type=restriction` relations are overwhelmingly
///     vehicle-oriented; treating them as also binding pedestrians would
///     be an unsupported assumption this codebase is not willing to make
///     silently -- see AGENTS.md rule 4).
///
/// Shortest paths are computed over an EXPANDED state space of
/// (node, arrivalWayId) rather than plain nodes, because a turn
/// restriction's applicability depends on which way a traveller arrived
/// on, not merely which node they are at -- collapsing to plain per-node
/// Dijkstra (as AccessibilityGraph does) would make turn-restriction
/// enforcement structurally impossible to add correctly later.
/// </summary>
public sealed class MobilityGraph
{
    /// <summary>Sentinel "no prior way" value for the very first step from
    /// an origin -- never a real OSM way id (those are always &gt;=1) and
    /// never a synthetic connector id (those are always negative), so it
    /// can never collide and accidentally suppress/allow a real turn
    /// check.</summary>
    private const long NoPriorWay = 0;

    private readonly Dictionary<long, List<(long To, double Length, long WayId)>> _adjacency = new();
    private readonly RoadGraphIndex _turnIndex;
    private readonly bool _respectsOneway;
    private readonly bool _respectsTurnRestrictions;

    public TravelMode Mode { get; }

    /// <param name="crossings">Player-committed pedestrian crossings
    /// (only meaningful for <see cref="TravelMode.Walk"/> -- ignored for
    /// Vehicle/Freight, since a crossing is a foot-traffic connector, not
    /// a road). See <see cref="Crossings.PedestrianCrossing"/>.</param>
    /// <param name="requireAccessibleCrossings">When true, a crossing is
    /// only usable if its <see cref="Crossings.PedestrianCrossing.AccessibleFlag"/>
    /// is set -- builds the graph a traveller with an accessible-path need
    /// actually routes over, as distinct from the general walking graph
    /// (false). See CrossingsTests for the test proving this changes which
    /// routes are computed.</param>
    /// <param name="closedWayIds">Way ids that are currently fully
    /// impassable for Vehicle/Freight (a <see cref="RoadWorks.RoadWorksZone"/>
    /// with <see cref="RoadWorks.RoadWorksZone.CapacityMultiplierDuringConstruction"/>
    /// at or below 0 -- see that type's doc comment: "callers that truly
    /// need a hard closure can still pass 0.0"). Zero effective capacity
    /// means zero vehicles can physically traverse it, so it is excluded
    /// from the adjacency list entirely rather than merely made
    /// expensive -- this is a real availability change, not a routing
    /// heuristic. Deliberately NOT applied to <see cref="TravelMode.Walk"/>:
    /// a road closed to vehicle traffic during construction does not, by
    /// itself, establish that its footway/verge is also impassable, and
    /// this codebase does not assume facts it was not given (AGENTS.md
    /// rule 4) -- ignored for Walk.</param>
    public MobilityGraph(
        RoadGraph roadGraph,
        TravelMode mode,
        IReadOnlyList<PlannedRoadSegment>? extraSegments = null,
        IReadOnlyList<Crossings.PedestrianCrossing>? crossings = null,
        bool requireAccessibleCrossings = false,
        IReadOnlySet<long>? closedWayIds = null)
    {
        Mode = mode;
        _respectsOneway = mode != TravelMode.Walk;
        _respectsTurnRestrictions = mode != TravelMode.Walk;
        _turnIndex = new RoadGraphIndex(roadGraph);
        var appliesClosures = mode != TravelMode.Walk && closedWayIds is { Count: > 0 };

        var nodeById = new Dictionary<long, RoadGraphNode>();
        foreach (var node in roadGraph.Nodes)
        {
            nodeById[node.NodeId] = node;
        }

        foreach (var edge in roadGraph.Edges)
        {
            if (!ModeAccess.IsAllowed(edge, mode))
            {
                continue;
            }

            if (appliesClosures && closedWayIds!.Contains(edge.WayId))
            {
                continue; // fully closed this tick -- not part of the routable graph at all, for Vehicle/Freight.
            }

            for (var i = 0; i < edge.NodeRefs.Count - 1; i++)
            {
                var a = edge.NodeRefs[i];
                var b = edge.NodeRefs[i + 1];
                if (!nodeById.TryGetValue(a, out var na) || !nodeById.TryGetValue(b, out var nb))
                {
                    continue;
                }

                var length = Distance(na, nb);

                if (!_respectsOneway)
                {
                    AddStep(a, b, length, edge.WayId);
                    AddStep(b, a, length, edge.WayId);
                    continue;
                }

                switch (edge.Oneway)
                {
                    case OnewayDirection.No:
                        AddStep(a, b, length, edge.WayId);
                        AddStep(b, a, length, edge.WayId);
                        break;
                    case OnewayDirection.Forward:
                        AddStep(a, b, length, edge.WayId);
                        break;
                    case OnewayDirection.Reversed:
                        AddStep(b, a, length, edge.WayId);
                        break;
                }
            }
        }

        var syntheticWayId = -1L; // shared counter: connectors and crossings never collide with each other or with a real (>=1) way id.

        if (extraSegments is not null)
        {
            foreach (var segment in extraSegments)
            {
                var wayId = syntheticWayId--;
                AddStep(segment.FromNodeId, segment.ToNodeId, segment.LengthMeters, wayId);
                AddStep(segment.ToNodeId, segment.FromNodeId, segment.LengthMeters, wayId);
            }
        }

        if (crossings is not null && mode == TravelMode.Walk)
        {
            foreach (var crossing in crossings)
            {
                if (requireAccessibleCrossings && !crossing.AccessibleFlag)
                {
                    continue; // not usable by a traveller who needs an accessible path.
                }

                var wayId = syntheticWayId--;
                AddStep(crossing.NodeAId, crossing.NodeBId, crossing.LengthMeters, wayId);
                AddStep(crossing.NodeBId, crossing.NodeAId, crossing.LengthMeters, wayId);
            }
        }
    }

    private static double Distance(RoadGraphNode a, RoadGraphNode b)
    {
        var dx = a.LocalX - b.LocalX;
        var dz = a.LocalZ - b.LocalZ;
        return Math.Sqrt(dx * dx + dz * dz);
    }

    private void AddStep(long from, long to, double length, long wayId)
    {
        if (!_adjacency.TryGetValue(from, out var list))
        {
            list = new List<(long, double, long)>();
            _adjacency[from] = list;
        }

        list.Add((to, length, wayId));
    }

    public readonly record struct Route(double DistanceMeters, IReadOnlyList<long> WayIdsInOrder);

    /// <summary>Per-(from,to) memoized result of a STATIC (no
    /// <c>wayCostMultiplier</c>) <see cref="ShortestRoute"/> call -- see
    /// ADR-0027. A resolved OD route cannot change tick-to-tick unless this
    /// graph's topology changes, and this graph's adjacency is fixed for
    /// the lifetime of the instance (built once, in the constructor, never
    /// mutated afterward) -- so keying the cache to the INSTANCE itself,
    /// rather than to some separately tracked "is it still valid" flag, is
    /// the entire invalidation contract: a stale entry is structurally
    /// impossible as long as nobody keeps using an old instance past the
    /// point where <see cref="WorldState.GetOrBuildVehicleGraph"/>/
    /// <see cref="WorldState.GetOrBuildWalkGraph"/> would have rebuilt it.
    /// A route that legitimately does not exist is cached as a null
    /// <see cref="Route"/> too (a nullable value in the dictionary,
    /// distinguished from "not yet computed" by TryGetValue's own bool),
    /// so an unreachable pair is not re-searched every tick either.</summary>
    private readonly Dictionary<(long From, long To), Route?> _staticRouteCache = new();

    /// <summary>Turn-aware shortest route. Returns null when no path exists
    /// under this mode's access/oneway/turn-restriction rules -- never a
    /// straight-line fallback (same discipline as AccessibilityGraph).
    ///
    /// <paramref name="wayCostMultiplier"/> is null for every caller before
    /// Debt 2 (see ADR-0026/ADR-0027) and for every caller that wants real
    /// free-flow distance today (job-search, bus walking-reach, safety
    /// proximity, accessibility score) -- that path is cached per this
    /// type's <see cref="_staticRouteCache"/> doc comment. When non-null
    /// (only <see cref="Demand.NetworkDemandAssignment.AssignToWaysCongestionAware"/>
    /// passes one), each candidate edge's search WEIGHT is
    /// <c>length * wayCostMultiplier(wayId)</c> so the search can prefer a
    /// congested-but-short way less than an uncongested-but-longer one --
    /// but the returned <see cref="Route.DistanceMeters"/> is always the
    /// chosen path's true physical length, computed alongside the weighted
    /// search, never the weighted cost itself (a caller reading
    /// DistanceMeters must always get real metres). This dynamic path is
    /// deliberately NEVER cached: the whole point is that congestion
    /// changes what "shortest" means from one call to the next within the
    /// same tick (successive slices) and across ticks, so memoizing it
    /// would silently reintroduce the exact staleness bug the static cache
    /// above is built to avoid.</summary>
    public Route? ShortestRoute(long fromNodeId, long toNodeId, Func<long, double>? wayCostMultiplier = null)
    {
        if (fromNodeId == toNodeId)
        {
            return new Route(0, Array.Empty<long>());
        }

        if (wayCostMultiplier is null && _staticRouteCache.TryGetValue((fromNodeId, toNodeId), out var cachedRoute))
        {
            return cachedRoute;
        }

        var result = ComputeShortestRoute(fromNodeId, toNodeId, wayCostMultiplier);

        if (wayCostMultiplier is null)
        {
            _staticRouteCache[(fromNodeId, toNodeId)] = result;
        }

        return result;
    }

    private Route? ComputeShortestRoute(long fromNodeId, long toNodeId, Func<long, double>? wayCostMultiplier)
    {
        var start = (Node: fromNodeId, ViaWayId: NoPriorWay);
        var cost = new Dictionary<(long Node, long ViaWayId), double> { [start] = 0 };
        var physicalDistance = new Dictionary<(long Node, long ViaWayId), double> { [start] = 0 };
        var prev = new Dictionary<(long Node, long ViaWayId), (long Node, long ViaWayId)>();
        var visited = new HashSet<(long, long)>();
        var queue = new PriorityQueue<(long Node, long ViaWayId), double>();
        queue.Enqueue(start, 0);

        while (queue.TryDequeue(out var current, out _))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            if (current.Node == toNodeId)
            {
                return new Route(physicalDistance[current], ReconstructWayIds(prev, current));
            }

            if (!_adjacency.TryGetValue(current.Node, out var steps))
            {
                continue;
            }

            foreach (var (to, length, wayId) in steps)
            {
                if (_respectsTurnRestrictions && current.ViaWayId != NoPriorWay)
                {
                    var decision = _turnIndex.EvaluateTurn(current.ViaWayId, current.Node, wayId);
                    if (!GameplayTurnPolicy.IsAllowedForGameplayRouting(decision))
                    {
                        continue;
                    }
                }

                var weight = wayCostMultiplier is null ? length : length * wayCostMultiplier(wayId);
                var next = (to, wayId);
                var candidateCost = cost[current] + weight;
                if (!cost.TryGetValue(next, out var existingCost) || candidateCost < existingCost)
                {
                    cost[next] = candidateCost;
                    physicalDistance[next] = physicalDistance[current] + length;
                    prev[next] = current;
                    queue.Enqueue(next, candidateCost);
                }
            }
        }

        return null;
    }

    public double? ShortestDistanceMeters(long fromNodeId, long toNodeId) => ShortestRoute(fromNodeId, toNodeId)?.DistanceMeters;

    private static IReadOnlyList<long> ReconstructWayIds(
        Dictionary<(long Node, long ViaWayId), (long Node, long ViaWayId)> prev,
        (long Node, long ViaWayId) end)
    {
        var ids = new List<long>();
        var cursor = end;
        while (prev.TryGetValue(cursor, out var previous))
        {
            ids.Add(cursor.ViaWayId);
            cursor = previous;
        }

        ids.Reverse();
        return ids;
    }
}
