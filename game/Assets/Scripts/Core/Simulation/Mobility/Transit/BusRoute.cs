// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;

namespace Thaivia.Core.Simulation.Mobility.Transit;

/// <summary>
/// A bus route defined over the road-graph network (spec §9: "bus route บน
/// network; คิว/ขีดจำกัด"). <see cref="VehicleCount"/> is a hard, finite
/// cap (never "as many buses as demand needs") -- ridership is drawn from
/// cohorts (batch numbers, see <see cref="BusRidership"/>), never
/// per-passenger agents, matching the same
/// "cohorts are population truth, sprites are only a sample" discipline as
/// <see cref="Cohorts.HouseholdCohort"/>.
/// </summary>
public sealed class BusRoute
{
    public BusRoute(string id, IReadOnlyList<long> stopNodeIds, int dwellTicksPerStop, int vehicleCount, int capacityPerVehicle)
    {
        if (stopNodeIds.Count < 2)
        {
            throw new ArgumentException("A bus route needs at least two stops.", nameof(stopNodeIds));
        }

        if (dwellTicksPerStop < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dwellTicksPerStop));
        }

        if (vehicleCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vehicleCount), "VehicleCount must be a positive, finite cap -- an unbounded fleet is not modelled.");
        }

        if (capacityPerVehicle <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityPerVehicle));
        }

        Id = id;
        StopNodeIds = stopNodeIds;
        DwellTicksPerStop = dwellTicksPerStop;
        VehicleCount = vehicleCount;
        CapacityPerVehicle = capacityPerVehicle;
    }

    public string Id { get; }
    public IReadOnlyList<long> StopNodeIds { get; }
    public int DwellTicksPerStop { get; }
    public int VehicleCount { get; }
    public int CapacityPerVehicle { get; }

    /// <summary>Total seated/standing capacity across the whole fleet at
    /// any instant -- the hard bound ridership assignment never
    /// exceeds.</summary>
    public int TotalFleetCapacity => VehicleCount * CapacityPerVehicle;
}
