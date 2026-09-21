using System;
using System.Linq;
using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.Planning;
using Thaivia.Core.Simulation.Progression;
using Xunit;
using Xunit.Abstractions;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// Tutorial/scenario progression as data + logic, testable headlessly
/// (spec §19, g5 supervisor brief) -- no UI, no Unity. Builds a small
/// three-step tutorial ("commit any project" -> "get the relocated
/// building's accessibility to at least 90" -> "keep at least 100,000 THB
/// in reserve") over SimulationFixtures and drives it through a real
/// PlanningEngine commit, proving the sequencing/locking rules and that
/// evaluation never mutates the world.
/// </summary>
public class ScenarioProgressionTests
{
    private readonly ITestOutputHelper _output;

    public ScenarioProgressionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static ScenarioProgression BuildThreeStepTutorial() => new(new ObjectiveDefinition[]
    {
        new("commit-a-project", "Commit any project", new ProjectCommittedCheck()),
        new("reach-high-access", "Reach at least 90 accessibility for the relocated building",
            new AccessibilityAtLeastCheck(SimulationFixtures.ResidentialBuildingId, minimumScore: 90)),
        new("keep-reserve", "Keep at least 100,000 THB in reserve", new BudgetAtLeastCheck(100_000)),
    });

    [Fact]
    public void AllObjectives_StartAsInProgressOrLocked_NeverCompleted_OnAFreshWorld()
    {
        var world = SimulationFixtures.BuildWorldState();
        var progression = BuildThreeStepTutorial();

        var evaluated = progression.Evaluate(world);

        Assert.Equal(ObjectiveStatus.InProgress, evaluated[0].Status); // the first objective is always at least evaluated.
        Assert.Equal(ObjectiveStatus.Locked, evaluated[1].Status); // nothing after an incomplete objective is evaluated.
        Assert.Equal(ObjectiveStatus.Locked, evaluated[2].Status);
        Assert.False(progression.IsScenarioComplete(world));
    }

    [Fact]
    public void Evaluate_NeverEvaluatesALockedObjectivesCheck_SpyControlTest()
    {
        var world = SimulationFixtures.BuildWorldState();
        var throwingCheck = new ThrowingSpyCheck();
        var progression = new ScenarioProgression(new ObjectiveDefinition[]
        {
            new("never-satisfied", "This objective is never satisfied", new AlwaysFalseCheck()),
            new("would-throw-if-evaluated", "If this ever runs, the sequencing rule is broken", throwingCheck),
        });

        var evaluated = progression.Evaluate(world);

        Assert.Equal(ObjectiveStatus.InProgress, evaluated[0].Status);
        Assert.Equal(ObjectiveStatus.Locked, evaluated[1].Status);
        Assert.False(throwingCheck.WasCalled, "a Locked objective's check must never be evaluated -- if it was, the throwing spy would have thrown.");
    }

    [Fact]
    public void Objectives_UnlockInOrder_AsRealCommandsSatisfyThem_EndingScenarioComplete()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 5_000_000);
        var progression = BuildThreeStepTutorial();

        // Step 0: nothing committed yet.
        var step0 = progression.Evaluate(world);
        Assert.Equal(ObjectiveStatus.InProgress, step0[0].Status);
        Assert.Equal(ObjectiveStatus.Locked, step0[1].Status);
        Assert.Equal(ObjectiveStatus.Locked, step0[2].Status);

        // Commit a real relocation that pushes accessibility to 98 (see
        // PlanningEngineTests/AccessibilityGraphTests for the same
        // fixture's numbers) -- this is a REAL PlanningEngine commit, not
        // a fabricated world-state edit.
        var draft = new BuildingRelocationDraft("prog-reloc", world.Revision, 200_000,
            PlanningEngine.EstimateRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4),
            SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        var commit = PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, new long[] { 200_000 });
        Assert.True(commit.Success, string.Join("; ", commit.Failures));

        // Step 1: objective 0 (commit a project) is now Completed, and
        // objective 1 (accessibility >= 90) is ALSO already satisfied by
        // this same commit -- so it should be Completed too, unlocking
        // objective 2.
        var step1 = progression.Evaluate(world);
        _output.WriteLine($"After relocation: {string.Join(", ", step1.Select(e => $"{e.Objective.Id}={e.Status}"))} (accessibility now {world.ComputeAccessibilityScore(SimulationFixtures.ResidentialBuildingId)}).");
        Assert.Equal(ObjectiveStatus.Completed, step1[0].Status);
        Assert.Equal(ObjectiveStatus.Completed, step1[1].Status);
        Assert.Equal(ObjectiveStatus.Completed, step1[2].Status); // 5,000,000 - 200,000 = 4,800,000 available -- already >= 100,000, so...

        // ...this scenario is ALREADY complete after one commit, given
        // the generous starting budget. Confirms IsScenarioComplete
        // reflects Evaluate exactly.
        Assert.True(progression.IsScenarioComplete(world));
        Assert.Equal(ObjectiveStatus.Completed, progression.Evaluate(world)[2].Status);
    }

    /// <summary>The same three-step tutorial, but starting with just
    /// enough cash that after paying for the relocation, the reserve
    /// objective is NOT yet satisfied -- proves objective 2 can stay
    /// InProgress even once objective 1 unlocks it, i.e. unlocking is not
    /// the same as completing.</summary>
    [Fact]
    public void UnlockingAnObjective_IsNotTheSameAsCompletingIt()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 250_000); // 250,000 - 200,000 = 50,000 left -- below the 100,000 reserve objective.
        var progression = BuildThreeStepTutorial();

        var draft = new BuildingRelocationDraft("prog-reloc-2", world.Revision, 200_000,
            PlanningEngine.EstimateRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4),
            SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        var commit = PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, new long[] { 200_000 });
        Assert.True(commit.Success, string.Join("; ", commit.Failures));

        var evaluated = progression.Evaluate(world);
        Assert.Equal(ObjectiveStatus.Completed, evaluated[0].Status);
        Assert.Equal(ObjectiveStatus.Completed, evaluated[1].Status);
        Assert.Equal(ObjectiveStatus.InProgress, evaluated[2].Status); // unlocked (evaluated at all) but not satisfied.
        Assert.False(progression.IsScenarioComplete(world));
        _output.WriteLine($"Budget after relocation: {world.Ledger.Available} (reserve objective needs 100,000) -- reserve objective correctly still InProgress, not Completed.");
    }

    [Fact]
    public void Evaluate_DoesNotMutateTheWorld()
    {
        var world = SimulationFixtures.BuildWorldState();
        var progression = BuildThreeStepTutorial();
        var hashBefore = world.ComputeStructuralHash();

        _ = progression.Evaluate(world);
        _ = progression.IsScenarioComplete(world);

        Assert.Equal(hashBefore, world.ComputeStructuralHash());
    }

    [Fact]
    public void AllOfCheck_RequiresEveryComponentCheck_NotJustOne()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 5_000_000);
        var combined = new AllOfCheck(new ProjectCommittedCheck(), new BudgetAtLeastCheck(4_000_000));

        Assert.False(combined.IsSatisfied(world)); // no project committed yet -- fails even though budget alone would pass.

        var draft = new BuildingRelocationDraft("prog-reloc-3", world.Revision, 200_000,
            PlanningEngine.EstimateRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4),
            SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, new long[] { 200_000 });

        Assert.True(combined.IsSatisfied(world)); // both now hold: a project was committed AND >= 4,000,000 remains.
    }

    private sealed class AlwaysFalseCheck : IObjectiveCheck
    {
        public bool IsSatisfied(WorldState world) => false;
    }

    private sealed class ThrowingSpyCheck : IObjectiveCheck
    {
        public bool WasCalled { get; private set; }

        public bool IsSatisfied(WorldState world)
        {
            WasCalled = true;
            throw new InvalidOperationException("This check must never be called while its objective is Locked.");
        }
    }
}
