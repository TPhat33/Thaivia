// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;
using Thaivia.Core.Simulation.Mobility.Queues;

namespace Thaivia.Core.Simulation.Accessibility;

/// <summary>
/// Inspectable data for each <see cref="RoadPreset"/>: a display name, a
/// lane count, and the resulting per-tick capacity computed with the
/// SAME per-lane constant (<see cref="LinkCapacity.VehPerTickPerLane"/>)
/// an existing OSM-derived <c>RoadEdge</c> uses -- so a new player-drawn
/// "local 2-lane" road and an existing real 2-lane way are assumed to
/// carry the same vehicles/tick, never a separately-tuned number (spec
/// §9's "start from 2-3 presets" plus the existing "congestion from
/// capacity, not a hand-tuned score" discipline
/// <see cref="LinkCapacity"/> already documents).
///
/// G6 scope note: this catalog gives a committed <see cref="PlannedRoadSegment"/>
/// a real, inspectable lane count/capacity number. Actually CHARGING
/// congestion against a new connector's synthetic way id in
/// <see cref="Mobility.Demand.NetworkDemandAssignment"/>/
/// <see cref="WorldState"/>'s per-tick link-queue step is a separate,
/// already-documented scope gap (see
/// <see cref="Mobility.Demand.NetworkDemandAssignment"/>'s
/// DeduplicateWayIds doc comment: "Negative ids are synthetic
/// player-connector/crossing ids with no real way_id to charge capacity
/// against") -- this catalog does not close that gap by itself, it only
/// makes sure the NUMBER a future fix would charge against is already
/// defined, consistent, and inspectable rather than invented ad hoc at
/// that time.
/// </summary>
public static class RoadPresetCatalog
{
    private readonly record struct PresetData(string DisplayName, int LaneCount);

    private static readonly IReadOnlyDictionary<RoadPreset, PresetData> Presets = new Dictionary<RoadPreset, PresetData>
    {
        [RoadPreset.Soi] = new PresetData("Soi / alley (1 lane)", LaneCount: 1),
        [RoadPreset.Local] = new PresetData("Local street (2 lanes)", LaneCount: 2),
        [RoadPreset.Arterial] = new PresetData("Arterial (4 lanes)", LaneCount: 4),
    };

    public static string DisplayName(RoadPreset preset) => Presets[preset].DisplayName;

    public static int LaneCount(RoadPreset preset) => Presets[preset].LaneCount;

    /// <summary>Per-tick vehicle capacity for one directed traversal of a
    /// new segment built with <paramref name="preset"/> -- the same
    /// <c>LaneCount * VehPerTickPerLane</c> formula
    /// <see cref="LinkCapacity.BaseCapacityVehPerTick"/> uses for a real
    /// OSM `lanes` tag, so a preset's capacity is never a separately
    /// invented number.</summary>
    public static int CapacityVehPerTick(RoadPreset preset) => LaneCount(preset) * LinkCapacity.VehPerTickPerLane;

    public static IReadOnlyList<RoadPreset> AllPresets { get; } = new[] { RoadPreset.Soi, RoadPreset.Local, RoadPreset.Arterial };
}
