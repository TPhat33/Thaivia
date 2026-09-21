// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Coordinates;

/// <summary>
/// A float32 ground-plane point in Unity's X/Z-ground, Y-up convention
/// (see `manifest.unity_axis_convention`). This mirrors
/// `UnityEngine.Vector3`'s field layout exactly (X, Y, Z, all float) on
/// purpose, so `Runtime` code can construct a `new Vector3(v.X, v.Y,
/// v.Z)` with no further conversion -- but this type itself has no
/// UnityEngine dependency, which is what lets
/// Thaivia.Core.Coordinates.CoordinateNarrowing compile and be tested
/// with plain `dotnet test`.
/// </summary>
public readonly record struct Vec3F(float X, float Y, float Z);
