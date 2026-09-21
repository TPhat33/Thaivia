// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Thaivia.Core.Simulation.Planning;

/// <summary>
/// A project's financial/status record after commit. Geometry effects
/// (the building's new BuildingSimState, or a new
/// Thaivia.Core.Simulation.Accessibility.PlannedRoadSegment) are applied
/// to WorldState immediately at commit time and are NOT re-derived from
/// this type afterwards -- this type only tracks money/status, which is
/// what makes it cheap to persist and restore verbatim in a save file
/// (see Thaivia.Core.Simulation.Save.SavedProject).
///
/// Instances are replaced, never mutated in place (see
/// <see cref="WithMilestonePaid"/>/<see cref="WithCancelled"/>) --
/// consistent with every other simulation-state type in this codebase.
/// </summary>
public sealed class CommittedProject
{
    public CommittedProject(
        string id,
        ProjectKind kind,
        Economy.LedgerAccountKind ledgerKind,
        long fixedCostThb,
        IReadOnlyList<long> milestoneAmounts,
        int paidMilestones,
        long totalPaid,
        ProjectStatus status)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("CommittedProject.Id must not be empty.", nameof(id));
        }

        if (milestoneAmounts.Count == 0)
        {
            throw new ArgumentException("A committed project must have at least one milestone.", nameof(milestoneAmounts));
        }

        if (milestoneAmounts.Sum() != fixedCostThb)
        {
            throw new ArgumentException("Milestone amounts must sum exactly to the project's fixed cost.", nameof(milestoneAmounts));
        }

        if (paidMilestones < 0 || paidMilestones > milestoneAmounts.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(paidMilestones));
        }

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
    public ProjectKind Kind { get; }
    public Economy.LedgerAccountKind LedgerKind { get; }
    public long FixedCostThb { get; }
    public IReadOnlyList<long> MilestoneAmounts { get; }
    public int PaidMilestones { get; }
    public long TotalPaid { get; }
    public ProjectStatus Status { get; }

    public CommittedProject WithMilestonePaid(long amount)
    {
        var newPaidMilestones = PaidMilestones + 1;
        var newStatus = newPaidMilestones >= MilestoneAmounts.Count ? ProjectStatus.Completed : ProjectStatus.PartiallyPaid;
        return new CommittedProject(Id, Kind, LedgerKind, FixedCostThb, MilestoneAmounts, newPaidMilestones, TotalPaid + amount, newStatus);
    }

    public CommittedProject WithCancelled() =>
        new(Id, Kind, LedgerKind, FixedCostThb, MilestoneAmounts, PaidMilestones, TotalPaid, ProjectStatus.Cancelled);
}
