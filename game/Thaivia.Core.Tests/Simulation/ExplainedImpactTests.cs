using System.Linq;
using Thaivia.Core.Simulation.Planning;
using Xunit;
using Xunit.Abstractions;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// Explainable predictions (spec §12, g5 supervisor brief): a committed
/// project's predicted impact must carry its uncertainty AND the
/// reasoning inputs that produced it -- not a bare confident number.
/// Fixed cost stays a separate, definite field. These tests prove
/// PlanningEngine's Explain* methods actually satisfy that, and that they
/// are non-mutating like every other Estimate*-style method in this
/// class.
/// </summary>
public class ExplainedImpactTests
{
    private readonly ITestOutputHelper _output;

    public ExplainedImpactTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ExplainRelocationAccessImpact_CarriesUncertainty_AndRealReasoningInputs()
    {
        var world = SimulationFixtures.BuildWorldState();
        var explained = PlanningEngine.ExplainRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);

        // Uncertainty: same band discipline as the plain Estimate method.
        Assert.True(explained.MinValue < explained.ExpectedValue);
        Assert.True(explained.ExpectedValue < explained.MaxValue);

        // Reasoning: not empty, and specifically contains the inputs that
        // actually determine the prediction -- current score, projected
        // distance, the reference distance constant, projected score.
        Assert.NotEmpty(explained.ReasoningInputs);
        var names = explained.ReasoningInputs.Select(r => r.Name).ToList();
        Assert.Contains("current_accessibility_score", names);
        Assert.Contains("projected_network_distance_to_nearest_job", names);
        Assert.Contains("accessibility_reference_distance", names);
        Assert.Contains("projected_accessibility_score", names);

        foreach (var input in explained.ReasoningInputs)
        {
            _output.WriteLine($"{input.Name} = {input.Value} {input.Unit}");
        }

        // Consistency: the reasoning inputs must actually be consistent
        // with the range they explain, not just present alongside it --
        // projected score minus current score must equal the range's
        // expected value (both come from the exact same computation).
        var currentScore = explained.ReasoningInputs.Single(r => r.Name == "current_accessibility_score").Value;
        var projectedScore = explained.ReasoningInputs.Single(r => r.Name == "projected_accessibility_score").Value;
        Assert.Equal(projectedScore - currentScore, explained.ExpectedValue, precision: 6);
    }

    [Fact]
    public void ExplainRelocationAccessImpact_MatchesThePlainEstimate_SameNumberDifferentShape()
    {
        var world = SimulationFixtures.BuildWorldState();
        var plain = PlanningEngine.EstimateRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        var explained = PlanningEngine.ExplainRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);

        Assert.Equal(plain, explained.Impact);
    }

    [Fact]
    public void ExplainNewRoadAccessImpact_CarriesTheNewSegmentLength_AsAReasoningInput()
    {
        var world = SimulationFixtures.BuildWorldState();
        var explained = PlanningEngine.ExplainNewRoadAccessImpact(world, SimulationFixtures.Node2, SimulationFixtures.Node3, SimulationFixtures.ResidentialBuildingId);

        var lengthInput = explained.ReasoningInputs.Single(r => r.Name == "new_segment_length");
        Assert.True(lengthInput.Value > 0);
        Assert.Equal("m", lengthInput.Unit);
    }

    [Fact]
    public void ExplainRoadWorksCapacityImpact_CarriesBaseAndReducedCapacityAsReasoningInputs()
    {
        var world = SimulationFixtures.BuildWorldState();
        var explained = PlanningEngine.ExplainRoadWorksCapacityImpact(world, wayId: 101, capacityMultiplierDuringConstruction: 0.5);

        var baseCap = explained.ReasoningInputs.Single(r => r.Name == "base_capacity").Value;
        var reducedCap = explained.ReasoningInputs.Single(r => r.Name == "reduced_capacity").Value;

        Assert.True(reducedCap < baseCap);
        Assert.Equal(reducedCap - baseCap, explained.ExpectedValue, precision: 6);
    }

    /// <summary>Explain* is pure, like every Estimate* method -- world hash
    /// and every RNG stream's state/draw count unchanged.</summary>
    [Fact]
    public void ExplainMethods_DoNotMutateTheWorld()
    {
        var world = SimulationFixtures.BuildWorldState();
        var hashBefore = world.ComputeStructuralHash();
        var statesBefore = world.RandomStreams.CaptureStates();
        var drawsBefore = world.RandomStreams.CaptureDrawCounts();

        _ = PlanningEngine.ExplainRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        _ = PlanningEngine.ExplainNewRoadAccessImpact(world, SimulationFixtures.Node2, SimulationFixtures.Node3, SimulationFixtures.ResidentialBuildingId);
        _ = PlanningEngine.ExplainRoadWorksCapacityImpact(world, 101, 0.5);

        Assert.Equal(hashBefore, world.ComputeStructuralHash());
        Assert.Equal(statesBefore, world.RandomStreams.CaptureStates());
        Assert.Equal(drawsBefore, world.RandomStreams.CaptureDrawCounts());
        Assert.Equal(0, world.Revision);
    }

    /// <summary>Fixed cost and predicted impact are structurally different
    /// shapes on ProjectDraft -- reaffirms the existing discipline in this
    /// new explainability context (ImpactRange's own doc comment already
    /// states this; this is the behavioural companion).</summary>
    [Fact]
    public void FixedCostAndPredictedImpact_AreDifferentFieldsOnTheDraft_NeverConflated()
    {
        var world = SimulationFixtures.BuildWorldState();
        var explained = PlanningEngine.ExplainRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        var draft = new BuildingRelocationDraft("draft-explain-1", world.Revision, fixedCostThb: 200_000, explained.Impact,
            SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);

        Assert.Equal(200_000, draft.FixedCostThb); // a definite long.
        Assert.NotEqual((double)draft.FixedCostThb, draft.PredictedImpact.ExpectedValue); // never the same number by construction of this scenario.
    }
}
