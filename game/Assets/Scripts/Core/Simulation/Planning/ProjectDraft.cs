// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Planning;

/// <summary>
/// A player's draft plan before it is committed (spec §12: "Draft มี
/// baseWorldRevision; estimate ไม่แตะ RNG/world จริง"). Two concrete
/// subclasses (<see cref="BuildingRelocationDraft"/>,
/// <see cref="NewRoadConnectorDraft"/>) carry the specific fields each
/// project kind needs; the shared base only holds what every draft has:
/// an id (used as the idempotency key for commit/re-confirm), the world
/// revision it was estimated against, a definite fixed cost, and an
/// uncertain predicted impact (see <see cref="ImpactRange"/>'s doc
/// comment for why those two are different shapes on purpose).
/// </summary>
public abstract class ProjectDraft
{
    protected ProjectDraft(string id, ProjectKind kind, long baseWorldRevision, long fixedCostThb, ImpactRange predictedImpact)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("ProjectDraft.Id must not be empty.", nameof(id));
        }

        if (fixedCostThb < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedCostThb));
        }

        Id = id;
        Kind = kind;
        BaseWorldRevision = baseWorldRevision;
        FixedCostThb = fixedCostThb;
        PredictedImpact = predictedImpact;
    }

    public string Id { get; }
    public ProjectKind Kind { get; }
    public long BaseWorldRevision { get; }
    public long FixedCostThb { get; }
    public ImpactRange PredictedImpact { get; }
}

/// <summary>Move an existing GeographyBase building (by its source id) to
/// a destination local-space location, anchored at an existing road-graph
/// node for accessibility purposes (plan §8: "การย้าย = destination
/// geometry + project cost + occupants/business transfer +
/// old-location treatment"). No free teleport: <see cref="ProjectDraft.FixedCostThb"/>
/// is required and validated positive-or-zero by the base constructor,
/// and PlanningEngine additionally requires the caller to actually spend
/// budget on commit (see PlanningEngineTests for a same-cost-as-zero
/// rejection... actually cost=0 is technically allowed by the type, but
/// no scenario in this codebase seeds a zero-cost relocation).</summary>
public sealed class BuildingRelocationDraft : ProjectDraft
{
    public BuildingRelocationDraft(
        string id,
        long baseWorldRevision,
        long fixedCostThb,
        ImpactRange predictedImpact,
        long buildingSourceId,
        double destLocalX,
        double destLocalZ,
        long destNearestRoadNodeId)
        : base(id, ProjectKind.BuildingRelocation, baseWorldRevision, fixedCostThb, predictedImpact)
    {
        BuildingSourceId = buildingSourceId;
        DestLocalX = destLocalX;
        DestLocalZ = destLocalZ;
        DestNearestRoadNodeId = destNearestRoadNodeId;
    }

    public long BuildingSourceId { get; }
    public double DestLocalX { get; }
    public double DestLocalZ { get; }
    public long DestNearestRoadNodeId { get; }
}

/// <summary>Connects two EXISTING road-graph nodes with a new bidirectional
/// segment (see Thaivia.Core.Simulation.Accessibility.PlannedRoadSegment's
/// doc comment for the G3 scope decision behind "existing nodes only").</summary>
public sealed class NewRoadConnectorDraft : ProjectDraft
{
    public NewRoadConnectorDraft(
        string id,
        long baseWorldRevision,
        long fixedCostThb,
        ImpactRange predictedImpact,
        long fromNodeId,
        long toNodeId)
        : base(id, ProjectKind.NewRoadConnector, baseWorldRevision, fixedCostThb, predictedImpact)
    {
        FromNodeId = fromNodeId;
        ToNodeId = toNodeId;
    }

    public long FromNodeId { get; }
    public long ToNodeId { get; }
}
