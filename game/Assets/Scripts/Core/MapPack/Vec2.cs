// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.MapPack;

/// <summary>
/// A local-space coordinate pair straight out of the MapPack, in metres,
/// relative to `manifest.local_origin`, BEFORE any float32 narrowing.
/// Per the manifest's `unity_axis_convention`
/// ("X=east_offset_m, Z=north_offset_m"), X is this pair's first
/// component and Z is the second -- there is deliberately no `Y` here,
/// because the source never supplies elevation (Y is reserved, per the
/// manifest, and stays a caller concern in Thaivia.Core.Coordinates).
/// </summary>
public readonly record struct Vec2(double X, double Z);
