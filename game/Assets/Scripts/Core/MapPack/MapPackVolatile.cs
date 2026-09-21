// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.MapPack;

/// <summary>Fields excluded from `content_hash` on purpose (bake
/// wall-clock time, source retrieval wall-clock time) -- see
/// `map_pipeline.pipeline.mappack` module docstring / ADR-0006.</summary>
public sealed class MapPackVolatile
{
    public MapPackVolatile(string bakedAt, string sourceRetrievedAt)
    {
        BakedAt = bakedAt;
        SourceRetrievedAt = sourceRetrievedAt;
    }

    public string BakedAt { get; }
    public string SourceRetrievedAt { get; }
}
