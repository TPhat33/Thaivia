using System.Collections.Generic;
using System.Linq;
using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.Planning;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

public class PlanningEngineTests
{
    private static BuildingRelocationDraft MakeRelocationDraft(WorldState world, string id = "draft-reloc-1", long cost = 200_000) =>
        new(id, world.Revision, cost, PlanningEngine.EstimateRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4),
            SimulationFixtures.ResidentialBuildingId, destLocalX: 22, destLocalZ: 3, destNearestRoadNodeId: SimulationFixtures.Node4);

    private static NewRoadConnectorDraft MakeRoadDraft(WorldState world, string id = "draft-road-1", long cost = 100_000) =>
        new(id, world.Revision, cost, PlanningEngine.EstimateNewRoadAccessImpact(world, SimulationFixtures.Node2, SimulationFixtures.Node3, SimulationFixtures.ResidentialBuildingId),
            SimulationFixtures.Node2, SimulationFixtures.Node3);

    [Fact]
    public void Estimate_DoesNotMutateTheWorld_HashAndEveryRngStreamUnchanged()
    {
        var world = SimulationFixtures.BuildWorldState();
        var hashBefore = world.ComputeStructuralHash();
        var statesBefore = world.RandomStreams.CaptureStates();
        var drawsBefore = world.RandomStreams.CaptureDrawCounts();

        _ = PlanningEngine.EstimateRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        _ = PlanningEngine.EstimateNewRoadAccessImpact(world, SimulationFixtures.Node2, SimulationFixtures.Node3, SimulationFixtures.ResidentialBuildingId);

        Assert.Equal(hashBefore, world.ComputeStructuralHash());
        Assert.Equal(statesBefore, world.RandomStreams.CaptureStates());
        Assert.Equal(drawsBefore, world.RandomStreams.CaptureDrawCounts());
        Assert.Equal(0, world.Revision);
    }

    [Fact]
    public void Estimate_ReturnsARange_NeverASingleDefiniteNumber()
    {
        var world = SimulationFixtures.BuildWorldState();
        var impact = PlanningEngine.EstimateRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        Assert.True(impact.MinValue <= impact.ExpectedValue);
        Assert.True(impact.ExpectedValue <= impact.MaxValue);
        Assert.True(impact.MinValue < impact.MaxValue); // a genuine band, not a fixed-width-zero fake range.
    }

    [Fact]
    public void CommitRelocation_ReservesExactlyTheFixedCost_NoMore()
    {
        var world = SimulationFixtures.BuildWorldState();
        var cashBefore = world.Ledger.Available;
        var draft = MakeRelocationDraft(world);

        var result = PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, new long[] { 200_000 });

        Assert.True(result.Success, string.Join("; ", result.Failures));
        Assert.Equal(cashBefore - 200_000, world.Ledger.Available);
        Assert.Equal(200_000, world.Ledger.ReservedOf(LedgerAccountKind.Capex));
        Assert.Equal(1, world.Revision);
    }

    [Fact]
    public void ConfirmingTheSameDraftTwice_DoesNotDeductBudgetTwice()
    {
        var world = SimulationFixtures.BuildWorldState();
        var draft = MakeRelocationDraft(world);

        var first = PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, new long[] { 200_000 });
        var availableAfterFirst = world.Ledger.Available;
        var revisionAfterFirst = world.Revision;

        var second = PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, new long[] { 200_000 });

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Same(first.Project, second.Project); // the exact same object -- no second project was created.
        Assert.Equal(availableAfterFirst, world.Ledger.Available); // unchanged by the second confirm.
        Assert.Equal(revisionAfterFirst, world.Revision); // no second revision bump either.
    }

    [Fact]
    public void AcceptingThenPayingMilestones_NeverDoubleCharges_TotalDeductedEqualsFixedCostExactlyOnce()
    {
        var world = SimulationFixtures.BuildWorldState();
        var cashBefore = world.Ledger.Cash;
        var draft = MakeRelocationDraft(world, cost: 300_000);
        var milestones = new long[] { 100_000, 100_000, 100_000 };

        var commit = PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, milestones);
        Assert.True(commit.Success, string.Join("; ", commit.Failures));

        // Accepting alone must not have deducted cash yet -- only reserved it.
        Assert.Equal(cashBefore, world.Ledger.Cash);
        Assert.Equal(300_000, world.Ledger.ReservedOf(LedgerAccountKind.Capex));

        PlanningEngine.PayMilestone(world, draft.Id, 0);
        PlanningEngine.PayMilestone(world, draft.Id, 1);
        PlanningEngine.PayMilestone(world, draft.Id, 2);

        Assert.Equal(cashBefore - 300_000, world.Ledger.Cash); // deducted exactly once, in total.
        Assert.Equal(0, world.Ledger.ReservedOf(LedgerAccountKind.Capex));
        Assert.Equal(ProjectStatus.Completed, world.Projects[draft.Id].Status);

        // Paying an already-paid milestone index again is refused outright.
        Assert.Throws<System.InvalidOperationException>(() => PlanningEngine.PayMilestone(world, draft.Id, 2));
        Assert.Equal(cashBefore - 300_000, world.Ledger.Cash); // refused attempt changed nothing.
    }

    [Fact]
    public void CommitRelocation_InsufficientBudget_AppliesNothing_TransactionalRollback()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 100_000); // less than the draft's cost.
        var buildingBefore = world.BuildingStates[SimulationFixtures.ResidentialBuildingId];
        var hashBefore = world.ComputeStructuralHash();
        var draft = MakeRelocationDraft(world, cost: 200_000);

        var result = PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, new long[] { 200_000 });

        Assert.False(result.Success);
        Assert.Null(result.Project);
        Assert.NotEmpty(result.Failures);
        Assert.Contains(result.Failures, f => f.Contains("insufficient available budget"));

        // Nothing applied: building did not move, no reservation, no
        // project, no revision bump, world hash identical.
        Assert.Equal(buildingBefore.LocalX, world.BuildingStates[SimulationFixtures.ResidentialBuildingId].LocalX);
        Assert.Equal(buildingBefore.LocalZ, world.BuildingStates[SimulationFixtures.ResidentialBuildingId].LocalZ);
        Assert.False(world.Projects.ContainsKey(draft.Id));
        Assert.Equal(0, world.Ledger.Reserved);
        Assert.Equal(100_000, world.Ledger.Available);
        Assert.Equal(0, world.Revision);
        Assert.Equal(hashBefore, world.ComputeStructuralHash());
    }

    [Fact]
    public void CommitRelocation_GeometryConflict_DestinationNodeDoesNotExist_AppliesNothing_EvenWithSufficientBudget()
    {
        var world = SimulationFixtures.BuildWorldState();
        var impact = PlanningEngine.EstimateRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        var draft = new BuildingRelocationDraft("draft-bad-node", world.Revision, 100_000, impact,
            SimulationFixtures.ResidentialBuildingId, destLocalX: 999, destLocalZ: 999, destNearestRoadNodeId: 999999);

        var cashBefore = world.Ledger.Available;
        var result = PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, new long[] { 100_000 });

        Assert.False(result.Success);
        Assert.Contains(result.Failures, f => f.Contains("does not exist in the road graph"));
        // Budget check would have PASSED on its own (plenty available) --
        // proving the geometry check being the only failure still blocks
        // the whole transaction: nothing is applied piecemeal.
        Assert.Equal(cashBefore, world.Ledger.Available);
        Assert.False(world.Projects.ContainsKey(draft.Id));
    }

    [Fact]
    public void CommitRelocation_MilestonesNotSummingToFixedCost_IsRejected()
    {
        var world = SimulationFixtures.BuildWorldState();
        var draft = MakeRelocationDraft(world, cost: 200_000);
        var result = PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, new long[] { 100_000, 50_000 }); // sums to 150k, not 200k.

        Assert.False(result.Success);
        Assert.Contains(result.Failures, f => f.Contains("sum exactly"));
        Assert.Equal(0, world.Ledger.Reserved);
    }

    [Fact]
    public void CancelDraft_IsFree_NeverReservedAnythingToBeginWith()
    {
        var world = SimulationFixtures.BuildWorldState();
        var availableBefore = world.Ledger.Available;
        _ = MakeRelocationDraft(world); // only estimated, never committed.
        PlanningEngine.CancelDraft(); // no-op by construction -- see its doc comment.
        Assert.Equal(availableBefore, world.Ledger.Available);
    }

    [Fact]
    public void CancelProject_ReleasesRemainingReservationAndChargesTheCancellationFee()
    {
        var world = SimulationFixtures.BuildWorldState();
        var draft = MakeRelocationDraft(world, cost: 300_000);
        var commit = PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, new long[] { 100_000, 100_000, 100_000 });
        Assert.True(commit.Success);

        PlanningEngine.PayMilestone(world, draft.Id, 0); // pay 100k, 200k still reserved.
        var cashBefore = world.Ledger.Cash;

        PlanningEngine.CancelProject(world, draft.Id, cancellationFeeThb: 20_000);

        Assert.Equal(0, world.Ledger.ReservedOf(LedgerAccountKind.Capex)); // remaining 200k reservation released.
        Assert.Equal(cashBefore - 20_000, world.Ledger.Cash); // only the fee left cash, not the released reservation.
        Assert.Equal(ProjectStatus.Cancelled, world.Projects[draft.Id].Status);

        // Cancelling an already-cancelled project is idempotent.
        PlanningEngine.CancelProject(world, draft.Id, cancellationFeeThb: 20_000);
        Assert.Equal(cashBefore - 20_000, world.Ledger.Cash);
    }

    [Fact]
    public void Relocation_ConservesPopulationAndJobs_AndDoesNotMutateGeographyBase()
    {
        var world = SimulationFixtures.BuildWorldState();
        var originalGeographyBaseRef = world.GeographyBase;
        var buildingCountBefore = world.GeographyBase.Buildings.Count;
        var populationBefore = world.TotalPopulation;
        var jobsBefore = world.TotalJobs;

        var draft = MakeRelocationDraft(world);
        var result = PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, new long[] { 200_000 });
        Assert.True(result.Success, string.Join("; ", result.Failures));

        Assert.Equal(populationBefore, world.TotalPopulation); // cohort household count untouched by relocation.
        Assert.Equal(jobsBefore, world.TotalJobs);

        // GeographyBase: same reference (never reassigned), same building
        // count/content -- WorldState has no mutation path into it at all
        // (see ImmutabilityTests for the type-level proof; this is the
        // behavioural companion).
        Assert.Same(originalGeographyBaseRef, world.GeographyBase);
        Assert.Equal(buildingCountBefore, world.GeographyBase.Buildings.Count);
        var sourceBuilding = world.GeographyBase.Buildings.Single(b => b.SourceId == SimulationFixtures.ResidentialBuildingId);
        Assert.Equal(-1, sourceBuilding.RingGroupsCanonical[0].Outer[0].X); // original footprint corner untouched.

        // The building's SIMULATION state, by contrast, did move -- and a
        // vacated-lot record was kept (old-location treatment).
        var relocatedState = world.BuildingStates[SimulationFixtures.ResidentialBuildingId];
        Assert.True(relocatedState.Relocated);
        Assert.Equal(22, relocatedState.LocalX);
        Assert.Single(world.VacatedLots);
        Assert.Equal(0, world.VacatedLots[0].OldLocalX);
    }

    [Fact]
    public void Relocation_ImprovesAccessibility_WhenMovingIntoTheConnectedCluster()
    {
        var world = SimulationFixtures.BuildWorldState();
        var before = world.ComputeAccessibilityScore(SimulationFixtures.ResidentialBuildingId);

        var draft = MakeRelocationDraft(world);
        var result = PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, new long[] { 200_000 });
        Assert.True(result.Success);

        var after = world.ComputeAccessibilityScore(SimulationFixtures.ResidentialBuildingId);
        Assert.True(after > before, $"expected accessibility to improve: before={before}, after={after}");
    }
}
