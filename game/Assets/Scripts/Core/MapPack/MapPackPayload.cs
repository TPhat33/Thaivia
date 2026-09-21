// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.MapPack;

/// <summary>The hashed part of a MapPack file (everything `content_hash`
/// is computed over).</summary>
public sealed class MapPackPayload
{
    public MapPackPayload(
        Manifest manifest,
        Provenance provenance,
        GeographyBase geographyBase,
        RoadGraph roadGraph,
        QualityReport qualityReport,
        SimulationInitialization simulationInitialization)
    {
        Manifest = manifest;
        Provenance = provenance;
        GeographyBase = geographyBase;
        RoadGraph = roadGraph;
        QualityReport = qualityReport;
        SimulationInitialization = simulationInitialization;
    }

    public Manifest Manifest { get; }
    public Provenance Provenance { get; }
    public GeographyBase GeographyBase { get; }
    public RoadGraph RoadGraph { get; }
    public QualityReport QualityReport { get; }
    public SimulationInitialization SimulationInitialization { get; }
}
