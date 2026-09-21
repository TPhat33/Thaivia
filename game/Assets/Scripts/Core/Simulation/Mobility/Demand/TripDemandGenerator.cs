// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;
using Thaivia.Core.Simulation.Archetypes;
using Thaivia.Core.Simulation.Buildings;
using Thaivia.Core.Simulation.Cohorts;
using Thaivia.Core.Simulation.Mobility.Routing;

namespace Thaivia.Core.Simulation.Mobility.Demand;

/// <summary>
/// Generates commute <see cref="OdBatch"/>es from
/// <see cref="HouseholdCohort"/> population -- ONE batch per cohort, sized
/// by that cohort's <see cref="HouseholdCohort.PopulationCount"/> times the
/// residential archetype's hourly trip-generation activity (see
/// <see cref="ActivityClockCatalog"/>) times a mode-share fraction. There
/// is no per-person loop anywhere in this type -- a cohort of 10,000
/// people produces exactly one <see cref="OdBatch"/>, matching spec §11's
/// "รถ/คนที่วาดเป็น sample ไม่ใช่ตัวกำหนดประชากรจริง" discipline extended
/// to travel demand.
/// </summary>
public static class TripDemandGenerator
{
    /// <summary>Fraction of a cohort's generated trips assumed to use a
    /// private vehicle this hour -- a documented simulation_assumption
    /// scenario number (no measured mode-share data exists).</summary>
    public const double DefaultVehicleModeShare = 0.3;

    public static IReadOnlyList<OdBatch> GenerateCommuteBatches(
        IEnumerable<HouseholdCohort> cohorts,
        IReadOnlyDictionary<long, BuildingSimState> buildingStates,
        MobilityGraph vehicleGraph,
        int hourOfDay,
        double vehicleModeShare = DefaultVehicleModeShare)
    {
        var batches = new List<OdBatch>();
        foreach (var cohort in cohorts)
        {
            if (!buildingStates.TryGetValue(cohort.HomeBuildingSourceId, out var home))
            {
                continue; // the home building no longer exists in this world -- skip rather than guess.
            }

            var nearestJobNode = FindNearestJobNode(buildingStates, vehicleGraph, home.NearestRoadNodeId, excludeSourceId: home.SourceId);
            if (nearestJobNode is null)
            {
                continue; // no reachable job-bearing building this hour -- no trip generated, not a fabricated one.
            }

            // Cohorts only ever exist for Residential buildings (see
            // WorldState.SeedFromGeography) -- the origin's activity
            // profile is always Residential's.
            var activity = ActivityClockCatalog.At(BuildingArchetype.Residential, hourOfDay);
            var tripCount = (long)(cohort.PopulationCount * activity.TripGeneration * vehicleModeShare);
            if (tripCount <= 0)
            {
                continue;
            }

            batches.Add(new OdBatch(home.NearestRoadNodeId, nearestJobNode.Value, TravelMode.Vehicle, tripCount, cohort.Id));
        }

        return batches;
    }

    private static long? FindNearestJobNode(IReadOnlyDictionary<long, BuildingSimState> buildingStates, MobilityGraph graph, long fromNodeId, long excludeSourceId)
    {
        long? best = null;
        var bestDistance = double.PositiveInfinity;
        foreach (var b in buildingStates.Values)
        {
            if (b.JobsCount <= 0 || b.SourceId == excludeSourceId)
            {
                continue;
            }

            var distance = graph.ShortestDistanceMeters(fromNodeId, b.NearestRoadNodeId);
            if (distance is { } value && value < bestDistance)
            {
                bestDistance = value;
                best = b.NearestRoadNodeId;
            }
        }

        return best;
    }
}
