// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Accessibility;

/// <summary>
/// A player-committed road connector between two EXISTING road-graph
/// nodes (G3 scope decision: arbitrary new polyline road drawing with
/// fresh control points is deferred to G4 -- see docs/progress.md; this
/// wave only supports connecting two nodes the loaded MapPack's road
/// graph already has). This type lives in Thaivia.Core.Simulation, never
/// Thaivia.Core.MapPack: it is never merged into
/// <see cref="Thaivia.Core.MapPack.RoadGraph"/>, only combined
/// side-by-side with it at query time by <see cref="AccessibilityGraph"/>
/// -- the base RoadGraph object from a loaded MapPack is never mutated or
/// extended in place.
/// </summary>
/// <param name="Preset">Which <see cref="RoadPreset"/> (G6-05) this
/// segment was built with -- <see cref="RoadPresetCatalog"/> for its
/// lane count/capacity. Defaults to <see cref="RoadPreset.Local"/> only
/// for call sites that do not care about lane count at all (accessibility
/// distance previews, which are preset-independent) or that are
/// restoring a save written before G6-05 introduced this field (see
/// SaveSerializer's ReadRoadSegments, documented as a stated assumption,
/// never presented as a source fact).</param>
public readonly record struct PlannedRoadSegment(long FromNodeId, long ToNodeId, double LengthMeters, string ProjectId, RoadPreset Preset = RoadPreset.Local);
