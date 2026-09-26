using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.Planning;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// G6-08: investor-proposal subsystem, driven with the same transactional
/// discipline PlanningEngineTests already proves -- accept, decline,
/// expiry, and a condition-violated-so-funding-is-withdrawn path, all
/// with real ledger numbers.
/// </summary>
public class InvestorProposalTests
{
    private static InvestorProposal MakeProposal(
        string id = "invest-1",
        long fundingAmountThb = 100_000,
        long conditionWindowTicks = 50,
        long offerExpiryTick = 10,
        ProjectKind requiredKind = ProjectKind.NewRoadConnector) =>
        new(id, fundingAmountThb, LedgerAccountKind.NonRecurring, requiredKind, conditionWindowTicks, offerExpiryTick);

    [Fact]
    public void Accept_CreditsTheLedgerExactlyOnce_WithRealNumbers()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 1_000_000);
        var before = world.Ledger.Cash;
        world.AddInvestorProposal(MakeProposal(fundingAmountThb: 250_000));

        var result = InvestorProposalEngine.Accept(world, "invest-1", currentTick: 0);

        Assert.True(result.Success, string.Join("; ", result.Failures));
        Assert.Equal(before + 250_000, world.Ledger.Cash);
        Assert.Equal(InvestorProposalStatus.Accepted, world.InvestorProposals["invest-1"].Status);
    }

    [Fact]
    public void Accept_Twice_IsRejected_NeverDoubleCredits()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 1_000_000);
        world.AddInvestorProposal(MakeProposal(fundingAmountThb: 250_000));

        var first = InvestorProposalEngine.Accept(world, "invest-1", currentTick: 0);
        Assert.True(first.Success);
        var cashAfterFirst = world.Ledger.Cash;

        var second = InvestorProposalEngine.Accept(world, "invest-1", currentTick: 1);
        Assert.False(second.Success);
        Assert.Equal(cashAfterFirst, world.Ledger.Cash); // unchanged -- no double credit.
    }

    [Fact]
    public void AcceptAfterOfferExpiry_IsRejected_NoLedgerEffect()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 1_000_000);
        var before = world.Ledger.Cash;
        world.AddInvestorProposal(MakeProposal(offerExpiryTick: 5));

        var result = InvestorProposalEngine.Accept(world, "invest-1", currentTick: 6);

        Assert.False(result.Success);
        Assert.Equal(before, world.Ledger.Cash);
    }

    [Fact]
    public void Decline_HasNoLedgerEffectAtAll()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 1_000_000);
        var before = world.Ledger.Cash;
        var beforeReserved = world.Ledger.Reserved;
        world.AddInvestorProposal(MakeProposal(fundingAmountThb: 500_000));

        var result = InvestorProposalEngine.Decline(world, "invest-1");

        Assert.True(result.Success);
        Assert.Equal(InvestorProposalStatus.Declined, world.InvestorProposals["invest-1"].Status);
        Assert.Equal(before, world.Ledger.Cash);
        Assert.Equal(beforeReserved, world.Ledger.Reserved);
    }

    [Fact]
    public void DeclineAfterAccept_IsRejected()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 1_000_000);
        world.AddInvestorProposal(MakeProposal());
        InvestorProposalEngine.Accept(world, "invest-1", currentTick: 0);

        var result = InvestorProposalEngine.Decline(world, "invest-1");
        Assert.False(result.Success);
    }

    [Fact]
    public void UnacceptedOffer_PastExpiry_LapsesToExpired_NoLedgerEffect()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 1_000_000);
        var before = world.Ledger.Cash;
        world.AddInvestorProposal(MakeProposal(offerExpiryTick: 5));

        var result = InvestorProposalEngine.EvaluateExpiryAndCondition(world, "invest-1", currentTick: 6);

        Assert.True(result.Success);
        Assert.Equal(InvestorProposalStatus.Expired, world.InvestorProposals["invest-1"].Status);
        Assert.Equal(before, world.Ledger.Cash);
    }

    [Fact]
    public void UnacceptedOffer_BeforeExpiry_StaysOffered()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 1_000_000);
        world.AddInvestorProposal(MakeProposal(offerExpiryTick: 100));

        InvestorProposalEngine.EvaluateExpiryAndCondition(world, "invest-1", currentTick: 5);

        Assert.Equal(InvestorProposalStatus.Offered, world.InvestorProposals["invest-1"].Status);
    }

    /// <summary>The condition is checked against REAL WorldState.Projects
    /// -- committing a real NewRoadConnector project (through the exact
    /// same PlanningEngine flow PlanningEngineTests uses) satisfies it,
    /// never a faked/assumed-true flag.</summary>
    [Fact]
    public void ConditionSatisfiedByARealCommittedProject_MarksFulfilled_NoClawback()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 1_000_000);
        world.AddInvestorProposal(MakeProposal(fundingAmountThb: 200_000, conditionWindowTicks: 50, requiredKind: ProjectKind.NewRoadConnector));
        InvestorProposalEngine.Accept(world, "invest-1", currentTick: 0);
        var cashAfterAccept = world.Ledger.Cash;

        var impact = PlanningEngine.EstimateNewRoadAccessImpact(world, SimulationFixtures.Node2, SimulationFixtures.Node3, SimulationFixtures.ResidentialBuildingId);
        var draft = new NewRoadConnectorDraft("road-for-condition", world.Revision, 50_000, impact, SimulationFixtures.Node2, SimulationFixtures.Node3);
        var commitResult = PlanningEngine.CommitNewRoadConnector(world, draft, LedgerAccountKind.Capex, new long[] { 50_000 });
        Assert.True(commitResult.Success);

        var evalResult = InvestorProposalEngine.EvaluateExpiryAndCondition(world, "invest-1", currentTick: 10);

        Assert.True(evalResult.Success);
        Assert.Equal(InvestorProposalStatus.Fulfilled, world.InvestorProposals["invest-1"].Status);
        Assert.Equal(0, world.InvestorProposals["invest-1"].AmountClawedBack);
        // Committing a project only RESERVES its cost (see MoneyLedger's
        // doc comment: cash moves only on PayMilestone) -- Cash itself is
        // unaffected here, proving Fulfilled never touches the ledger by
        // itself, only the road connector's own normal reservation did.
        Assert.Equal(cashAfterAccept, world.Ledger.Cash);
        Assert.Equal(50_000, world.Ledger.Reserved);
    }

    /// <summary>A project of the WRONG kind, or one that already existed
    /// BEFORE acceptance, must never satisfy the condition -- proving the
    /// baseline-count check is real, not a "does any project of this kind
    /// exist at all" shortcut.</summary>
    [Fact]
    public void PreexistingProjectOfRequiredKind_DoesNotRetroactivelySatisfyTheCondition()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 1_000_000);

        // A road connector committed BEFORE the proposal is even offered.
        var impact = PlanningEngine.EstimateNewRoadAccessImpact(world, SimulationFixtures.Node2, SimulationFixtures.Node3, SimulationFixtures.ResidentialBuildingId);
        var preexisting = new NewRoadConnectorDraft("preexisting-road", world.Revision, 50_000, impact, SimulationFixtures.Node2, SimulationFixtures.Node3);
        Assert.True(PlanningEngine.CommitNewRoadConnector(world, preexisting, LedgerAccountKind.Capex, new long[] { 50_000 }).Success);

        world.AddInvestorProposal(MakeProposal(fundingAmountThb: 200_000, conditionWindowTicks: 5, requiredKind: ProjectKind.NewRoadConnector));
        InvestorProposalEngine.Accept(world, "invest-1", currentTick: 0);
        var cashAfterAccept = world.Ledger.Cash;

        // No NEW road connector committed after acceptance -- deadline passes.
        var evalResult = InvestorProposalEngine.EvaluateExpiryAndCondition(world, "invest-1", currentTick: 10);

        Assert.True(evalResult.Success);
        Assert.Equal(InvestorProposalStatus.Withdrawn, world.InvestorProposals["invest-1"].Status);
        Assert.Equal(200_000, world.InvestorProposals["invest-1"].AmountClawedBack);
        Assert.Equal(cashAfterAccept - 200_000, world.Ledger.Cash);
    }

    /// <summary>The path the acceptance criteria names explicitly:
    /// condition violated -> funding withdrawn, with real ledger
    /// numbers before/after.</summary>
    [Fact]
    public void ConditionViolatedByDeadline_WithdrawsTheFullAmount_WithRealLedgerNumbers()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 1_000_000);
        world.AddInvestorProposal(MakeProposal(fundingAmountThb: 300_000, conditionWindowTicks: 5));
        InvestorProposalEngine.Accept(world, "invest-1", currentTick: 0);
        var cashAfterAccept = world.Ledger.Cash;
        Assert.Equal(1_000_000 + 300_000, cashAfterAccept);

        // Deadline is currentTick 0 + 5 = 5; evaluate at tick 6, past it, with the condition never met.
        var result = InvestorProposalEngine.EvaluateExpiryAndCondition(world, "invest-1", currentTick: 6);

        Assert.True(result.Success);
        var proposal = world.InvestorProposals["invest-1"];
        Assert.Equal(InvestorProposalStatus.Withdrawn, proposal.Status);
        Assert.Equal(300_000, proposal.AmountClawedBack);
        Assert.Equal(1_000_000, world.Ledger.Cash); // exactly back to the pre-acceptance amount.
    }

    /// <summary>Before the deadline, the accepted proposal must NOT be
    /// withdrawn even if the condition is still unmet -- premature
    /// clawback would be its own bug.</summary>
    [Fact]
    public void ConditionUnmetButDeadlineNotYetReached_StaysAccepted_NoClawback()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 1_000_000);
        world.AddInvestorProposal(MakeProposal(fundingAmountThb: 300_000, conditionWindowTicks: 50));
        InvestorProposalEngine.Accept(world, "invest-1", currentTick: 0);
        var cashAfterAccept = world.Ledger.Cash;

        var result = InvestorProposalEngine.EvaluateExpiryAndCondition(world, "invest-1", currentTick: 10);

        Assert.True(result.Success);
        Assert.Equal(InvestorProposalStatus.Accepted, world.InvestorProposals["invest-1"].Status);
        Assert.Equal(cashAfterAccept, world.Ledger.Cash);
    }

    /// <summary>If the player already spent the subsidy down below what
    /// is owed by the deadline, the clawback takes only what remains
    /// available -- it never forces cash negative -- and the ACTUAL
    /// amount is recorded, not assumed to be the full amount.</summary>
    [Fact]
    public void ClawbackNeverForcesNegativeCash_RecordsTheActualAmountTakenBack()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 1_000_000);
        world.AddInvestorProposal(MakeProposal(fundingAmountThb: 300_000, conditionWindowTicks: 5));
        InvestorProposalEngine.Accept(world, "invest-1", currentTick: 0);

        // Spend almost everything the ledger has, via a direct charge (a
        // real ledger operation, not a mocked balance) so Available is
        // far below the 300,000 owed.
        var spendable = world.Ledger.Available - 50_000;
        world.Ledger.ChargeDirect(LedgerAccountKind.NonRecurring, spendable);
        Assert.Equal(50_000, world.Ledger.Available);

        var result = InvestorProposalEngine.EvaluateExpiryAndCondition(world, "invest-1", currentTick: 6);

        Assert.True(result.Success);
        var proposal = world.InvestorProposals["invest-1"];
        Assert.Equal(InvestorProposalStatus.Withdrawn, proposal.Status);
        Assert.Equal(50_000, proposal.AmountClawedBack); // only what was actually available, not the full 300,000.
        Assert.Equal(0, world.Ledger.Available); // taken down to exactly zero, never negative.
    }

    [Fact]
    public void TerminalStatus_EvaluateIsIdempotent_NeverErrors()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 1_000_000);
        world.AddInvestorProposal(MakeProposal());
        InvestorProposalEngine.Decline(world, "invest-1");

        var result = InvestorProposalEngine.EvaluateExpiryAndCondition(world, "invest-1", currentTick: 1000);
        Assert.True(result.Success);
        Assert.Equal(InvestorProposalStatus.Declined, world.InvestorProposals["invest-1"].Status);
    }

    [Fact]
    public void NonPositiveFundingAmount_IsRejectedAtConstruction()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => MakeProposal(fundingAmountThb: 0));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => MakeProposal(fundingAmountThb: -1));
    }

    [Fact]
    public void DuplicateProposalId_IsRejected()
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 1_000_000);
        world.AddInvestorProposal(MakeProposal());
        Assert.Throws<System.ArgumentException>(() => world.AddInvestorProposal(MakeProposal()));
    }
}
