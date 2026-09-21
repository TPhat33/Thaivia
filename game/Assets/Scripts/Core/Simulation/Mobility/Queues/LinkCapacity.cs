// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using Thaivia.Core.MapPack;

namespace Thaivia.Core.Simulation.Mobility.Queues;

/// <summary>
/// Per-link vehicle-per-tick throughput capacity, the thing congestion in
/// this codebase is actually derived FROM (spec: "Queues with finite
/// capacity. Congestion must emerge from capacity, not from a hand-tuned
/// traffic score"). There is no "traffic score" field anywhere in this
/// namespace -- <see cref="LinkQueueSimulator"/> only ever compares
/// arrivals against this capacity number.
/// </summary>
public static class LinkCapacity
{
    /// <summary>Vehicles/tick one lane can discharge -- a documented
    /// simulation_assumption (plan §9: "road widths, lanes... ที่ไม่มี tag
    /// ต้องเป็น assumptions ที่แก้และ inspect ได้"), not a measured
    /// value.</summary>
    public const int VehPerTickPerLane = 2;

    /// <summary>Lane count used when the source has no `lanes` tag at all
    /// -- also a documented assumption, never presented as a source
    /// fact.</summary>
    public const int DefaultLaneCountWhenUnknown = 1;

    /// <summary>Base (no road works) capacity for one directed traversal of
    /// <paramref name="edge"/>, from its `lanes` tag when present.</summary>
    public static int BaseCapacityVehPerTick(RoadEdge edge)
    {
        var lanes = DefaultLaneCountWhenUnknown;
        var tag = edge.SourceTags.Get("lanes");
        if (tag is not null && int.TryParse(tag, out var parsed) && parsed > 0)
        {
            lanes = parsed;
        }

        return lanes * VehPerTickPerLane;
    }

    /// <summary>Base capacity reduced by any road-works zone active on this
    /// way at <paramref name="tick"/> -- the mechanism behind "road works
    /// must degrade access WHILE under construction, not only on
    /// completion" (plan §9). Never returns a capacity below 0.</summary>
    public static int EffectiveCapacityVehPerTick(RoadEdge edge, long tick, System.Collections.Generic.IReadOnlyList<RoadWorks.RoadWorksZone> zones)
    {
        var basis = BaseCapacityVehPerTick(edge);
        var multiplier = 1.0;
        foreach (var zone in zones)
        {
            if (zone.WayId == edge.WayId && zone.IsActiveAt(tick))
            {
                multiplier = Math.Min(multiplier, zone.CapacityMultiplierDuringConstruction);
            }
        }

        return Math.Max(0, (int)Math.Floor(basis * multiplier));
    }
}
