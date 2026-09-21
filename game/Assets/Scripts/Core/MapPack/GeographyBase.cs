// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;

namespace Thaivia.Core.MapPack;

/// <summary>
/// The GeographyBase layer (AGENTS.md rule 3, layer 1): vectors/tags/
/// provenance from the source snapshot. This type owns ONLY feature
/// collections that carry <see cref="SourceTags"/> plus visual/simulation
/// assumption arrays -- it has no field that could hold a
/// SimulationInitialization or PlayerDelta value, so the "three layers
/// never mix in one structure" rule is enforced by this type simply not
/// declaring anywhere to put them, not by a comment asking callers not to
/// add one.
/// </summary>
public sealed class GeographyBase
{
    public GeographyBase(
        IReadOnlyList<PolygonFeature> buildings,
        IReadOnlyList<PolygonFeature> buildingParts,
        IReadOnlyList<PolygonFeature> waterPolygons,
        IReadOnlyList<LineFeature> waterways,
        IReadOnlyList<LineFeature> barriers)
    {
        Buildings = buildings;
        BuildingParts = buildingParts;
        WaterPolygons = waterPolygons;
        Waterways = waterways;
        Barriers = barriers;
    }

    public IReadOnlyList<PolygonFeature> Buildings { get; }
    public IReadOnlyList<PolygonFeature> BuildingParts { get; }
    public IReadOnlyList<PolygonFeature> WaterPolygons { get; }
    public IReadOnlyList<LineFeature> Waterways { get; }
    public IReadOnlyList<LineFeature> Barriers { get; }
}
