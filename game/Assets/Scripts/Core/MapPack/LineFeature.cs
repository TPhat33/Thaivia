// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;

namespace Thaivia.Core.MapPack;

/// <summary>A GeographyBase line feature (waterway or barrier).</summary>
public sealed class LineFeature
{
    public LineFeature(
        string featureClass,
        string sourceKind,
        long sourceId,
        SourceTags sourceTags,
        IReadOnlyList<Vec2> coordsCanonical,
        IReadOnlyList<Vec2> coordsRender1Cm)
    {
        FeatureClass = featureClass;
        SourceKind = sourceKind;
        SourceId = sourceId;
        SourceTags = sourceTags;
        CoordsCanonical = coordsCanonical;
        CoordsRender1Cm = coordsRender1Cm;
    }

    public string FeatureClass { get; }
    public string SourceKind { get; }
    public long SourceId { get; }
    public SourceTags SourceTags { get; }
    public IReadOnlyList<Vec2> CoordsCanonical { get; }
    public IReadOnlyList<Vec2> CoordsRender1Cm { get; }
}
