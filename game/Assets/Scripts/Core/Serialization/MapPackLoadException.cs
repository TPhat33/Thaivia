// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Serialization;

/// <summary>Base type for every reason MapPackLoader refuses a file. Never
/// caught-and-ignored by the loader itself -- every failure mode below
/// stops loading and surfaces one of these instead of returning a
/// partially-populated MapPackDocument.</summary>
public class MapPackLoadException : Exception
{
    public MapPackLoadException(string message) : base(message)
    {
    }
}

/// <summary>A required field the schema/loader contract demands was
/// missing, or present with the wrong JSON kind.</summary>
public sealed class MapPackFieldException : MapPackLoadException
{
    public MapPackFieldException(string path, string message)
        : base($"MapPack field '{path}': {message}")
    {
        Path = path;
    }

    public string Path { get; }
}

/// <summary>`content_hash` did not match a fresh hash of `payload` -- the
/// file was tampered with or corrupted after baking.</summary>
public sealed class MapPackTamperedException : MapPackLoadException
{
    public MapPackTamperedException(string declaredHash, string recomputedHash)
        : base(
            "MapPack content_hash mismatch: the file's declared content_hash does not match a hash "
            + "recomputed from its own payload. The file was tampered with or corrupted after baking. "
            + $"declared={declaredHash} recomputed={recomputedHash}")
    {
        DeclaredHash = declaredHash;
        RecomputedHash = recomputedHash;
    }

    public string DeclaredHash { get; }
    public string RecomputedHash { get; }
}

/// <summary>`manifest.schema_version` or `manifest.importer_version` does
/// not match a version this build of Thaivia.Core knows how to load.</summary>
public sealed class MapPackVersionMismatchException : MapPackLoadException
{
    public MapPackVersionMismatchException(string field, string found, string expected)
        : base(
            $"MapPack {field} mismatch: this build of Thaivia.Core supports {field}="
            + $"'{expected}' but the file declares '{found}'. Regenerate the MapPack with a matching "
            + "pipeline version, or update Thaivia.Core.MapPack.MapPackVersions and re-validate before "
            + "loading packs built with a newer/older importer.")
    {
        Field = field;
        Found = found;
        Expected = expected;
    }

    public string Field { get; }
    public string Found { get; }
    public string Expected { get; }
}
