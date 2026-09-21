// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;
using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.RandomStreams;

namespace Thaivia.Core.Simulation.Save;

/// <summary>
/// Everything spec §13 requires a save to carry: mapId + mapVersion
/// (here, mapContentHash -- the MapPack's already-verified content hash,
/// stronger than a bare version string), simulation/content version,
/// mutable state, PlayerDelta-derived state (buildings/cohorts/projects/
/// planned segments/vacated lots), RNG stream positions, and the tick/
/// revision counters. Deliberately does NOT carry GeographyBase or
/// SimulationInitialization themselves -- those are re-loaded fresh from
/// the installed MapPack on restore, and mapId/mapContentHash is exactly
/// what lets <see cref="SaveGameStore"/> refuse to load a save against
/// the wrong one (see LoadResult.MapVersionMismatch).
/// </summary>
public sealed class SaveGame
{
    public SaveGame(
        string mapId,
        string mapContentHash,
        string simulationVersion,
        string contentVersion,
        long currentTick,
        long revision,
        long masterSeed,
        IReadOnlyDictionary<RandomStreamName, ulong> rngStates,
        IReadOnlyDictionary<RandomStreamName, long> rngDrawCounts,
        IReadOnlyDictionary<LedgerAccountKind, long> cashByKind,
        IReadOnlyDictionary<LedgerAccountKind, long> reservedByKind,
        IReadOnlyList<SavedCohort> cohorts,
        IReadOnlyList<SavedBuilding> buildings,
        IReadOnlyList<SavedProject> projects,
        IReadOnlyList<SavedRoadSegment> plannedRoadSegments,
        IReadOnlyList<SavedVacatedLot> vacatedLots,
        IReadOnlyList<SavedLinkQueue> linkQueues,
        IReadOnlyList<SavedGatewayFlow> gatewayFlows,
        IReadOnlyList<SavedRoadWorksZone> roadWorksZones,
        IReadOnlyList<SavedBusRoute> busRoutes,
        IReadOnlyList<SavedSignal> signals,
        IReadOnlyList<SavedIncidentSite> incidentSites)
    {
        MapId = mapId;
        MapContentHash = mapContentHash;
        SimulationVersion = simulationVersion;
        ContentVersion = contentVersion;
        CurrentTick = currentTick;
        Revision = revision;
        MasterSeed = masterSeed;
        RngStates = rngStates;
        RngDrawCounts = rngDrawCounts;
        CashByKind = cashByKind;
        ReservedByKind = reservedByKind;
        Cohorts = cohorts;
        Buildings = buildings;
        Projects = projects;
        PlannedRoadSegments = plannedRoadSegments;
        VacatedLots = vacatedLots;
        LinkQueues = linkQueues;
        GatewayFlows = gatewayFlows;
        RoadWorksZones = roadWorksZones;
        BusRoutes = busRoutes;
        Signals = signals;
        IncidentSites = incidentSites;
    }

    public string MapId { get; }
    public string MapContentHash { get; }
    public string SimulationVersion { get; }
    public string ContentVersion { get; }
    public long CurrentTick { get; }
    public long Revision { get; }
    public long MasterSeed { get; }
    public IReadOnlyDictionary<RandomStreamName, ulong> RngStates { get; }
    public IReadOnlyDictionary<RandomStreamName, long> RngDrawCounts { get; }
    public IReadOnlyDictionary<LedgerAccountKind, long> CashByKind { get; }
    public IReadOnlyDictionary<LedgerAccountKind, long> ReservedByKind { get; }
    public IReadOnlyList<SavedCohort> Cohorts { get; }
    public IReadOnlyList<SavedBuilding> Buildings { get; }
    public IReadOnlyList<SavedProject> Projects { get; }
    public IReadOnlyList<SavedRoadSegment> PlannedRoadSegments { get; }
    public IReadOnlyList<SavedVacatedLot> VacatedLots { get; }

    // --- G4 mobility state (spec §13 / AGENTS.md rule 5: save must cover
    // queues/routes/signal state/incident state too) ---
    public IReadOnlyList<SavedLinkQueue> LinkQueues { get; }
    public IReadOnlyList<SavedGatewayFlow> GatewayFlows { get; }
    public IReadOnlyList<SavedRoadWorksZone> RoadWorksZones { get; }
    public IReadOnlyList<SavedBusRoute> BusRoutes { get; }
    public IReadOnlyList<SavedSignal> Signals { get; }
    public IReadOnlyList<SavedIncidentSite> IncidentSites { get; }
}
