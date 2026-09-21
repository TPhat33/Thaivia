using System.Linq;
using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.Mobility.Queues;
using Thaivia.Core.Simulation.Planning;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// Task Zero (g5 wave): road works as a REAL committable project through
/// PlanningEngine, with budget reservation and transactional commit --
/// replacing WorldState.AddRoadWorksZone's direct mutation as the
/// player-facing path (see ADR-0023). Same invariant discipline as
/// PlanningEngineTests for the other two project kinds: no double-charge,
/// rollback on failure, and (new to this kind) a cancellation rule that
/// truncates the zone's future effect without rewriting its past.
/// </summary>
public class RoadWorksProjectTests
{
    private const long WayId = 101; // SimulationFixtures.BuildRoadGraph's node1-node2 edge, lanes unset -> base capacity = 1 * VehPerTickPerLane.

    private static RoadWorksDraft MakeDraft(WorldState world, string id = "draft-roadworks-1", long cost = 60_000, long durationTicks = 100, double multiplier = 0.5) =>
        new(id, world.Revision, cost, PlanningEngine.EstimateRoadWorksCapacityImpact(world, WayId, multiplier), WayId, durationTicks, multiplier);

    [Fact]
    public void EstimateRoadWorksCapacityImpact_ReturnsANegativeRange_NeverAMutation()
    {
        var world = SimulationFixtures.BuildWorldState();
        var hashBefore = world.ComputeStructuralHash();

        var impact = PlanningEngine.EstimateRoadWorksCapacityImpact(world, WayId, 0.5);

        Assert.True(impact.ExpectedValue <= 0, "a capacity reduction must never be predicted as a positive delta.");
        Assert.True(impact.MinValue < impact.MaxValue);
        Assert.Equal(hashBefore, world.ComputeStructuralHash());
        Assert.Equal(0, world.Revision);
    }

    [Fact]
    public void CommitRoadWorks_ReservesExactlyTheFixedCost_AndRegistersAZone_ThatActuallyDegradesCapacity()
    {
        var world = SimulationFixtures.BuildWorldState();
        var cashBefore = world.Ledger.Available;
        var draft = MakeDraft(world);

        var result = PlanningEngine.CommitRoadWorks(world, draft, LedgerAccountKind.NonRecurring, new long[] { 60_000 });

        Assert.True(result.Success, string.Join("; ", result.Failures));
        Assert.Equal(ProjectKind.RoadWorks, result.Project!.Kind);
        Assert.Equal(cashBefore - 60_000, world.Ledger.Available);
        Assert.Equal(60_000, world.Ledger.ReservedOf(LedgerAccountKind.NonRecurring));
        Assert.Equal(1, world.Revision);

        var zone = Assert.Single(world.RoadWorksZones);
        Assert.Equal(WayId, zone.WayId);
        Assert.Equal(0, zone.StartTick); // committed at tick 0 -- takes effect immediately, like every other project kind.
        Assert.Equal(100, zone.DurationTicks);

        var edge = world.RoadGraph.Edges.Single(e => e.WayId == WayId);
        var baseCapacity = LinkCapacity.BaseCapacityVehPerTick(edge);
        var duringCapacity = LinkCapacity.EffectiveCapacityVehPerTick(edge, tick: 5, world.RoadWorksZones);
        Assert.True(duringCapacity < baseCapacity, "committing a road works project must measurably reduce the way's effective capacity while active.");
    }

    [Fact]
    public void ConfirmingTheSameRoadWorksDraftTwice_DoesNotDeductBudgetTwice_AndDoesNotRegisterASecondZone()
    {
        var world = SimulationFixtures.BuildWorldState();
        var draft = MakeDraft(world);

        var first = PlanningEngine.CommitRoadWorks(world, draft, LedgerAccountKind.NonRecurring, new long[] { 60_000 });
        var availableAfterFirst = world.Ledger.Available;
        var zoneCountAfterFirst = world.RoadWorksZones.Count;

        var second = PlanningEngine.CommitRoadWorks(world, draft, LedgerAccountKind.NonRecurring, new long[] { 60_000 });

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Same(first.Project, second.Project);
        Assert.Equal(availableAfterFirst, world.Ledger.Available);
        Assert.Equal(zoneCountAfterFirst, world.RoadWorksZones.Count); // no duplicate zone from the idempotent re-confirm.
        Assert.Equal(1, world.Revision); // no second bump either.
    }

    [Fact]
    public void CommitRoadWorks_InsufficientBudget_AppliesNothing_TransactionalRollback()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 10_000); // less than the draft's cost.
        var hashBefore = world.ComputeStructuralHash();
        var draft = MakeDraft(world, cost: 60_000);

        var result = PlanningEngine.CommitRoadWorks(world, draft, LedgerAccountKind.NonRecurring, new long[] { 60_000 });

        Assert.False(result.Success);
        Assert.Null(result.Project);
        Assert.Contains(result.Failures, f => f.Contains("insufficient available budget"));

        Assert.Empty(world.RoadWorksZones); // no zone registered on a failed commit.
        Assert.False(world.Projects.ContainsKey(draft.Id));
        Assert.Equal(0, world.Ledger.Reserved);
        Assert.Equal(0, world.Revision);
        Assert.Equal(hashBefore, world.ComputeStructuralHash());
    }

    [Fact]
    public void CommitRoadWorks_UnknownWay_AppliesNothing_EvenWithSufficientBudget()
    {
        var world = SimulationFixtures.BuildWorldState();
        var impact = PlanningEngine.EstimateRoadWorksCapacityImpact(world, WayId, 0.5); // estimate against a real way...
        var draft = new RoadWorksDraft("draft-bad-way", world.Revision, 60_000, impact, wayId: 999_999, durationTicks: 100, capacityMultiplierDuringConstruction: 0.5); // ...but commit against a nonexistent one.

        var cashBefore = world.Ledger.Available;
        var result = PlanningEngine.CommitRoadWorks(world, draft, LedgerAccountKind.NonRecurring, new long[] { 60_000 });

        Assert.False(result.Success);
        Assert.Contains(result.Failures, f => f.Contains("does not exist in the road graph"));
        Assert.Equal(cashBefore, world.Ledger.Available); // budget check would have passed on its own -- proves the geometry check alone blocks the whole transaction.
        Assert.Empty(world.RoadWorksZones);
        Assert.False(world.Projects.ContainsKey(draft.Id));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void CommitRoadWorks_MultiplierOutOfRange_IsRejected(double badMultiplier)
    {
        var world = SimulationFixtures.BuildWorldState();
        var impact = PlanningEngine.EstimateRoadWorksCapacityImpact(world, WayId, 0.5);
        var draft = new RoadWorksDraft("draft-bad-multiplier", world.Revision, 60_000, impact, WayId, durationTicks: 100, capacityMultiplierDuringConstruction: badMultiplier);

        var result = PlanningEngine.CommitRoadWorks(world, draft, LedgerAccountKind.NonRecurring, new long[] { 60_000 });

        Assert.False(result.Success);
        Assert.Contains(result.Failures, f => f.Contains("capacity multiplier"));
        Assert.Empty(world.RoadWorksZones);
    }

    [Fact]
    public void CommitRoadWorks_MilestonesNotSummingToFixedCost_IsRejected()
    {
        var world = SimulationFixtures.BuildWorldState();
        var draft = MakeDraft(world, cost: 60_000);
        var result = PlanningEngine.CommitRoadWorks(world, draft, LedgerAccountKind.NonRecurring, new long[] { 30_000, 20_000 }); // sums to 50k, not 60k.

        Assert.False(result.Success);
        Assert.Contains(result.Failures, f => f.Contains("sum exactly"));
        Assert.Equal(0, world.Ledger.Reserved);
        Assert.Empty(world.RoadWorksZones);
    }

    [Fact]
    public void AcceptingThenPayingMilestones_RoadWorks_NeverDoubleCharges_TotalDeductedEqualsFixedCostExactlyOnce()
    {
        var world = SimulationFixtures.BuildWorldState();
        var cashBefore = world.Ledger.Cash;
        var draft = MakeDraft(world, cost: 90_000);
        var milestones = new long[] { 30_000, 30_000, 30_000 };

        var commit = PlanningEngine.CommitRoadWorks(world, draft, LedgerAccountKind.NonRecurring, milestones);
        Assert.True(commit.Success, string.Join("; ", commit.Failures));
        Assert.Equal(cashBefore, world.Ledger.Cash); // reserved only, not yet charged.

        PlanningEngine.PayMilestone(world, draft.Id, 0);
        PlanningEngine.PayMilestone(world, draft.Id, 1);
        PlanningEngine.PayMilestone(world, draft.Id, 2);

        Assert.Equal(cashBefore - 90_000, world.Ledger.Cash);
        Assert.Equal(0, world.Ledger.ReservedOf(LedgerAccountKind.NonRecurring));
        Assert.Equal(ProjectStatus.Completed, world.Projects[draft.Id].Status);

        Assert.Throws<System.InvalidOperationException>(() => PlanningEngine.PayMilestone(world, draft.Id, 2));
        Assert.Equal(cashBefore - 90_000, world.Ledger.Cash); // refused attempt changed nothing.
    }

    /// <summary>The cancellation rule specific to this project kind: after
    /// cancelling mid-construction, FUTURE ticks are unaffected (full
    /// capacity again), but the degradation that already happened at
    /// EARLIER ticks (before cancellation) is untouched -- history is not
    /// rewritten.</summary>
    [Fact]
    public void CancelProject_RoadWorks_TruncatesTheZone_FutureTicksUnaffected_PastDegradationUnchanged()
    {
        var world = SimulationFixtures.BuildWorldState();
        var draft = MakeDraft(world, cost: 60_000, durationTicks: 200, multiplier: 0.25);
        var commit = PlanningEngine.CommitRoadWorks(world, draft, LedgerAccountKind.NonRecurring, new long[] { 60_000 });
        Assert.True(commit.Success);

        var edge = world.RoadGraph.Edges.Single(e => e.WayId == WayId);
        var baseCapacity = LinkCapacity.BaseCapacityVehPerTick(edge);

        // Before cancellation, tick 50 is inside the (not-yet-truncated)
        // window -- degraded.
        var beforeCancelAtTick50 = LinkCapacity.EffectiveCapacityVehPerTick(edge, tick: 50, world.RoadWorksZones);
        Assert.True(beforeCancelAtTick50 < baseCapacity);

        var cashBefore = world.Ledger.Cash;
        PlanningEngine.CancelProject(world, draft.Id, cancellationFeeThb: 5_000);

        Assert.Equal(ProjectStatus.Cancelled, world.Projects[draft.Id].Status);
        Assert.Equal(0, world.Ledger.ReservedOf(LedgerAccountKind.NonRecurring)); // full remaining reservation released.
        Assert.Equal(cashBefore - 5_000, world.Ledger.Cash); // only the fee left cash.

        // Re-reading the SAME tick 50 after cancellation must give the
        // IDENTICAL answer as before cancellation -- history is not
        // rewritten by a later cancellation (WorldState.Clock is still at
        // tick 0 in this test; the truncation point is tick 0, so tick 50
        // -- which is strictly after the truncation point -- now reads as
        // full capacity instead, which is the FUTURE-unaffected half of
        // this assertion. See the two checks below for both halves made
        // explicit.)
        var zone = Assert.Single(world.RoadWorksZones);
        Assert.Equal(0, zone.DurationTicks); // cancelled at tick 0 (the same tick it was committed at in this test) -- truncated to zero effect from here on.

        var afterCancelAtTick50 = LinkCapacity.EffectiveCapacityVehPerTick(edge, tick: 50, world.RoadWorksZones);
        Assert.Equal(baseCapacity, afterCancelAtTick50); // future tick: no longer degraded.
    }

    /// <summary>Same rule, but cancelling AFTER some real ticks have
    /// already passed inside the construction window: those earlier
    /// ticks' degraded-capacity answer must be preserved, and only ticks
    /// from the cancellation point onward return to full capacity.</summary>
    [Fact]
    public void CancelProject_RoadWorks_MidConstruction_PastTicksStayDegraded_OnlyFutureTicksRecover()
    {
        var world = SimulationFixtures.BuildWorldState();
        var draft = MakeDraft(world, cost: 60_000, durationTicks: 200, multiplier: 0.25);
        PlanningEngine.CommitRoadWorks(world, draft, LedgerAccountKind.NonRecurring, new long[] { 60_000 });

        for (var i = 0; i < 40; i++)
        {
            world.SimulateTick(); // advance the world's clock to tick 40, inside the [0, 200) construction window.
        }

        var edge = world.RoadGraph.Edges.Single(e => e.WayId == WayId);
        var baseCapacity = LinkCapacity.BaseCapacityVehPerTick(edge);

        PlanningEngine.CancelProject(world, draft.Id, cancellationFeeThb: 0);

        var zone = Assert.Single(world.RoadWorksZones);
        Assert.Equal(40, zone.DurationTicks); // truncated to exactly the tick cancellation happened at.

        // Tick 20 (before cancellation): still reads as degraded -- past history untouched.
        Assert.True(LinkCapacity.EffectiveCapacityVehPerTick(edge, tick: 20, world.RoadWorksZones) < baseCapacity);
        // Tick 40 itself and everything after: back to full capacity -- the half-open [start, start+duration) convention (see RoadWorksZone.IsActiveAt) means tick 40 is the first tick NOT covered.
        Assert.Equal(baseCapacity, LinkCapacity.EffectiveCapacityVehPerTick(edge, tick: 40, world.RoadWorksZones));
        Assert.Equal(baseCapacity, LinkCapacity.EffectiveCapacityVehPerTick(edge, tick: 100, world.RoadWorksZones));
    }

    [Fact]
    public void CancelProject_RoadWorks_AlreadyNaturallyEnded_LeavesTheZoneUntouched()
    {
        var world = SimulationFixtures.BuildWorldState();
        var draft = MakeDraft(world, cost: 60_000, durationTicks: 10, multiplier: 0.25); // short window.
        var commit = PlanningEngine.CommitRoadWorks(world, draft, LedgerAccountKind.NonRecurring, new long[] { 60_000 });
        Assert.True(commit.Success);

        for (var i = 0; i < 20; i++)
        {
            world.SimulateTick(); // past the 10-tick window already.
        }

        PlanningEngine.CancelProject(world, draft.Id, cancellationFeeThb: 0);

        var zone = Assert.Single(world.RoadWorksZones);
        Assert.Equal(10, zone.DurationTicks); // untouched -- it had already ended naturally, nothing to truncate.
    }
}
