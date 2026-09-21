// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;
using System.Linq;
using Thaivia.Core.MapPack;

namespace Thaivia.Core.Graph;

/// <summary>A single directed step available at a node: leave `FromNodeId`
/// along `WayId`'s polyline segment `SegmentIndex` and arrive at
/// `ToNodeId`.</summary>
public readonly record struct TraversalStep(long FromNodeId, long ToNodeId, long WayId, int SegmentIndex);

public enum TurnDecision
{
    Allowed,
    Denied,
    Unsupported,
}

/// <summary>
/// Adjacency and turn-restriction evaluation built ONLY from
/// <see cref="RoadEdge.NodeRefs"/> node-id equality -- this index never
/// looks at a coordinate, so two edges that merely cross in plan view (no
/// shared OSM node id, e.g. a bridge over a road at a different `layer`)
/// never produce adjacency; two edges that share a node id always do,
/// regardless of `layer`/`bridge`/`tunnel`. `layer` itself is never read
/// by this type at all.
/// </summary>
public sealed class RoadGraphIndex
{
    private readonly Dictionary<long, List<TraversalStep>> _outgoing = new();
    private readonly List<TurnRestrictionRecord> _restrictions;

    public RoadGraphIndex(RoadGraph graph)
    {
        _restrictions = graph.TurnRestrictions.ToList();

        foreach (var edge in graph.Edges)
        {
            for (var i = 0; i < edge.NodeRefs.Count - 1; i++)
            {
                var a = edge.NodeRefs[i];
                var b = edge.NodeRefs[i + 1];

                switch (edge.Oneway)
                {
                    case OnewayDirection.No:
                        AddStep(a, b, edge.WayId, i);
                        AddStep(b, a, edge.WayId, i);
                        break;
                    case OnewayDirection.Forward:
                        AddStep(a, b, edge.WayId, i);
                        break;
                    case OnewayDirection.Reversed:
                        // oneway=-1: the polyline is drawn a->b in the
                        // source but travel is only allowed b->a.
                        AddStep(b, a, edge.WayId, i);
                        break;
                }
            }
        }
    }

    private void AddStep(long from, long to, long wayId, int segmentIndex)
    {
        if (!_outgoing.TryGetValue(from, out var list))
        {
            list = new List<TraversalStep>();
            _outgoing[from] = list;
        }

        list.Add(new TraversalStep(from, to, wayId, segmentIndex));
    }

    /// <summary>All directed steps that can be taken starting at
    /// `nodeId`, honoring each edge's oneway direction. Empty (not null)
    /// when the node is not part of any road edge, or is a dead end in
    /// the allowed direction.</summary>
    public IReadOnlyList<TraversalStep> GetOutgoingSteps(long nodeId) =>
        _outgoing.TryGetValue(nodeId, out var list) ? list : System.Array.Empty<TraversalStep>();

    /// <summary>True exactly when `nodeId` is reachable as the start or
    /// end of some directed step -- i.e. two edges share this node id.
    /// Two edges that merely cross in plan view with no shared node id
    /// never make this true for the crossing point (there is no crossing
    /// point in this index at all -- see class doc).</summary>
    public bool HasAdjacency(long nodeId) => _outgoing.ContainsKey(nodeId);

    /// <summary>
    /// Evaluates whether travelling from `fromWayId`, through `viaNodeId`,
    /// onto `toWayId` is allowed, applying every `type=restriction`
    /// relation whose `via` is exactly this single node and whose `from`
    /// way matches. A `no_*` restriction naming this `to_way` denies it;
    /// an `only_*` restriction denies every `to_way` other than the one it
    /// names. A restriction that IS anchored at this via node but was
    /// marked `Supported == false` by the pipeline (e.g. a via-way
    /// restriction, or an unrecognized type) returns
    /// <see cref="TurnDecision.Unsupported"/> rather than silently being
    /// treated as "no restriction" -- callers must handle that case
    /// explicitly (e.g. refuse the turn, or surface it to a human).
    /// </summary>
    public TurnDecision EvaluateTurn(long fromWayId, long viaNodeId, long toWayId)
    {
        var applicable = _restrictions.Where(r =>
            r.ViaKind == "n" && r.Via.Count == 1 && r.Via[0] == viaNodeId && r.FromWay == fromWayId);

        var sawUnsupported = false;
        foreach (var r in applicable)
        {
            if (!r.Supported)
            {
                sawUnsupported = true;
                continue;
            }

            if (r.RestrictionType.StartsWith("no_") && r.ToWay == toWayId)
            {
                return TurnDecision.Denied;
            }

            if (r.RestrictionType.StartsWith("only_") && r.ToWay != toWayId)
            {
                return TurnDecision.Denied;
            }
        }

        return sawUnsupported ? TurnDecision.Unsupported : TurnDecision.Allowed;
    }
}
