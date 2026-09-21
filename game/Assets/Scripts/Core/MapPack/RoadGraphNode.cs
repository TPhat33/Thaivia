// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.MapPack;

public sealed class RoadGraphNode
{
    public RoadGraphNode(long nodeId, double localX, double localZ)
    {
        NodeId = nodeId;
        LocalX = localX;
        LocalZ = localZ;
    }

    public long NodeId { get; }
    public double LocalX { get; }
    public double LocalZ { get; }
}
