// Pure C# -- no UnityEngine reference. See game/README.md.
using Thaivia.Core.Simulation.Mobility.Routing;

namespace Thaivia.Core.Simulation.Mobility.Demand;

/// <summary>
/// One origin-destination BATCH of trips (spec §9/§11: "OD batches" derived
/// from cohorts, never per-agent spawning). <see cref="VehicleCount"/> is a
/// whole batch's worth of trips from ONE cohort's population, not one
/// record per traveller -- see <see cref="TripDemandGenerator"/>.
/// </summary>
public readonly record struct OdBatch(long OriginNodeId, long DestinationNodeId, TravelMode Mode, long VehicleCount, string CohortId);
