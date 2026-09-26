// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;
using System.Linq;
using Thaivia.Core.Simulation.Mobility.Routing;

namespace Thaivia.Core.Simulation.Mobility.Demand;

/// <summary>
/// Turns <see cref="OdBatch"/>es into per-way vehicle arrivals for this
/// tick. Two assignment models live here on purpose (see ADR-0022 and
/// ADR-0027):
///
///   - <see cref="AssignToWaysAllOrNothing"/>: the original G4 model
///     (ADR-0022). Documented SIMPLIFICATION, kept verbatim (not deleted)
///     because <see cref="Tests.Simulation.Mobility.AllOrNothingAssignmentDistortionTests"/>
///     (Thaivia.Core.Tests) exists specifically to document what it gets
///     wrong: 100% of a batch's demand lands on its current free-flow
///     shortest path every tick, with no capacity-aware rerouting, so an
///     alternate route with spare capacity can sit idle forever while the
///     shortest one backs up without bound.
///   - <see cref="AssignToWaysCongestionAware"/>: the ADR-0027 replacement
///     actually wired into <see cref="WorldState.SimulateTick"/>. Splits
///     each batch into <see cref="DefaultSliceCount"/> integer-exact
///     slices and routes each slice by a BPR-style congestion cost that
///     rises with (already-queued backlog + volume assigned so far this
///     tick) over capacity, so demand can shift onto an under-used
///     alternate route both within one tick (successive slices) and
///     across ticks (the queue backlog term). See that method's doc
///     comment for the exact cost function and ADR-0027 for why this
///     model was chosen over a full iterative equilibrium solve.
///
/// Both methods share the same "a batch with no route at all is simply
/// not assigned anywhere" discipline (see ADR-0022) and the same
/// per-batch way-id de-duplication bug fix ADR-0022 documents (a route
/// that crosses the same way_id in more than one segment must not be
/// double-counted).
/// </summary>
public static class NetworkDemandAssignment
{
    /// <summary>Number of integer-exact slices each batch's VehicleCount is
    /// split into for <see cref="AssignToWaysCongestionAware"/> -- a
    /// documented simulation_assumption (no measured "how many times does
    /// a real driver population effectively re-evaluate its route within
    /// one 0.2s tick" data exists; 4 is chosen as the smallest slice count
    /// that let <c>CongestionAwareAssignmentDivertsDemandTests</c>
    /// demonstrate a real, non-trivial diversion onto the high-capacity
    /// alternate route in the two-route fixture -- see that test and
    /// ADR-0027 for the measured before/after).</summary>
    public const int DefaultSliceCount = 4;

    /// <summary>BPR (Bureau of Public Roads) volume-delay function
    /// coefficients: cost multiplier = 1 + Alpha * (load/capacity)^Beta.
    /// These are the textbook default values from the transportation
    /// planning literature, NOT a value fit to any measured Thai traffic
    /// data (none exists yet -- ADR-0003) -- a documented
    /// simulation_assumption, inspectable and changeable here, not a
    /// hidden magic number.</summary>
    public const double CongestionAlpha = 0.15;

    public const double CongestionBeta = 4.0;

    /// <summary>Cap on the BPR multiplier so a single pathological
    /// (load/capacity) ratio cannot produce NaN/Infinity through
    /// Math.Pow, and so an effectively-closed (capacity &lt;= 0) way with
    /// any load on it is merely "extremely undesirable" rather than
    /// literally unrouteable via this cost path (a TRUE hard closure is
    /// <see cref="MobilityGraph"/>'s closedWayIds mechanism -- see
    /// ADR-0027 -- this is only the soft congestion cost).</summary>
    public const double MaxCongestionMultiplier = 1_000.0;

    /// <summary>The original all-or-nothing model (ADR-0022): each batch is
    /// routed once by CURRENT free-flow distance and its whole
    /// VehicleCount is added to every way that route traverses. See this
    /// type's class doc comment for why it is kept rather than deleted.</summary>
    public static IReadOnlyDictionary<long, long> AssignToWaysAllOrNothing(MobilityGraph vehicleGraph, IReadOnlyList<OdBatch> batches)
    {
        var arrivalsByWay = new Dictionary<long, long>();
        foreach (var batch in batches)
        {
            var route = vehicleGraph.ShortestRoute(batch.OriginNodeId, batch.DestinationNodeId);
            if (route is null)
            {
                continue;
            }

            foreach (var wayId in DeduplicateWayIds(route.Value.WayIdsInOrder))
            {
                arrivalsByWay[wayId] = (arrivalsByWay.TryGetValue(wayId, out var existing) ? existing : 0) + batch.VehicleCount;
            }
        }

        return arrivalsByWay;
    }

    /// <summary>
    /// Congestion-aware incremental assignment (ADR-0027). Each batch's
    /// VehicleCount is split into <paramref name="sliceCount"/>
    /// integer-exact slices (see <see cref="SliceShare"/> -- their sum is
    /// always exactly VehicleCount, never rounded away). Batches are
    /// processed in a fixed <see cref="OdBatch.CohortId"/> ordinal order
    /// (never dictionary/set enumeration order -- determinism) and, within
    /// each slice, each batch is routed by a cost function where a way's
    /// weight is <c>length * (1 + CongestionAlpha * (load/capacity)^CongestionBeta)</c>,
    /// <c>load</c> being <paramref name="priorQueueLengthByWay"/> (the real
    /// backlog carried over from the end of the previous tick's
    /// <see cref="Queues.LinkQueueSimulator"/> step) PLUS however much this
    /// SAME tick's incremental assignment has already routed onto that way
    /// in an earlier batch/slice. This makes route CHOICE respond to real
    /// capacity and real backlog -- congestion itself still emerges purely
    /// from <see cref="Queues.LinkQueueSimulator"/> comparing arrivals to
    /// capacity, unchanged; this method only decides WHICH way a batch's
    /// vehicles arrive on, never invents a queue length or backlog number
    /// of its own (spec: "congestion ต้องเกิดจาก capacity ไม่ใช่ hand-tuned
    /// traffic score").
    ///
    /// Determinism: all VehicleCounts/arrivals stay integer (long); the
    /// only doubles are the BPR cost used purely to ORDER candidate edges
    /// within one deterministic Dijkstra run per (batch, slice) -- the same
    /// class of floating-point use already present in
    /// <see cref="MobilityGraph"/>'s Distance() (Math.Sqrt) and never
    /// itself persisted/hashed (see WorldState.ComputeStructuralHash,
    /// which only ever hashes the resulting integer arrivals/queue state).
    /// </summary>
    public static IReadOnlyDictionary<long, long> AssignToWaysCongestionAware(
        MobilityGraph vehicleGraph,
        IReadOnlyList<OdBatch> batches,
        IReadOnlyDictionary<long, int> capacityByWayId,
        IReadOnlyDictionary<long, long> priorQueueLengthByWay,
        int sliceCount = DefaultSliceCount)
    {
        if (sliceCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sliceCount), "at least one slice is required.");
        }

        var arrivalsByWay = new Dictionary<long, long>();
        var cumulativeVolumeByWay = new Dictionary<long, long>();

        var orderedBatches = batches.OrderBy(b => b.CohortId, StringComparer.Ordinal).ToList();

        double CostMultiplier(long wayId)
        {
            var capacity = capacityByWayId.TryGetValue(wayId, out var c) ? c : 0;
            var priorQueue = priorQueueLengthByWay.TryGetValue(wayId, out var q) ? q : 0;
            var volumeSoFarThisTick = cumulativeVolumeByWay.TryGetValue(wayId, out var v) ? v : 0;
            var load = priorQueue + volumeSoFarThisTick;
            return CongestionMultiplier(load, capacity);
        }

        for (var slice = 0; slice < sliceCount; slice++)
        {
            foreach (var batch in orderedBatches)
            {
                var sliceVehicles = SliceShare(batch.VehicleCount, sliceCount, slice);
                if (sliceVehicles <= 0)
                {
                    continue;
                }

                var route = vehicleGraph.ShortestRoute(batch.OriginNodeId, batch.DestinationNodeId, CostMultiplier);
                if (route is null)
                {
                    continue;
                }

                foreach (var wayId in DeduplicateWayIds(route.Value.WayIdsInOrder))
                {
                    arrivalsByWay[wayId] = (arrivalsByWay.TryGetValue(wayId, out var existing) ? existing : 0) + sliceVehicles;
                    cumulativeVolumeByWay[wayId] = (cumulativeVolumeByWay.TryGetValue(wayId, out var cv) ? cv : 0) + sliceVehicles;
                }
            }
        }

        return arrivalsByWay;
    }

    /// <summary>BPR volume-delay multiplier for one way's current load
    /// against its capacity. A non-positive capacity (a fully-degraded
    /// road-works zone that <see cref="MobilityGraph"/> did not already
    /// exclude outright, e.g. because this cost path is also reachable for
    /// a way not present in <paramref name="capacityByWayId"/> at all)
    /// is treated as "avoid this way if there is any load on it at all",
    /// never as free (a missing/zero capacity is never treated as
    /// infinite capacity).</summary>
    private static double CongestionMultiplier(long load, int capacity)
    {
        if (capacity <= 0)
        {
            return load > 0 ? MaxCongestionMultiplier : 1.0;
        }

        var ratio = (double)load / capacity;
        var multiplier = 1.0 + (CongestionAlpha * Math.Pow(ratio, CongestionBeta));
        return Math.Min(multiplier, MaxCongestionMultiplier);
    }

    /// <summary>Integer-exact split of <paramref name="total"/> into
    /// <paramref name="sliceCount"/> shares: the sum over
    /// sliceIndex=0..sliceCount-1 is always exactly <paramref name="total"/>
    /// (the remainder is distributed to the first `remainder` slices, never
    /// dropped or rounded away).</summary>
    private static long SliceShare(long total, int sliceCount, int sliceIndex)
    {
        var baseShare = total / sliceCount;
        var remainder = total % sliceCount;
        return baseShare + (sliceIndex < remainder ? 1 : 0);
    }

    /// <summary>A single way can appear more than once in WayIdsInOrder (a
    /// multi-segment polyline crosses several node-pairs that all belong
    /// to the same way_id) -- de-duplicate PER ROUTE first, or a batch/
    /// slice would be double-counted onto the same way just because its
    /// path happened to cross more than one segment of it (see ADR-0022's
    /// account of the real bug this caught). Negative ids are synthetic
    /// player-connector/crossing ids with no real way_id to charge
    /// capacity against.</summary>
    private static IEnumerable<long> DeduplicateWayIds(IReadOnlyList<long> wayIdsInOrder)
    {
        var seen = new HashSet<long>();
        foreach (var wayId in wayIdsInOrder)
        {
            if (wayId >= 0 && seen.Add(wayId))
            {
                yield return wayId;
            }
        }
    }
}
