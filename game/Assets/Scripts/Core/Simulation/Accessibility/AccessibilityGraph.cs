// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;
using Thaivia.Core.MapPack;

namespace Thaivia.Core.Simulation.Accessibility;

/// <summary>
/// Shortest-path NETWORK distance over the road graph -- the only
/// accessibility metric this codebase computes. There is deliberately no
/// straight-line/radius accessibility helper anywhere in Thaivia.Core:
/// plan §11 is explicit that a radius would let a resident "walk across a
/// canal" (เดินข้ามคลองได้). Edge weights are Euclidean lengths between
/// nodes that a real polyline segment (or a player-committed
/// <see cref="PlannedRoadSegment"/>) actually connects -- never between
/// two arbitrary points that merely happen to be near each other in plan
/// view, mirroring the same "adjacency only from an explicit shared
/// connection" discipline as
/// <see cref="Thaivia.Core.Graph.RoadGraphIndex"/>.
/// </summary>
public sealed class AccessibilityGraph
{
    private readonly Dictionary<long, List<(long To, double LengthMeters)>> _adjacency = new();

    public AccessibilityGraph(RoadGraph baseGraph, IReadOnlyList<PlannedRoadSegment>? extraSegments = null)
    {
        var nodeById = new Dictionary<long, RoadGraphNode>();
        foreach (var node in baseGraph.Nodes)
        {
            nodeById[node.NodeId] = node;
        }

        foreach (var edge in baseGraph.Edges)
        {
            for (var i = 0; i < edge.NodeRefs.Count - 1; i++)
            {
                var a = edge.NodeRefs[i];
                var b = edge.NodeRefs[i + 1];
                if (!nodeById.TryGetValue(a, out var na) || !nodeById.TryGetValue(b, out var nb))
                {
                    // A node referenced by this edge fell outside the
                    // loaded node set (e.g. buffer-only geometry) -- skip
                    // rather than guess a length.
                    continue;
                }

                var length = Distance(na, nb);
                switch (edge.Oneway)
                {
                    case OnewayDirection.No:
                        AddDirectedEdge(a, b, length);
                        AddDirectedEdge(b, a, length);
                        break;
                    case OnewayDirection.Forward:
                        AddDirectedEdge(a, b, length);
                        break;
                    case OnewayDirection.Reversed:
                        AddDirectedEdge(b, a, length);
                        break;
                }
            }
        }

        if (extraSegments is not null)
        {
            foreach (var segment in extraSegments)
            {
                // Player-committed connectors are modelled as bidirectional
                // by default (G3 scope decision, see PlannedRoadSegment's
                // doc comment) -- no oneway concept for this simplified
                // connector type yet.
                AddDirectedEdge(segment.FromNodeId, segment.ToNodeId, segment.LengthMeters);
                AddDirectedEdge(segment.ToNodeId, segment.FromNodeId, segment.LengthMeters);
            }
        }
    }

    private static double Distance(RoadGraphNode a, RoadGraphNode b)
    {
        var dx = a.LocalX - b.LocalX;
        var dz = a.LocalZ - b.LocalZ;
        return Math.Sqrt(dx * dx + dz * dz);
    }

    private void AddDirectedEdge(long from, long to, double length)
    {
        if (!_adjacency.TryGetValue(from, out var list))
        {
            list = new List<(long, double)>();
            _adjacency[from] = list;
        }

        list.Add((to, length));
    }

    /// <summary>Dijkstra shortest network distance in metres from
    /// <paramref name="fromNodeId"/> to <paramref name="toNodeId"/>, or
    /// null if there is no path at all through this graph. Two nodes that
    /// are close in straight-line space but not connected by any edge
    /// (own or extra) return null here -- never a straight-line
    /// fallback.</summary>
    public double? ShortestDistanceMeters(long fromNodeId, long toNodeId)
    {
        if (fromNodeId == toNodeId)
        {
            return 0;
        }

        var dist = new Dictionary<long, double> { [fromNodeId] = 0 };
        var visited = new HashSet<long>();
        var queue = new PriorityQueue<long, double>();
        queue.Enqueue(fromNodeId, 0);

        while (queue.TryDequeue(out var current, out _))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            if (current == toNodeId)
            {
                return dist[current];
            }

            if (!_adjacency.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var (to, length) in neighbors)
            {
                var candidate = dist[current] + length;
                if (!dist.TryGetValue(to, out var existing) || candidate < existing)
                {
                    dist[to] = candidate;
                    queue.Enqueue(to, candidate);
                }
            }
        }

        return null;
    }
}
