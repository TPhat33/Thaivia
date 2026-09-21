// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;

namespace Thaivia.Core.Simulation.Save;

/// <summary>Flat, get-only DTOs for the parts of a <see cref="SaveGame"/>
/// that are themselves collections of records. Archetype/kind/status enums
/// are stored as their string name (not their numeric value) so a save
/// file stays readable/diffable and does not silently reinterpret itself
/// if an enum is ever reordered.</summary>
public sealed class SavedCohort
{
    public SavedCohort(string id, long homeBuildingSourceId, int householdCount, int peoplePerHousehold, int jobsHeld)
    {
        Id = id;
        HomeBuildingSourceId = homeBuildingSourceId;
        HouseholdCount = householdCount;
        PeoplePerHousehold = peoplePerHousehold;
        JobsHeld = jobsHeld;
    }

    public string Id { get; }
    public long HomeBuildingSourceId { get; }
    public int HouseholdCount { get; }
    public int PeoplePerHousehold { get; }
    public int JobsHeld { get; }
}

public sealed class SavedBuilding
{
    public SavedBuilding(long sourceId, string archetype, double localX, double localZ, long nearestRoadNodeId, int jobsCount, bool relocated)
    {
        SourceId = sourceId;
        Archetype = archetype;
        LocalX = localX;
        LocalZ = localZ;
        NearestRoadNodeId = nearestRoadNodeId;
        JobsCount = jobsCount;
        Relocated = relocated;
    }

    public long SourceId { get; }
    public string Archetype { get; }
    public double LocalX { get; }
    public double LocalZ { get; }
    public long NearestRoadNodeId { get; }
    public int JobsCount { get; }
    public bool Relocated { get; }
}

public sealed class SavedProject
{
    public SavedProject(string id, string kind, string ledgerKind, long fixedCostThb, IReadOnlyList<long> milestoneAmounts, int paidMilestones, long totalPaid, string status)
    {
        Id = id;
        Kind = kind;
        LedgerKind = ledgerKind;
        FixedCostThb = fixedCostThb;
        MilestoneAmounts = milestoneAmounts;
        PaidMilestones = paidMilestones;
        TotalPaid = totalPaid;
        Status = status;
    }

    public string Id { get; }
    public string Kind { get; }
    public string LedgerKind { get; }
    public long FixedCostThb { get; }
    public IReadOnlyList<long> MilestoneAmounts { get; }
    public int PaidMilestones { get; }
    public long TotalPaid { get; }
    public string Status { get; }
}

public sealed class SavedRoadSegment
{
    public SavedRoadSegment(long fromNodeId, long toNodeId, double lengthMeters, string projectId)
    {
        FromNodeId = fromNodeId;
        ToNodeId = toNodeId;
        LengthMeters = lengthMeters;
        ProjectId = projectId;
    }

    public long FromNodeId { get; }
    public long ToNodeId { get; }
    public double LengthMeters { get; }
    public string ProjectId { get; }
}

public sealed class SavedVacatedLot
{
    public SavedVacatedLot(long buildingSourceId, double oldLocalX, double oldLocalZ, string projectId)
    {
        BuildingSourceId = buildingSourceId;
        OldLocalX = oldLocalX;
        OldLocalZ = oldLocalZ;
        ProjectId = projectId;
    }

    public long BuildingSourceId { get; }
    public double OldLocalX { get; }
    public double OldLocalZ { get; }
    public string ProjectId { get; }
}

// --- G4 mobility state (queues/routes/signals/incidents) ---
// The four categories spec §13/AGENTS.md rule 5 require a save to cover
// for G4: network queues, bus routes, signal state, incident state --
// plus the gateway conservation ledger (see ADR-0019), which is the
// hardest-to-lose state of all.

/// <summary>Mirrors one <see cref="Mobility.Queues.LinkKey"/> +
/// <see cref="Mobility.Queues.LinkQueueSimulator"/> entry.</summary>
public sealed class SavedLinkQueue
{
    public SavedLinkQueue(long wayId, bool forward, long queueLength, long totalCompleted, long totalArrived)
    {
        WayId = wayId;
        Forward = forward;
        QueueLength = queueLength;
        TotalCompleted = totalCompleted;
        TotalArrived = totalArrived;
    }

    public long WayId { get; }
    public bool Forward { get; }
    public long QueueLength { get; }
    public long TotalCompleted { get; }
    public long TotalArrived { get; }
}

/// <summary>Mirrors one <see cref="Mobility.Gateways.GatewayFlow"/> --
/// see ADR-0019 for why every one of these fields must round-trip exactly
/// for the conservation invariant to keep holding across a save/restore.</summary>
public sealed class SavedGatewayFlow
{
    public SavedGatewayFlow(
        long gatewayNodeId, int inboundCapacityPerTick, int outboundCapacityPerTick, bool isOpen,
        long generatedOutbound, long completedOutbound, long pendingOutbound,
        long generatedInbound, long completedInbound, long pendingInbound)
    {
        GatewayNodeId = gatewayNodeId;
        InboundCapacityPerTick = inboundCapacityPerTick;
        OutboundCapacityPerTick = outboundCapacityPerTick;
        IsOpen = isOpen;
        GeneratedOutbound = generatedOutbound;
        CompletedOutbound = completedOutbound;
        PendingOutbound = pendingOutbound;
        GeneratedInbound = generatedInbound;
        CompletedInbound = completedInbound;
        PendingInbound = pendingInbound;
    }

    public long GatewayNodeId { get; }
    public int InboundCapacityPerTick { get; }
    public int OutboundCapacityPerTick { get; }
    public bool IsOpen { get; }
    public long GeneratedOutbound { get; }
    public long CompletedOutbound { get; }
    public long PendingOutbound { get; }
    public long GeneratedInbound { get; }
    public long CompletedInbound { get; }
    public long PendingInbound { get; }
}

public sealed class SavedRoadWorksZone
{
    public SavedRoadWorksZone(string id, long wayId, long startTick, long durationTicks, double capacityMultiplierDuringConstruction, string projectId)
    {
        Id = id;
        WayId = wayId;
        StartTick = startTick;
        DurationTicks = durationTicks;
        CapacityMultiplierDuringConstruction = capacityMultiplierDuringConstruction;
        ProjectId = projectId;
    }

    public string Id { get; }
    public long WayId { get; }
    public long StartTick { get; }
    public long DurationTicks { get; }
    public double CapacityMultiplierDuringConstruction { get; }
    public string ProjectId { get; }
}

public sealed class SavedBusRoute
{
    public SavedBusRoute(string id, IReadOnlyList<long> stopNodeIds, int dwellTicksPerStop, int vehicleCount, int capacityPerVehicle, long cumulativeRidership)
    {
        Id = id;
        StopNodeIds = stopNodeIds;
        DwellTicksPerStop = dwellTicksPerStop;
        VehicleCount = vehicleCount;
        CapacityPerVehicle = capacityPerVehicle;
        CumulativeRidership = cumulativeRidership;
    }

    public string Id { get; }
    public IReadOnlyList<long> StopNodeIds { get; }
    public int DwellTicksPerStop { get; }
    public int VehicleCount { get; }
    public int CapacityPerVehicle { get; }
    public long CumulativeRidership { get; }
}

/// <summary>Mirrors one <see cref="Mobility.Signals.SignalInstance"/> --
/// both its config (Kind/CycleTicks/ConfigValue, needed to rebuild the
/// exact same ISignalPlan) and its live SignalIntersectionSimulator
/// state.</summary>
public sealed class SavedSignal
{
    public SavedSignal(
        string id, long nodeId, string kind, long cycleTicks, int configValue, int dischargeRatePerGreenTick,
        long queueA, long queueB, int currentGreenTicksA, int currentGreenTicksB, long cumulativeQueueTicksA, long cumulativeQueueTicksB, long ticksSimulated)
    {
        Id = id;
        NodeId = nodeId;
        Kind = kind;
        CycleTicks = cycleTicks;
        ConfigValue = configValue;
        DischargeRatePerGreenTick = dischargeRatePerGreenTick;
        QueueA = queueA;
        QueueB = queueB;
        CurrentGreenTicksA = currentGreenTicksA;
        CurrentGreenTicksB = currentGreenTicksB;
        CumulativeQueueTicksA = cumulativeQueueTicksA;
        CumulativeQueueTicksB = cumulativeQueueTicksB;
        TicksSimulated = ticksSimulated;
    }

    public string Id { get; }
    public long NodeId { get; }
    public string Kind { get; }
    public long CycleTicks { get; }
    public int ConfigValue { get; }
    public int DischargeRatePerGreenTick { get; }
    public long QueueA { get; }
    public long QueueB { get; }
    public int CurrentGreenTicksA { get; }
    public int CurrentGreenTicksB { get; }
    public long CumulativeQueueTicksA { get; }
    public long CumulativeQueueTicksB { get; }
    public long TicksSimulated { get; }
}

/// <summary>Mirrors one <see cref="Mobility.Incidents.IncidentSite"/>.</summary>
public sealed class SavedIncidentSite
{
    public SavedIncidentSite(string siteId, string strand, string phase, long ticksInPhase, long warningsIssued, long incidentsTriggered, int lastSeverity)
    {
        SiteId = siteId;
        Strand = strand;
        Phase = phase;
        TicksInPhase = ticksInPhase;
        WarningsIssued = warningsIssued;
        IncidentsTriggered = incidentsTriggered;
        LastSeverity = lastSeverity;
    }

    public string SiteId { get; }
    public string Strand { get; }
    public string Phase { get; }
    public long TicksInPhase { get; }
    public long WarningsIssued { get; }
    public long IncidentsTriggered { get; }
    public int LastSeverity { get; }
}
