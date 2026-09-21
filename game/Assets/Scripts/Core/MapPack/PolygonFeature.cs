// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;

namespace Thaivia.Core.MapPack;

/// <summary>
/// A GeographyBase polygon feature (building, building_part, or
/// water_polygon). Every property is populated once, in the constructor,
/// from the MapPack file, and has no public setter -- the whole type is
/// effectively immutable for its lifetime, which is what makes it safe to
/// share across the render/selection/inspector layers without a defensive
/// copy at each boundary.
/// </summary>
public sealed class PolygonFeature
{
    public PolygonFeature(
        string featureClass,
        string sourceKind,
        long sourceId,
        SourceTags sourceTags,
        IReadOnlyList<AssumptionRecord> visualAssumptions,
        IReadOnlyList<AssumptionRecord> simulationAssumptions,
        IReadOnlyList<RingGroup> ringGroupsCanonical,
        IReadOnlyList<RingGroup> ringGroupsRender1Cm)
    {
        FeatureClass = featureClass;
        SourceKind = sourceKind;
        SourceId = sourceId;
        SourceTags = sourceTags;
        VisualAssumptions = visualAssumptions;
        SimulationAssumptions = simulationAssumptions;
        RingGroupsCanonical = ringGroupsCanonical;
        RingGroupsRender1Cm = ringGroupsRender1Cm;
    }

    public string FeatureClass { get; }
    public string SourceKind { get; }
    public long SourceId { get; }
    public SourceTags SourceTags { get; }
    public IReadOnlyList<AssumptionRecord> VisualAssumptions { get; }
    public IReadOnlyList<AssumptionRecord> SimulationAssumptions { get; }
    public IReadOnlyList<RingGroup> RingGroupsCanonical { get; }
    public IReadOnlyList<RingGroup> RingGroupsRender1Cm { get; }
}
