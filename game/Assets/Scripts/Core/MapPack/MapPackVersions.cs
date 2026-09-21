// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.MapPack;

/// <summary>
/// The schema/importer versions this build of Thaivia.Core knows how to
/// load, mirroring `map_pipeline.pipeline.settings.SCHEMA_VERSION` /
/// `IMPORTER_VERSION`. Bump these in lockstep with the Python pipeline
/// when a wave intentionally changes the MapPack shape; until then, any
/// pack whose manifest reports a different value is rejected with an
/// explicit, actionable error rather than partially loaded (see
/// MapPackLoader and ADR-0008).
/// </summary>
public static class MapPackVersions
{
    public const string SupportedSchemaVersion = "0.1.0";
    public const string SupportedImporterVersion = "0.1.0";
}
