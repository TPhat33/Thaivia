// Pure C# -- no UnityEngine reference. See game/README.md.
using Thaivia.Core.MapPack;

namespace Thaivia.Core.Coordinates;

/// <summary>
/// The ONE place float64 MapPack local coordinates (already rebased to
/// `manifest.local_origin` in metres by the Python pipeline) get narrowed
/// to float32 for Unity's `Vector3`. Every other piece of Thaivia.Core
/// that needs a renderable point goes through
/// <see cref="ToUnityGroundPlane"/> rather than casting `(float)` inline,
/// so the precision loss has exactly one measured site (see
/// docs/decisions and Thaivia.Core.Tests.CoordinateNarrowingTests, which
/// measures -- not asserts away -- the error over the pilot extent).
///
/// Per `manifest.unity_axis_convention`
/// ("X=east_offset_m, Z=north_offset_m, Y=up_reserved_no_elevation_source"):
/// MapPack `local_x` -> Unity X, MapPack `local_z` -> Unity Z, and Y is
/// whatever elevation value the caller supplies (0f when the source has
/// none, which is the common case today -- there is no OSM elevation
/// source wired up yet).
/// </summary>
public static class CoordinateNarrowing
{
    /// <summary>Narrows one MapPack local-space point to a Unity ground
    /// point, additionally reporting the exact narrowing error (in
    /// metres) on each axis so callers that care (tests, diagnostics) can
    /// see the real number rather than assuming it away.</summary>
    public static Vec3F ToUnityGroundPlane(Vec2 localMeters, float elevationY, out double errorXMeters, out double errorZMeters)
    {
        var x = (float)localMeters.X;
        var z = (float)localMeters.Z;
        errorXMeters = (double)x - localMeters.X;
        errorZMeters = (double)z - localMeters.Z;
        return new Vec3F(x, elevationY, z);
    }

    public static Vec3F ToUnityGroundPlane(Vec2 localMeters, float elevationY = 0f) =>
        ToUnityGroundPlane(localMeters, elevationY, out _, out _);

    /// <summary>
    /// Rebases a local-space point to a NEW local origin before narrowing
    /// (e.g. splitting a MapPack into render chunks each with their own
    /// float32-friendly origin). `originDelta` is `newOrigin - oldOrigin`
    /// in the same metre units as `localMeters`.
    /// </summary>
    public static Vec2 Rebase(Vec2 localMeters, Vec2 originDelta) =>
        new(localMeters.X - originDelta.X, localMeters.Z - originDelta.Z);
}
