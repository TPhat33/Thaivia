// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Thaivia.Core.Simulation.Planning;

/// <summary>Outcome of an <see cref="InvestorProposalEngine"/> action --
/// deliberately NOT <see cref="CommitResult"/> (that type carries a
/// <see cref="CommittedProject"/>, which an investor proposal is not).</summary>
public sealed record InvestorProposalResult(bool Success, IReadOnlyList<string> Failures);

/// <summary>
/// G6-08: accept/decline/evaluate flow for an <see cref="InvestorProposal"/>,
/// using the SAME transactional discipline
/// <see cref="PlanningEngine"/> already proves for committed projects:
/// accepting credits the integer <see cref="Economy.MoneyLedger"/>
/// atomically in one call, declining or letting an offer expire has NO
/// ledger side effect at all, and a condition violation withdraws funding
/// against real WorldState state -- never a faked/assumed-satisfied
/// check.
///
/// Condition-checking design note: <see cref="Planning.CommittedProject"/>
/// does not carry a "committed at tick" timestamp, so "was the required
/// project kind committed WITHIN the window" is checked by comparing the
/// CURRENT count of non-cancelled committed projects of that kind against
/// a BASELINE count snapshotted at the moment of acceptance
/// (<see cref="InvestorProposal.BaselineRequiredKindCount"/>) -- the
/// condition is satisfied only once the count rises strictly above that
/// baseline, so a project committed before the offer was even accepted
/// can never retroactively satisfy it. This is real WorldState state
/// (<see cref="Simulation.WorldState.Projects"/>), read fresh every call,
/// never cached or assumed.
///
/// Clawback design note: on a condition violation, this engine withdraws
/// <c>Min(FundingAmountThb, ledger.Available)</c> -- it never forces cash
/// negative. If the player already spent the subsidy below what is owed,
/// the clawback is only what remains available; the ACTUAL amount is
/// recorded on <see cref="InvestorProposal.AmountClawedBack"/> rather than
/// silently assumed to be the full amount. This keeps the system minor
/// (plan §16) by not requiring a debt/negative-cash model elsewhere in
/// the ledger.
/// </summary>
public static class InvestorProposalEngine
{
    public static InvestorProposalResult Accept(WorldState world, string proposalId, long currentTick)
    {
        if (!world.InvestorProposals.TryGetValue(proposalId, out var proposal))
        {
            return new InvestorProposalResult(false, new[] { $"no such investor proposal: {proposalId}" });
        }

        var failures = new List<string>();
        if (proposal.Status != InvestorProposalStatus.Offered)
        {
            failures.Add($"proposal {proposalId} is not Offered (currently {proposal.Status}) -- cannot accept.");
        }

        if (currentTick > proposal.OfferExpiryTick)
        {
            failures.Add($"proposal {proposalId}'s offer window has passed (expiry tick {proposal.OfferExpiryTick}, current tick {currentTick}).");
        }

        if (failures.Count > 0)
        {
            return new InvestorProposalResult(false, failures);
        }

        var baselineCount = CountNonCancelledProjectsOfKind(world, proposal.RequiredProjectKind);

        // Single atomic credit -- the only ledger mutation this method
        // performs, and it happens exactly once per successful Accept
        // call (re-accepting an already-Accepted/terminal proposal is
        // rejected above, so this can never double-credit).
        world.Ledger.Deposit(proposal.LedgerKind, proposal.FundingAmountThb);

        var updated = proposal.WithAccepted(currentTick, baselineCount);
        world.ReplaceInvestorProposal(updated);
        world.IncrementRevision();
        world.AddDelta(new PlayerDelta(
            proposalId,
            PlayerDeltaKind.InvestorProposalAcceptance,
            DateTimeOffset.UtcNow,
            $"Accepted investor proposal {proposalId}: +{proposal.FundingAmountThb} THB, condition: commit a {proposal.RequiredProjectKind} project by tick {updated.ConditionDeadlineTick}."));

        return new InvestorProposalResult(true, Array.Empty<string>());
    }

    /// <summary>Declining (or an unaccepted offer simply expiring, via
    /// <see cref="EvaluateExpiryAndCondition"/>) NEVER touches the ledger
    /// -- there is no code path in this method that calls any
    /// <see cref="Economy.MoneyLedger"/> method at all.</summary>
    public static InvestorProposalResult Decline(WorldState world, string proposalId)
    {
        if (!world.InvestorProposals.TryGetValue(proposalId, out var proposal))
        {
            return new InvestorProposalResult(false, new[] { $"no such investor proposal: {proposalId}" });
        }

        if (proposal.Status != InvestorProposalStatus.Offered)
        {
            return new InvestorProposalResult(false, new[] { $"proposal {proposalId} is not Offered (currently {proposal.Status}) -- cannot decline." });
        }

        world.ReplaceInvestorProposal(proposal.WithDeclined());
        return new InvestorProposalResult(true, Array.Empty<string>());
    }

    /// <summary>Call once per tick (or on demand) per live proposal: lapses
    /// an unaccepted offer past its expiry (no ledger effect), marks an
    /// accepted proposal Fulfilled once the required project kind's count
    /// rises above baseline, or withdraws funding once the condition
    /// deadline passes unmet. Idempotent on a terminal-status proposal
    /// (Declined/Expired/Fulfilled/Withdrawn): a no-op success, never an
    /// error, so a caller does not need to track which proposals are
    /// still live.</summary>
    public static InvestorProposalResult EvaluateExpiryAndCondition(WorldState world, string proposalId, long currentTick)
    {
        if (!world.InvestorProposals.TryGetValue(proposalId, out var proposal))
        {
            return new InvestorProposalResult(false, new[] { $"no such investor proposal: {proposalId}" });
        }

        switch (proposal.Status)
        {
            case InvestorProposalStatus.Offered:
                if (currentTick > proposal.OfferExpiryTick)
                {
                    world.ReplaceInvestorProposal(proposal.WithExpired());
                }

                return new InvestorProposalResult(true, Array.Empty<string>());

            case InvestorProposalStatus.Accepted:
                return EvaluateAccepted(world, proposal, currentTick);

            default:
                // Declined/Expired/Fulfilled/Withdrawn are all terminal --
                // nothing left to evaluate, never an error.
                return new InvestorProposalResult(true, Array.Empty<string>());
        }
    }

    private static InvestorProposalResult EvaluateAccepted(WorldState world, InvestorProposal proposal, long currentTick)
    {
        var currentCount = CountNonCancelledProjectsOfKind(world, proposal.RequiredProjectKind);
        if (currentCount > proposal.BaselineRequiredKindCount)
        {
            world.ReplaceInvestorProposal(proposal.WithFulfilled());
            return new InvestorProposalResult(true, Array.Empty<string>());
        }

        if (currentTick > proposal.ConditionDeadlineTick)
        {
            var owed = proposal.FundingAmountThb;
            var actualClawback = Math.Min(owed, world.Ledger.Available);
            if (actualClawback > 0)
            {
                world.Ledger.ChargeDirect(proposal.LedgerKind, actualClawback);
            }

            world.ReplaceInvestorProposal(proposal.WithWithdrawn(actualClawback));
            world.IncrementRevision();
            world.AddDelta(new PlayerDelta(
                proposal.Id,
                PlayerDeltaKind.InvestorProposalAcceptance,
                DateTimeOffset.UtcNow,
                $"Investor proposal {proposal.Id} condition violated by tick {currentTick}: clawed back {actualClawback} of {owed} THB owed."));
        }

        return new InvestorProposalResult(true, Array.Empty<string>());
    }

    private static int CountNonCancelledProjectsOfKind(WorldState world, ProjectKind kind) =>
        world.Projects.Values.Count(p => p.Kind == kind && p.Status != ProjectStatus.Cancelled);
}
