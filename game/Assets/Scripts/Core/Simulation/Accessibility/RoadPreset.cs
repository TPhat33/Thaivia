// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Accessibility;

/// <summary>
/// A small, named, finite set of lane-count presets for a NEW
/// player-drawn <see cref="PlannedRoadSegment"/> (plan §9: "polyline
/// ใหม่ 2-3 control points พร้อม preview ไม่เริ่ม arbitrary Bezier
/// editor" -- the same "start from 2-3 presets, not an arbitrary editor"
/// discipline applies to lane/capacity choice, not only to the geometry
/// tool). See <see cref="RoadPresetCatalog"/> for what each preset means
/// in lanes/capacity.
///
/// Scope: this enum describes a NEW connector's own lane assumption
/// ONLY. It is never consulted for an EXISTING <c>RoadEdge</c> parsed
/// from a real OSM source -- <see cref="Mobility.Queues.LinkCapacity.BaseCapacityVehPerTick"/>
/// already reads an existing way's own `lanes` tag (or its documented
/// unknown-default) and nothing in this codebase lets a preset overwrite
/// that (plan §9: "ไม่จัด lane count จากภาพถนนหรือ name โดยไร้หลักฐาน").
/// </summary>
public enum RoadPreset
{
    /// <summary>Soi/alley: one lane, the narrowest preset.</summary>
    Soi,

    /// <summary>Local street: two lanes (one each direction), the
    /// default preset when none is specified.</summary>
    Local,

    /// <summary>Arterial: four lanes, the widest preset this catalog
    /// offers.</summary>
    Arterial,
}
