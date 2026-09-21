// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;
using Thaivia.Core.Simulation.Mobility.Routing;

namespace Thaivia.Core.Simulation.Mobility.Demand;

/// <summary>
/// All-or-nothing traffic assignment: each <see cref="OdBatch"/> is routed
/// once (its turn-aware shortest path, via <see cref="MobilityGraph.ShortestRoute"/>)
/// and its whole VehicleCount is added as arrivals to every way that path
/// traverses. This is a documented SIMPLIFICATION over a real multi-path
/// equilibrium assignment (no capacity-aware rerouting within one
/// assignment pass) -- see ADR-0022. Arrivals are aggregated PER WAY
/// (not per direction) here; a caller that needs directional
/// <see cref="Queues.LinkKey"/> arrivals for <see cref="Queues.LinkQueueSimulator"/>
/// must still decide a direction (e.g. always "Forward" for a first cut),
/// which this type deliberately leaves to the caller rather than guessing.
/// A batch with no route at all (unreachable under current oneway/turn-
/// restriction/road-works state) is simply not assigned anywhere -- it
/// never silently lands on an arbitrary link.
/// </summary>
public static class NetworkDemandAssignment
{
    public static IReadOnlyDictionary<long, long> AssignToWays(MobilityGraph vehicleGraph, IReadOnlyList<OdBatch> batches)
    {
        var arrivalsByWay = new Dictionary<long, long>();
        foreach (var batch in batches)
        {
            var route = vehicleGraph.ShortestRoute(batch.OriginNodeId, batch.DestinationNodeId);
            if (route is null)
            {
                continue;
            }

            // A single way can appear more than once in WayIdsInOrder (a
            // multi-segment polyline crosses several node-pairs that all
            // belong to the same way_id) -- de-duplicate PER BATCH first,
            // or a batch would be double-counted onto the same way just
            // because its path happened to cross more than one segment of
            // it.
            var waysOnThisRoute = new HashSet<long>();
            foreach (var wayId in route.Value.WayIdsInOrder)
            {
                if (wayId >= 0)
                {
                    waysOnThisRoute.Add(wayId); // negative ids are synthetic player-connector/crossing ids -- no real way_id to charge capacity against.
                }
            }

            foreach (var wayId in waysOnThisRoute)
            {
                arrivalsByWay[wayId] = (arrivalsByWay.TryGetValue(wayId, out var existing) ? existing : 0) + batch.VehicleCount;
            }
        }

        return arrivalsByWay;
    }
}
