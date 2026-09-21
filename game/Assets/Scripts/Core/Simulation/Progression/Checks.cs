// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;
using System.Linq;
using Thaivia.Core.Simulation.Mobility.Incidents;
using Thaivia.Core.Simulation.Planning;

namespace Thaivia.Core.Simulation.Progression;

/// <summary>
/// A small library of <see cref="IObjectiveCheck"/> implementations, every
/// one of them a thin, pure wrapper around a query WorldState already
/// exposes (ComputeAccessibilityScore, ComputeCohortNeeds, Ledger.Available,
/// Clock.CurrentTick, Projects, IncidentSites) -- this namespace invents no
/// new simulation number of its own; it only reads and compares.
/// </summary>
public sealed class AccessibilityAtLeastCheck : IObjectiveCheck
{
    public AccessibilityAtLeastCheck(long buildingSourceId, int minimumScore)
    {
        BuildingSourceId = buildingSourceId;
        MinimumScore = minimumScore;
    }

    public long BuildingSourceId { get; }
    public int MinimumScore { get; }

    public bool IsSatisfied(WorldState world) => world.ComputeAccessibilityScore(BuildingSourceId) >= MinimumScore;
}

public sealed class CohortNeedAtLeastCheck : IObjectiveCheck
{
    public enum Dimension { Sleep, Access, Safety, Economy }

    public CohortNeedAtLeastCheck(string cohortId, Dimension dimension, int minimumValue, int hourOfDay)
    {
        CohortId = cohortId;
        NeedDimension = dimension;
        MinimumValue = minimumValue;
        HourOfDay = hourOfDay;
    }

    public string CohortId { get; }
    public Dimension NeedDimension { get; }
    public int MinimumValue { get; }
    public int HourOfDay { get; }

    public bool IsSatisfied(WorldState world)
    {
        var needs = world.ComputeCohortNeeds(CohortId, HourOfDay);
        var value = NeedDimension switch
        {
            Dimension.Sleep => needs.Sleep,
            Dimension.Access => needs.Access,
            Dimension.Safety => needs.Safety,
            Dimension.Economy => needs.Economy,
            _ => throw new System.ArgumentOutOfRangeException(),
        };
        return value >= MinimumValue;
    }
}

/// <summary>Satisfied once at least one non-cancelled project of the
/// given kind (or any kind, if <see cref="Kind"/> is null) has been
/// committed. "Committed" here means present in <c>world.Projects</c>
/// with a non-Cancelled status -- a Reserved project (accepted, not yet
/// fully paid) already counts, matching PlanningEngine's own commit-time
/// semantics (a project takes effect on commit, not on final
/// payment).</summary>
public sealed class ProjectCommittedCheck : IObjectiveCheck
{
    public ProjectCommittedCheck(ProjectKind? kind = null)
    {
        Kind = kind;
    }

    public ProjectKind? Kind { get; }

    public bool IsSatisfied(WorldState world) =>
        world.Projects.Values.Any(p => p.Status != ProjectStatus.Cancelled && (Kind is null || p.Kind == Kind));
}

public sealed class BudgetAtLeastCheck : IObjectiveCheck
{
    public BudgetAtLeastCheck(long minimumAvailableThb)
    {
        MinimumAvailableThb = minimumAvailableThb;
    }

    public long MinimumAvailableThb { get; }

    public bool IsSatisfied(WorldState world) => world.Ledger.Available >= MinimumAvailableThb;
}

public sealed class TicksElapsedAtLeastCheck : IObjectiveCheck
{
    public TicksElapsedAtLeastCheck(long minimumTick)
    {
        MinimumTick = minimumTick;
    }

    public long MinimumTick { get; }

    public bool IsSatisfied(WorldState world) => world.Clock.CurrentTick >= MinimumTick;
}

/// <summary>Satisfied when no registered IncidentSite is currently
/// Active -- a "keep the area calm" style objective.</summary>
public sealed class NoActiveIncidentCheck : IObjectiveCheck
{
    public bool IsSatisfied(WorldState world) => world.IncidentSites.All(s => s.Phase != IncidentPhase.Active);
}

/// <summary>Combines several checks with AND semantics -- lets a scenario
/// author express "commit a project AND keep some budget in reserve" as
/// one objective without writing a new IObjectiveCheck type.</summary>
public sealed class AllOfCheck : IObjectiveCheck
{
    public AllOfCheck(params IObjectiveCheck[] checks)
    {
        Checks = checks;
    }

    public IReadOnlyList<IObjectiveCheck> Checks { get; }

    public bool IsSatisfied(WorldState world) => Checks.All(c => c.IsSatisfied(world));
}
