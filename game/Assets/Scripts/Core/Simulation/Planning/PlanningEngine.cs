// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;
using System.Linq;
using Thaivia.Core.Simulation.Accessibility;
using Thaivia.Core.Simulation.Buildings;
using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.Mobility.Queues;
using Thaivia.Core.Simulation.Mobility.RoadWorks;

namespace Thaivia.Core.Simulation.Planning;

/// <summary>The outcome of a commit attempt: either the project (newly
/// created, or the already-committed one on an idempotent re-confirm) with
/// no failures, or no project at all with every validation failure that
/// was found. There is no partial-success shape -- see PlanningEngine's
/// commit methods for why that is a structural guarantee, not just a
/// convention.</summary>
public sealed record CommitResult(bool Success, CommittedProject? Project, IReadOnlyList<string> Failures);

/// <summary>
/// Orchestrates the draft -> estimate -> commit -> pay/cancel flow (spec
/// §12). Every mutating method here is the ONLY code path allowed to call
/// WorldState's internal Register*/IncrementRevision members (see
/// WorldState's doc comment) -- so this is the single place the "reserve
/// then pay, never double-charge", "commit is one transaction", and
/// "cancelling a draft is free" invariants are enforced.
/// </summary>
public static class PlanningEngine
{
    /// <summary>Pure: reads WorldState's CURRENT accessibility graph and
    /// building state to project what a relocation's accessibility impact
    /// would be, but calls no RNG, reserves no budget, and registers
    /// nothing -- see EstimateDoesNotMutateWorldTests for the proof (world
    /// hash + every RNG stream's draw count/state unchanged before/after).</summary>
    public static ImpactRange EstimateRelocationAccessImpact(WorldState world, long buildingSourceId, double destLocalX, double destLocalZ, long destNearestRoadNodeId)
    {
        if (!world.BuildingStates.ContainsKey(buildingSourceId))
        {
            throw new ArgumentException($"Unknown building source id {buildingSourceId}.", nameof(buildingSourceId));
        }

        var currentScore = world.ComputeAccessibilityScore(buildingSourceId);
        var graph = world.BuildAccessibilityGraph();
        var nearestJobNode = world.FindNearestJobNode(graph, destNearestRoadNodeId, excludeBuildingSourceId: buildingSourceId);
        var projectedDistance = nearestJobNode is { } nodeId ? graph.ShortestDistanceMeters(destNearestRoadNodeId, nodeId) : null;
        var projectedScore = AccessibilityNeed.ComputeScore(projectedDistance);
        var delta = projectedScore - currentScore;

        // A small, documented uncertainty band -- illustrative, not a
        // statistically fitted confidence interval (G3 scope decision;
        // see docs/progress.md). The point is structural: this method
        // NEVER returns a bare number, only a range (see ImpactRange).
        return new ImpactRange(delta, delta - 5, delta + 5, "cohort_access_need_score_delta");
    }

    /// <summary>Pure equivalent of <see cref="EstimateRelocationAccessImpact"/>
    /// for a new road connector draft: projects the access-score delta for
    /// the building nearest <paramref name="fromNodeId"/>, without
    /// registering the segment.</summary>
    public static ImpactRange EstimateNewRoadAccessImpact(WorldState world, long fromNodeId, long toNodeId, long evaluateForBuildingSourceId)
    {
        if (!world.RoadNodeIds.Contains(fromNodeId) || !world.RoadNodeIds.Contains(toNodeId))
        {
            throw new ArgumentException("Both fromNodeId and toNodeId must already exist in the road graph.");
        }

        var currentScore = world.ComputeAccessibilityScore(evaluateForBuildingSourceId);
        var length = SegmentLength(world, fromNodeId, toNodeId);
        var projectedGraph = new AccessibilityGraph(world.RoadGraph, world.PlannedRoadSegments
            .Append(new PlannedRoadSegment(fromNodeId, toNodeId, length, "estimate-preview"))
            .ToList());

        var building = world.BuildingStates[evaluateForBuildingSourceId];
        var nearestJobNode = world.FindNearestJobNode(projectedGraph, building.NearestRoadNodeId, excludeBuildingSourceId: evaluateForBuildingSourceId);
        var projectedDistance = nearestJobNode is { } nodeId ? projectedGraph.ShortestDistanceMeters(building.NearestRoadNodeId, nodeId) : null;
        var projectedScore = AccessibilityNeed.ComputeScore(projectedDistance);
        var delta = projectedScore - currentScore;

        return new ImpactRange(delta, delta - 5, delta + 5, "cohort_access_need_score_delta");
    }

    /// <summary>Pure: projects a road works zone's effect on its way's
    /// per-tick throughput capacity (definite, from
    /// <see cref="LinkCapacity.BaseCapacityVehPerTick"/> and the proposed
    /// multiplier) -- but still returned as an <see cref="ImpactRange"/>,
    /// never a bare number, because the actual queueing consequence
    /// downstream (how much backlog this produces) depends on demand this
    /// method does not observe, which is exactly the kind of prediction
    /// uncertainty ImpactRange exists to carry (spec §12). Touches no RNG,
    /// reserves no budget, registers nothing.</summary>
    public static ImpactRange EstimateRoadWorksCapacityImpact(WorldState world, long wayId, double capacityMultiplierDuringConstruction)
    {
        var edge = world.RoadGraph.Edges.FirstOrDefault(e => e.WayId == wayId)
            ?? throw new ArgumentException($"Way {wayId} does not exist in the road graph.", nameof(wayId));

        var baseCapacity = LinkCapacity.BaseCapacityVehPerTick(edge);
        var reducedCapacity = (int)Math.Floor(baseCapacity * capacityMultiplierDuringConstruction);
        var delta = (double)(reducedCapacity - baseCapacity); // always <= 0.

        // A small, documented uncertainty band -- same illustrative style
        // as EstimateRelocationAccessImpact/EstimateNewRoadAccessImpact,
        // not a statistically fitted interval.
        const double band = 1.0;
        return new ImpactRange(delta, delta - band, delta + band, "way_capacity_delta_veh_per_tick");
    }

    public static CommitResult CommitRelocation(WorldState world, BuildingRelocationDraft draft, LedgerAccountKind ledgerKind, IReadOnlyList<long> milestoneAmounts)
    {
        if (world.Projects.TryGetValue(draft.Id, out var existing) && existing.Status != ProjectStatus.Cancelled)
        {
            // Idempotent re-confirm: same draft id, already committed and
            // not cancelled -- return the existing project untouched. No
            // second Reserve, no second geometry application, no second
            // revision bump.
            return new CommitResult(true, existing, Array.Empty<string>());
        }

        var failures = new List<string>();
        if (!world.BuildingStates.ContainsKey(draft.BuildingSourceId))
        {
            failures.Add($"building {draft.BuildingSourceId} does not exist in this world.");
        }

        if (!world.RoadNodeIds.Contains(draft.DestNearestRoadNodeId))
        {
            failures.Add($"destination road node {draft.DestNearestRoadNodeId} does not exist in the road graph.");
        }

        ValidateMilestones(milestoneAmounts, draft.FixedCostThb, failures);

        if (draft.FixedCostThb > world.Ledger.Available)
        {
            failures.Add($"insufficient available budget: need {draft.FixedCostThb}, have {world.Ledger.Available}.");
        }

        if (failures.Count > 0)
        {
            // Every check above ran BEFORE any mutation below -- a failed
            // check here means nothing has been applied yet, so there is
            // nothing to roll back: budget, geometry and revision are all
            // still exactly what they were before this call.
            return new CommitResult(false, null, failures);
        }

        var building = world.BuildingStates[draft.BuildingSourceId];
        world.Ledger.Reserve(ledgerKind, draft.FixedCostThb);
        world.RegisterVacatedLot(new Buildings.VacatedLot(building.SourceId, building.LocalX, building.LocalZ, draft.Id));
        world.ReplaceBuildingState(building.WithLocation(draft.DestLocalX, draft.DestLocalZ, draft.DestNearestRoadNodeId));

        var project = new CommittedProject(draft.Id, ProjectKind.BuildingRelocation, ledgerKind, draft.FixedCostThb, milestoneAmounts, paidMilestones: 0, totalPaid: 0, ProjectStatus.Reserved);
        world.RegisterProject(project);
        world.IncrementRevision();
        world.AddDelta(new PlayerDelta(draft.Id, PlayerDeltaKind.BuildingRelocationProposal, DateTimeOffset.UtcNow,
            $"Relocate building {draft.BuildingSourceId} to ({draft.DestLocalX:F1}, {draft.DestLocalZ:F1})."));

        return new CommitResult(true, project, Array.Empty<string>());
    }

    public static CommitResult CommitNewRoadConnector(WorldState world, NewRoadConnectorDraft draft, LedgerAccountKind ledgerKind, IReadOnlyList<long> milestoneAmounts)
    {
        if (world.Projects.TryGetValue(draft.Id, out var existing) && existing.Status != ProjectStatus.Cancelled)
        {
            return new CommitResult(true, existing, Array.Empty<string>());
        }

        var failures = new List<string>();
        if (!world.RoadNodeIds.Contains(draft.FromNodeId))
        {
            failures.Add($"road node {draft.FromNodeId} does not exist in the road graph.");
        }

        if (!world.RoadNodeIds.Contains(draft.ToNodeId))
        {
            failures.Add($"road node {draft.ToNodeId} does not exist in the road graph.");
        }

        if (draft.FromNodeId == draft.ToNodeId)
        {
            failures.Add("a road connector must join two distinct nodes.");
        }

        ValidateMilestones(milestoneAmounts, draft.FixedCostThb, failures);

        if (draft.FixedCostThb > world.Ledger.Available)
        {
            failures.Add($"insufficient available budget: need {draft.FixedCostThb}, have {world.Ledger.Available}.");
        }

        if (failures.Count > 0)
        {
            return new CommitResult(false, null, failures);
        }

        var length = SegmentLength(world, draft.FromNodeId, draft.ToNodeId);
        world.Ledger.Reserve(ledgerKind, draft.FixedCostThb);
        world.RegisterPlannedRoadSegment(new PlannedRoadSegment(draft.FromNodeId, draft.ToNodeId, length, draft.Id));

        var project = new CommittedProject(draft.Id, ProjectKind.NewRoadConnector, ledgerKind, draft.FixedCostThb, milestoneAmounts, paidMilestones: 0, totalPaid: 0, ProjectStatus.Reserved);
        world.RegisterProject(project);
        world.IncrementRevision();
        world.AddDelta(new PlayerDelta(draft.Id, PlayerDeltaKind.NewRoadProposal, DateTimeOffset.UtcNow,
            $"New road connector {draft.FromNodeId} -> {draft.ToNodeId}."));

        return new CommitResult(true, project, Array.Empty<string>());
    }

    /// <summary>Commits a road-works construction zone through the same
    /// reserve/pay-milestone/cancel budget flow as every other project
    /// kind (see ADR-0023 -- this replaces WorldState.AddRoadWorksZone as
    /// the player-facing path; that method remains for direct test/manual
    /// setup only). The zone's construction window starts at the world's
    /// CURRENT tick, so committing takes effect immediately, exactly like
    /// a relocation's new location or a new road's segment do.</summary>
    public static CommitResult CommitRoadWorks(WorldState world, RoadWorksDraft draft, LedgerAccountKind ledgerKind, IReadOnlyList<long> milestoneAmounts)
    {
        if (world.Projects.TryGetValue(draft.Id, out var existing) && existing.Status != ProjectStatus.Cancelled)
        {
            return new CommitResult(true, existing, Array.Empty<string>());
        }

        var failures = new List<string>();
        var edge = world.RoadGraph.Edges.FirstOrDefault(e => e.WayId == draft.WayId);
        if (edge is null)
        {
            failures.Add($"way {draft.WayId} does not exist in the road graph.");
        }

        if (draft.DurationTicks <= 0)
        {
            failures.Add("road works duration must be positive.");
        }

        if (draft.CapacityMultiplierDuringConstruction <= 0 || draft.CapacityMultiplierDuringConstruction > 1)
        {
            failures.Add("capacity multiplier during construction must be in (0, 1].");
        }

        ValidateMilestones(milestoneAmounts, draft.FixedCostThb, failures);

        if (draft.FixedCostThb > world.Ledger.Available)
        {
            failures.Add($"insufficient available budget: need {draft.FixedCostThb}, have {world.Ledger.Available}.");
        }

        if (failures.Count > 0)
        {
            // Same discipline as every other Commit* method: every check
            // above ran before any mutation below, so a failure here means
            // nothing was applied -- budget, road-works zone list and
            // revision are all exactly what they were before this call.
            return new CommitResult(false, null, failures);
        }

        world.Ledger.Reserve(ledgerKind, draft.FixedCostThb);
        world.AddRoadWorksZone(new RoadWorksZone(draft.Id, draft.WayId, world.Clock.CurrentTick, draft.DurationTicks, draft.CapacityMultiplierDuringConstruction, draft.Id));

        var project = new CommittedProject(draft.Id, ProjectKind.RoadWorks, ledgerKind, draft.FixedCostThb, milestoneAmounts, paidMilestones: 0, totalPaid: 0, ProjectStatus.Reserved);
        world.RegisterProject(project);
        world.IncrementRevision();
        world.AddDelta(new PlayerDelta(draft.Id, PlayerDeltaKind.RoadWorksProposal, DateTimeOffset.UtcNow,
            $"Road works on way {draft.WayId} for {draft.DurationTicks} ticks (capacity x{draft.CapacityMultiplierDuringConstruction:F2})."));

        return new CommitResult(true, project, Array.Empty<string>());
    }

    /// <summary>Pays the next unpaid milestone in order. Throws (does not
    /// silently no-op) if the project is already Completed/Cancelled or if
    /// the requested index is not the next payable one -- paying the same
    /// milestone index twice is refused outright rather than quietly
    /// charging twice.</summary>
    public static void PayMilestone(WorldState world, string projectId, int milestoneIndex)
    {
        if (!world.Projects.TryGetValue(projectId, out var project))
        {
            throw new ArgumentException($"Unknown project {projectId}.", nameof(projectId));
        }

        if (project.Status is ProjectStatus.Completed or ProjectStatus.Cancelled)
        {
            throw new InvalidOperationException($"Project {projectId} is {project.Status}; cannot pay further milestones.");
        }

        if (milestoneIndex != project.PaidMilestones)
        {
            throw new InvalidOperationException(
                $"Milestone {milestoneIndex} is not payable: the next payable index for project {projectId} is {project.PaidMilestones}.");
        }

        var amount = project.MilestoneAmounts[milestoneIndex];
        world.Ledger.ChargeFromReservation(project.LedgerKind, amount);
        world.RegisterProject(project.WithMilestonePaid(amount));
    }

    /// <summary>A draft that was only ever estimated never reserved
    /// anything (Estimate* touches no ledger) -- so cancelling it is
    /// unconditionally free and needs no WorldState access at all. This
    /// method exists purely to give that rule a name callers can point
    /// to/test against.</summary>
    public static void CancelDraft()
    {
    }

    public static void CancelProject(WorldState world, string projectId, long cancellationFeeThb)
    {
        if (!world.Projects.TryGetValue(projectId, out var project))
        {
            throw new ArgumentException($"Unknown project {projectId}.", nameof(projectId));
        }

        if (project.Status == ProjectStatus.Cancelled)
        {
            return; // idempotent.
        }

        if (project.Status == ProjectStatus.Completed)
        {
            throw new InvalidOperationException($"Project {projectId} is already Completed; nothing left to cancel/release.");
        }

        var remainingReserved = project.FixedCostThb - project.TotalPaid;
        world.Ledger.ReleaseReservation(project.LedgerKind, remainingReserved);
        if (cancellationFeeThb > 0)
        {
            world.Ledger.ChargeDirect(project.LedgerKind, cancellationFeeThb);
        }

        if (project.Kind == ProjectKind.RoadWorks)
        {
            // Cancellation rule specific to this project kind: construction
            // stops having effect from the moment of cancellation onward --
            // the zone's window is truncated to end at the current tick
            // rather than continuing to degrade capacity for a project that
            // is no longer funded. Ticks already inside the window before
            // cancellation keep whatever degradation already happened (the
            // LinkQueueSimulator backlog they caused is real history, not
            // rewritten), but no FUTURE tick is affected.
            world.TruncateActiveRoadWorksZone(projectId, world.Clock.CurrentTick);
        }

        world.RegisterProject(project.WithCancelled());
    }

    private static void ValidateMilestones(IReadOnlyList<long> milestoneAmounts, long fixedCostThb, List<string> failures)
    {
        if (milestoneAmounts.Count == 0)
        {
            failures.Add("at least one milestone amount is required.");
            return;
        }

        if (milestoneAmounts.Any(a => a <= 0))
        {
            failures.Add("every milestone amount must be positive.");
        }

        if (milestoneAmounts.Sum() != fixedCostThb)
        {
            failures.Add($"milestone amounts must sum exactly to the fixed cost ({fixedCostThb}), got {milestoneAmounts.Sum()}.");
        }
    }

    private static double SegmentLength(WorldState world, long fromNodeId, long toNodeId)
    {
        var nodeById = world.RoadGraph.Nodes.ToDictionary(n => n.NodeId);
        var a = nodeById[fromNodeId];
        var b = nodeById[toNodeId];
        var dx = a.LocalX - b.LocalX;
        var dz = a.LocalZ - b.LocalZ;
        return Math.Sqrt(dx * dx + dz * dz);
    }
}
