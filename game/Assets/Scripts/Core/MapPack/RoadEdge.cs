// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;

namespace Thaivia.Core.MapPack;

/// <summary>
/// One road_graph edge (one OSM way). `Layer` is carried through as the
/// plain semantic ordering integer the pipeline wrote -- never
/// reinterpreted as metres or fed into any distance/height calculation
/// anywhere in Thaivia.Core (see Thaivia.Core.Graph.RoadGraphIndex, which
/// never reads it at all).
/// </summary>
public sealed class RoadEdge
{
    public RoadEdge(
        long wayId,
        SourceTags sourceTags,
        IReadOnlyList<AssumptionRecord> visualAssumptions,
        IReadOnlyList<AssumptionRecord> simulationAssumptions,
        IReadOnlyList<long> nodeRefs,
        IReadOnlyList<Vec2> coordsCanonical,
        IReadOnlyList<Vec2> coordsRender1Cm,
        OnewayDirection oneway,
        int layer,
        bool bridge,
        bool tunnel,
        bool gradeSeparated,
        IReadOnlyDictionary<string, string> accessModes,
        long fromNode,
        long toNode)
    {
        WayId = wayId;
        SourceTags = sourceTags;
        VisualAssumptions = visualAssumptions;
        SimulationAssumptions = simulationAssumptions;
        NodeRefs = nodeRefs;
        CoordsCanonical = coordsCanonical;
        CoordsRender1Cm = coordsRender1Cm;
        Oneway = oneway;
        Layer = layer;
        Bridge = bridge;
        Tunnel = tunnel;
        GradeSeparated = gradeSeparated;
        AccessModes = accessModes;
        FromNode = fromNode;
        ToNode = toNode;
    }

    public long WayId { get; }
    public SourceTags SourceTags { get; }
    public IReadOnlyList<AssumptionRecord> VisualAssumptions { get; }
    public IReadOnlyList<AssumptionRecord> SimulationAssumptions { get; }
    public IReadOnlyList<long> NodeRefs { get; }
    public IReadOnlyList<Vec2> CoordsCanonical { get; }
    public IReadOnlyList<Vec2> CoordsRender1Cm { get; }
    public OnewayDirection Oneway { get; }
    public int Layer { get; }
    public bool Bridge { get; }
    public bool Tunnel { get; }
    public bool GradeSeparated { get; }
    public IReadOnlyDictionary<string, string> AccessModes { get; }
    public long FromNode { get; }
    public long ToNode { get; }
}
