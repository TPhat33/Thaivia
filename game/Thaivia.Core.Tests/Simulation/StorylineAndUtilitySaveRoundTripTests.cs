using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.Planning;
using Thaivia.Core.Simulation.Save;
using Thaivia.Core.Simulation.Storyline;
using Thaivia.Core.Simulation.Utilities;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// Task 1 (wave 9, ADR-0039): closes the save-completeness gap the
/// previous wave stated plainly (ADR-0033/0035/0036 doc comments):
/// <see cref="UtilitySource"/>, <see cref="InvestorProposal"/> and
/// <see cref="CorruptionCase"/> were in-memory only and NOT in
/// <see cref="SaveGame"/>. Same "prove it field by field, not just via
/// one hash" discipline <c>MobilitySaveRoundTripTests</c> already
/// established for G4.
/// </summary>
public class StorylineAndUtilitySaveRoundTripTests
{
    private const string MapId = "test-map";
    private const string MapContentHash = "sha256:deadbeef";

    /// <summary>Builds a deliberately non-trivial mid-state: a utility
    /// source placed, an investor proposal ACCEPTED (funding credited)
    /// whose required project has since been committed (real money
    /// RESERVED against it, not yet paid), and a corruption case that has
    /// moved past Open into UnderInvestigation -- none of the three
    /// subsystems left at their trivial/starting state.</summary>
    private static void PopulateMinorSystemsState(WorldState world)
    {
        // Utility source: a water source anchored at a real road node.
        world.AddUtilitySource(new UtilitySource(UtilityKind.Water, SimulationFixtures.Node2, capacityUnitsPerTick: 7));

        // Investor proposal: accepted (credits the ledger), condition
        // requires a NewRoadConnector project within the window -- left
        // Accepted (not evaluated/fulfilled), so the nullable
        // AcceptedAtTick/ConditionDeadlineTick/BaselineRequiredKindCount
        // fields are all populated and must round-trip too.
        world.AddInvestorProposal(new InvestorProposal(
            "invest-save-1", fundingAmountThb: 300_000, LedgerAccountKind.NonRecurring,
            ProjectKind.NewRoadConnector, conditionWindowTicks: 100, offerExpiryTick: 5));
        var acceptResult = InvestorProposalEngine.Accept(world, "invest-save-1", currentTick: 0);
        Assert.True(acceptResult.Success, string.Join("; ", acceptResult.Failures));

        // Required project committed -- reserves real money, then ONE of
        // its two milestones is paid, so a "mid-state" project sits with
        // BOTH money already paid (real ledger history for the
        // corruption case below to be capped against) AND money still
        // reserved (proving the reservation itself, not just the paid
        // amount, survives the round trip).
        var impact = PlanningEngine.EstimateNewRoadAccessImpact(world, SimulationFixtures.Node2, SimulationFixtures.Node3, SimulationFixtures.ResidentialBuildingId);
        var roadDraft = new NewRoadConnectorDraft("save-road-1", world.Revision, 120_000, impact, SimulationFixtures.Node2, SimulationFixtures.Node3);
        var commit = PlanningEngine.CommitNewRoadConnector(world, roadDraft, LedgerAccountKind.Capex, new long[] { 60_000, 60_000 });
        Assert.True(commit.Success, string.Join("; ", commit.Failures));
        PlanningEngine.PayMilestone(world, "save-road-1", 0);
        Assert.Equal(60_000, world.Ledger.ReservedOf(LedgerAccountKind.Capex)); // second milestone still reserved, not paid.
        Assert.Equal(60_000, world.Projects["save-road-1"].TotalPaid);

        // Corruption case tied to that real committed project, moved to
        // UnderInvestigation -- not left at the trivial Open state.
        var openResult = CorruptionCaseEngine.OpenCase(world, "case-save-1", "save-road-1", "Contractor Alpha", allegedOverpaymentThb: 40_000);
        Assert.True(openResult.Success, string.Join("; ", openResult.Failures));
        var investigateResult = CorruptionCaseEngine.BeginInvestigation(world, "case-save-1");
        Assert.True(investigateResult.Success, string.Join("; ", investigateResult.Failures));
    }

    private static WorldState BuildWorldState(long masterSeed) =>
        SimulationFixtures.BuildWorldState(masterSeed: masterSeed, initialCashThb: 5_000_000);

    [Fact]
    public void AllThreeMinorSystems_SurviveASaveRestoreRoundTrip_FieldByField()
    {
        var world = BuildWorldState(masterSeed: 4242);
        PopulateMinorSystemsState(world);

        var save = world.CaptureSave(MapId, MapContentHash, "0.1.0", "0.1.0");
        var json = SaveSerializer.Serialize(save);
        var roundTripped = SaveSerializer.Deserialize(json);
        var restored = WorldState.Restore(roundTripped, SimulationFixtures.BuildGeographyBase(), SimulationFixtures.BuildSimulationInitialization(), SimulationFixtures.BuildRoadGraph(),
            new Thaivia.Core.Simulation.Scenario.ScenarioConfig(4242, 5_000_000));

        // --- Utility source ---
        Assert.Single(restored.UtilitySources);
        var restoredUtility = restored.UtilitySources[0];
        Assert.Equal(UtilityKind.Water, restoredUtility.Kind);
        Assert.Equal(SimulationFixtures.Node2, restoredUtility.NodeId);
        Assert.Equal(7, restoredUtility.CapacityUnitsPerTick);

        // --- Investor proposal (mid-lifecycle: Accepted, not Fulfilled) ---
        var originalProposal = world.InvestorProposals["invest-save-1"];
        var restoredProposal = restored.InvestorProposals["invest-save-1"];
        Assert.Equal(InvestorProposalStatus.Accepted, restoredProposal.Status);
        Assert.Equal(originalProposal.FundingAmountThb, restoredProposal.FundingAmountThb);
        Assert.Equal(originalProposal.LedgerKind, restoredProposal.LedgerKind);
        Assert.Equal(originalProposal.RequiredProjectKind, restoredProposal.RequiredProjectKind);
        Assert.Equal(originalProposal.ConditionWindowTicks, restoredProposal.ConditionWindowTicks);
        Assert.Equal(originalProposal.OfferExpiryTick, restoredProposal.OfferExpiryTick);
        Assert.Equal(originalProposal.AcceptedAtTick, restoredProposal.AcceptedAtTick);
        Assert.NotNull(restoredProposal.AcceptedAtTick); // sanity: really was accepted, not a trivial default.
        Assert.Equal(originalProposal.ConditionDeadlineTick, restoredProposal.ConditionDeadlineTick);
        Assert.NotNull(restoredProposal.ConditionDeadlineTick);
        Assert.Equal(originalProposal.BaselineRequiredKindCount, restoredProposal.BaselineRequiredKindCount);
        Assert.NotNull(restoredProposal.BaselineRequiredKindCount);
        Assert.Equal(originalProposal.AmountClawedBack, restoredProposal.AmountClawedBack);

        // Ledger: the proposal's funding credit and the road project's
        // reservation both survived too (not re-derived, read verbatim).
        Assert.Equal(world.Ledger.Cash, restored.Ledger.Cash);
        Assert.Equal(world.Ledger.ReservedOf(LedgerAccountKind.Capex), restored.Ledger.ReservedOf(LedgerAccountKind.Capex));
        Assert.Equal(60_000, restored.Ledger.ReservedOf(LedgerAccountKind.Capex));

        // --- Corruption case (mid-investigation, not Resolved/Dismissed) ---
        var originalCase = world.CorruptionCases["case-save-1"];
        var restoredCase = restored.CorruptionCases["case-save-1"];
        Assert.Equal(CorruptionCaseStatus.UnderInvestigation, restoredCase.Status);
        Assert.Equal(originalCase.RelatedCommittedProjectId, restoredCase.RelatedCommittedProjectId);
        Assert.Equal(originalCase.ContractorLabel, restoredCase.ContractorLabel);
        Assert.Equal(originalCase.AllegedOverpaymentThb, restoredCase.AllegedOverpaymentThb);
        Assert.Equal(originalCase.RecoveredAmountThb, restoredCase.RecoveredAmountThb);
        Assert.Equal(0, restoredCase.RecoveredAmountThb); // sanity: not yet resolved.

        // --- Whole-world hash: nothing about this state is invisible to
        // the determinism/save-completeness contract either.
        Assert.Equal(world.ComputeStructuralHash(), restored.ComputeStructuralHash());
    }

    [Fact]
    public void AllThreeMinorSystems_ContributeToComputeStructuralHash_SoLosingThemWouldBeDetected()
    {
        // Two worlds, identical seed/commands, EXCEPT one also registers
        // the minor-systems state -- if that state were NOT part of
        // ComputeStructuralHash, the two hashes would collide despite
        // materially different world state (exactly the risk ADR-0021
        // already called out for G4 mobility state).
        var withState = BuildWorldState(masterSeed: 5150);
        PopulateMinorSystemsState(withState);

        var withoutState = BuildWorldState(masterSeed: 5150);

        Assert.NotEqual(withState.ComputeStructuralHash(), withoutState.ComputeStructuralHash());
    }

    [Fact]
    public void SaveThenRestore_ThenContinue_WithMinorSystemsMidState_ProducesTheIdenticalHashAsNeverHavingSaved()
    {
        // Same shape as SaveLoadTests.SaveThenRestore_ThenContinue_..., but
        // with the Task 1 mid-state folded into phase one -- proves
        // save -> load -> continue N ticks is equivalent to never having
        // saved even with utility/investor/corruption state in play.
        var control = BuildWorldState(masterSeed: 6161);
        PopulateMinorSystemsState(control);
        RunPhaseTwo(control);
        var controlHash = control.ComputeStructuralHash();

        var beforeSave = BuildWorldState(masterSeed: 6161);
        PopulateMinorSystemsState(beforeSave);
        var save = beforeSave.CaptureSave(MapId, MapContentHash, "0.1.0", "0.1.0");

        var restored = WorldState.Restore(save, SimulationFixtures.BuildGeographyBase(), SimulationFixtures.BuildSimulationInitialization(), SimulationFixtures.BuildRoadGraph(),
            new Thaivia.Core.Simulation.Scenario.ScenarioConfig(6161, 5_000_000));
        RunPhaseTwo(restored);
        var restoredHash = restored.ComputeStructuralHash();

        Assert.Equal(controlHash, restoredHash);
    }

    private static void RunPhaseTwo(WorldState world)
    {
        for (var i = 0; i < 10; i++)
        {
            world.SimulateTick();
        }

        PlanningEngine.PayMilestone(world, "save-road-1", 1);
    }
}
