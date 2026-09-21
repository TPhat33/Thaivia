using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.Planning;
using Thaivia.Core.Simulation.Reporting;
using Xunit;
using Xunit.Abstractions;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// Before/after as a first-class, always-available feature (g5 supervisor
/// brief): the base map and baseline metrics must remain queryable after
/// ANY number of player projects. Proves GeographyBase is untouched and
/// the baseline snapshot reproduces -- not just once, right after session
/// start, but at any later point, no matter how much has been committed
/// to the "current" world in the meantime.
/// </summary>
public class BeforeAfterComparisonTests
{
    private readonly ITestOutputHelper _output;

    public BeforeAfterComparisonTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void RebuildBaseline_IsAvailableAtAnyPoint_EvenAfterManyProjectsAndTicksHaveRun_AndReproducesExactly()
    {
        var world = SimulationFixtures.BuildWorldState(masterSeed: 314);
        var cohortId = "cohort-" + SimulationFixtures.ResidentialBuildingId;

        // Capture what "before" looks like right at the start, from a
        // baseline rebuilt at this exact moment -- this is the ground
        // truth every later rebuild must keep matching.
        var earlyBaseline = BeforeAfterComparison.RebuildBaseline(world);
        var earlyBaselineNeeds = earlyBaseline.ComputeCohortNeeds(cohortId, hourOfDay: 12);
        var earlyBaselineHash = earlyBaseline.ComputeStructuralHash();

        // Now commit several real projects AND run real ticks -- the kind
        // of session history that, in a system without a real
        // before/after feature, would make "what was it like before"
        // unanswerable without having manually saved a copy up front.
        for (var i = 0; i < 20; i++)
        {
            world.SimulateTick();
        }

        var relocationDraft = new BuildingRelocationDraft("cmp-reloc", world.Revision, 200_000,
            PlanningEngine.EstimateRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4),
            SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        var commit = PlanningEngine.CommitRelocation(world, relocationDraft, LedgerAccountKind.Capex, new long[] { 200_000 });
        Assert.True(commit.Success, string.Join("; ", commit.Failures));

        for (var i = 0; i < 20; i++)
        {
            world.SimulateTick();
        }

        // Rebuild the baseline AGAIN, now, after all of that -- it must
        // reproduce byte-for-bit identically to the one captured before
        // any of it happened.
        var lateBaseline = BeforeAfterComparison.RebuildBaseline(world);
        var lateBaselineNeeds = lateBaseline.ComputeCohortNeeds(cohortId, hourOfDay: 12);

        Assert.Equal(earlyBaselineHash, lateBaseline.ComputeStructuralHash());
        Assert.Equal(earlyBaselineNeeds, lateBaselineNeeds);

        // The base map itself: literally the same object, never copied or
        // mutated, throughout everything above.
        Assert.Same(world.GeographyBase, earlyBaseline.GeographyBase);
        Assert.Same(world.GeographyBase, lateBaseline.GeographyBase);
        Assert.Equal(earlyBaseline.GeographyBase.Buildings.Count, lateBaseline.GeographyBase.Buildings.Count);

        _output.WriteLine($"Baseline Access for the relocated cohort, rebuilt before AND after the relocation+ticks: {earlyBaselineNeeds.Access} == {lateBaselineNeeds.Access} (identical).");
    }

    [Fact]
    public void CompareAccessibilityScore_ShowsARealMeasuredDelta_AcrossARelocation()
    {
        var world = SimulationFixtures.BuildWorldState();
        var baseline = BeforeAfterComparison.RebuildBaseline(world);

        var draft = new BuildingRelocationDraft("cmp-reloc-2", world.Revision, 200_000,
            PlanningEngine.EstimateRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4),
            SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        var commit = PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, new long[] { 200_000 });
        Assert.True(commit.Success);

        var delta = BeforeAfterComparison.CompareAccessibilityScore(world, baseline, SimulationFixtures.ResidentialBuildingId, out var before, out var after);
        _output.WriteLine($"Accessibility score for the relocated building: before={before}, after={after}, delta={delta}.");

        Assert.Equal(0, before); // baseline: the two clusters are disconnected (see SimulationFixtures' doc comment).
        Assert.True(after > 0); // current: the relocation moved the building into the connected cluster.
        Assert.Equal(after - before, delta);
    }

    /// <summary>Control: a baseline rebuilt from a world that never had
    /// anything committed to it must equal that same world's own live
    /// metrics exactly -- otherwise RebuildBaseline would not actually be
    /// reconstructing the SAME starting point, just something that looks
    /// similar.</summary>
    [Fact]
    public void RebuildBaseline_OnAWorldWithNoProjectsCommittedYet_MatchesTheLiveWorldExactly()
    {
        var world = SimulationFixtures.BuildWorldState(masterSeed: 55);
        var baseline = BeforeAfterComparison.RebuildBaseline(world);

        Assert.Equal(world.ComputeStructuralHash(), baseline.ComputeStructuralHash());
    }
}
