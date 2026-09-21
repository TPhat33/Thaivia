// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;

namespace Thaivia.Core.MapPack;

public sealed class RoadGraphBoundary
{
    public RoadGraphBoundary(
        IReadOnlyList<double> editableBboxWgs84Lonlat,
        double contextBufferM,
        IReadOnlyDictionary<string, int> nodeClassificationCounts)
    {
        EditableBboxWgs84Lonlat = editableBboxWgs84Lonlat;
        ContextBufferM = contextBufferM;
        NodeClassificationCounts = nodeClassificationCounts;
    }

    /// <summary>[min_lon, min_lat, max_lon, max_lat], WGS84 degrees.</summary>
    public IReadOnlyList<double> EditableBboxWgs84Lonlat { get; }
    public double ContextBufferM { get; }
    public IReadOnlyDictionary<string, int> NodeClassificationCounts { get; }
}
