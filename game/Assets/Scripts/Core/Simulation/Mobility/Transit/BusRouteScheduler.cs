// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using Thaivia.Core.Simulation.Mobility.Routing;

namespace Thaivia.Core.Simulation.Mobility.Transit;

/// <summary>Derives a bus route's round-trip duration and per-tick
/// throughput from the SAME turn-aware vehicle routing graph everything
/// else in this namespace uses -- a route's schedule is never a
/// hand-entered number, it is computed from real network distance +
/// dwell time.</summary>
public static class BusRouteScheduler
{
    /// <summary>Assumed cruising speed used only to convert route distance
    /// into ticks -- a documented simulation_assumption (no real speed
    /// measurement exists for this pilot AOI yet).</summary>
    public const double AssumedSpeedMetersPerTick = 2.0; // ~36 km/h at TickConfig.TicksPerSecond=5Hz.

    /// <summary>Total ticks for one full loop of <paramref name="route"/>
    /// (stop 0 -&gt; stop 1 -&gt; ... -&gt; last stop -&gt; back to stop 0),
    /// including dwell time at every stop visited. Throws if any leg has
    /// no vehicle path at all -- a bus route can never be scheduled over a
    /// disconnected network and silently produce a meaningless
    /// number.</summary>
    public static long RoundTripTicks(BusRoute route, MobilityGraph vehicleGraph)
    {
        double totalDistanceMeters = 0;
        for (var i = 0; i < route.StopNodeIds.Count - 1; i++)
        {
            totalDistanceMeters += RequireLegDistance(route, vehicleGraph, route.StopNodeIds[i], route.StopNodeIds[i + 1]);
        }

        totalDistanceMeters += RequireLegDistance(route, vehicleGraph, route.StopNodeIds[^1], route.StopNodeIds[0]);

        var travelTicks = (long)Math.Ceiling(totalDistanceMeters / AssumedSpeedMetersPerTick);
        var dwellTicks = (long)route.DwellTicksPerStop * route.StopNodeIds.Count;
        return travelTicks + dwellTicks;
    }

    private static double RequireLegDistance(BusRoute route, MobilityGraph vehicleGraph, long from, long to)
    {
        var distance = vehicleGraph.ShortestDistanceMeters(from, to);
        if (distance is null)
        {
            throw new InvalidOperationException($"Bus route '{route.Id}' has no vehicle path between stop {from} and stop {to}.");
        }

        return distance.Value;
    }

    /// <summary>Seats delivered per tick, averaged over one round trip:
    /// (vehicles x capacity) / round-trip ticks (Little's-law-style
    /// throughput, not a hand-picked number). Adding dwell time strictly
    /// increases <see cref="RoundTripTicks"/> and therefore strictly
    /// decreases this -- more boarding time per stop costs frequency, the
    /// trade-off the schedule makes explicit.</summary>
    public static double ThroughputPerTick(BusRoute route, long roundTripTicks)
    {
        if (roundTripTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roundTripTicks));
        }

        return (double)route.TotalFleetCapacity / roundTripTicks;
    }
}
