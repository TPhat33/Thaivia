// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Utilities;

/// <summary>
/// One utility service source (a treatment plant, substation, transfer
/// station -- whatever this <see cref="UtilityKind"/> needs, at scenario
/// authoring granularity, not per-pipe/per-cable detail), anchored at a
/// real road_graph node id exactly the way <see cref="Mobility.Gateways.GatewayFlow"/>
/// is anchored at a gateway node -- never a fabricated location. Modeled
/// the same shape as a bus route/gateway: a location plus a FINITE
/// per-tick throughput. <see cref="CapacityUnitsPerTick"/> has no
/// "unlimited" sentinel value anywhere in this type -- it is a plain
/// non-negative int, always consulted by <see cref="UtilityCoverage"/>,
/// so a source can never silently serve unbounded demand (AGENTS.md
/// rule 4's "no data is not empty/fine" discipline, applied to capacity
/// rather than to a source tag).
/// </summary>
public readonly struct UtilitySource
{
    public UtilitySource(UtilityKind kind, long nodeId, int capacityUnitsPerTick)
    {
        if (capacityUnitsPerTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityUnitsPerTick), "Utility source capacity must not be negative -- there is no 'unlimited' representation.");
        }

        Kind = kind;
        NodeId = nodeId;
        CapacityUnitsPerTick = capacityUnitsPerTick;
    }

    public UtilityKind Kind { get; }

    /// <summary>The road_graph node this source is anchored at. WorldState's
    /// <c>AddUtilitySource</c> rejects a node id that is not actually in
    /// the loaded road graph -- this can never be an invented location.</summary>
    public long NodeId { get; }

    /// <summary>Demand units/tick this source can serve. One household
    /// cohort's <c>HouseholdCount</c> is treated as one demand unit/tick
    /// (see <c>WorldState.ComputeUtilityCoverageScore</c>'s doc comment)
    /// -- a documented simulation_assumption, never a measured
    /// consumption figure.</summary>
    public int CapacityUnitsPerTick { get; }
}
