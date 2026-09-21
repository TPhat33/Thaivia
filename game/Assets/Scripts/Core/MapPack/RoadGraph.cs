// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;

namespace Thaivia.Core.MapPack;

/// <summary>The road_graph section of a MapPack.</summary>
public sealed class RoadGraph
{
    public RoadGraph(
        IReadOnlyList<RoadGraphNode> nodes,
        IReadOnlyList<RoadEdge> edges,
        IReadOnlySet<long> junctionNodeIds,
        IReadOnlyList<TurnRestrictionRecord> turnRestrictions,
        IReadOnlyList<Gateway> gateways,
        RoadGraphBoundary boundary)
    {
        Nodes = nodes;
        Edges = edges;
        JunctionNodeIds = junctionNodeIds;
        TurnRestrictions = turnRestrictions;
        Gateways = gateways;
        Boundary = boundary;
    }

    public IReadOnlyList<RoadGraphNode> Nodes { get; }
    public IReadOnlyList<RoadEdge> Edges { get; }
    public IReadOnlySet<long> JunctionNodeIds { get; }
    public IReadOnlyList<TurnRestrictionRecord> TurnRestrictions { get; }
    public IReadOnlyList<Gateway> Gateways { get; }
    public RoadGraphBoundary Boundary { get; }
}
