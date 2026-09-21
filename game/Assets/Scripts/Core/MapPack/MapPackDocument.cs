// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.MapPack;

/// <summary>The whole deserialized, hash-verified MapPack file. Only
/// <see cref="Thaivia.Core.Serialization.MapPackLoader"/> constructs this
/// (its constructor is internal to the assembly) so a caller cannot
/// synthesize a "loaded" MapPackDocument that skipped hash/version
/// validation.</summary>
public sealed class MapPackDocument
{
    internal MapPackDocument(MapPackVolatile volatileFields, string contentHash, MapPackPayload payload)
    {
        VolatileFields = volatileFields;
        ContentHash = contentHash;
        Payload = payload;
    }

    public MapPackVolatile VolatileFields { get; }

    /// <summary>"sha256:...", verified by the loader to match a fresh hash
    /// of <see cref="Payload"/> before this object is ever constructed.</summary>
    public string ContentHash { get; }

    public MapPackPayload Payload { get; }
}
