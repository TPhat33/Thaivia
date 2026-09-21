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
