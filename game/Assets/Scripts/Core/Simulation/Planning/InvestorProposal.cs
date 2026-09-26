// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using Thaivia.Core.Simulation.Economy;

namespace Thaivia.Core.Simulation.Planning;

/// <summary>
/// G6-08 (plan §16: "ข้อเสนอนักลงทุนหนึ่งระบบย่อ" -- one MINOR system,
/// deliberately kept small): a capped, fixed-amount funding offer with a
/// single condition -- commit a project of <see cref="RequiredProjectKind"/>
/// within <see cref="ConditionWindowTicks"/> of accepting. "Capped" means
/// <see cref="FundingAmountThb"/> is a fixed, definite integer amount
/// (validated positive at construction) -- never an open-ended or
/// percentage-of-something offer.
///
/// Instances are replaced, never mutated in place (the <c>With*</c>
/// methods), the same discipline every other simulation-state type in
/// this codebase already uses (<see cref="CommittedProject"/>,
/// <see cref="Mobility.Incidents.IncidentSite"/>'s replace-not-mutate
/// pattern via WorldState, etc.).
///
/// See <see cref="InvestorProposalEngine"/> for the transactional accept/
/// decline/evaluate flow this data type backs.
/// </summary>
public sealed class InvestorProposal
{
    public InvestorProposal(
        string id,
        long fundingAmountThb,
        LedgerAccountKind ledgerKind,
        ProjectKind requiredProjectKind,
        long conditionWindowTicks,
        long offerExpiryTick,
        InvestorProposalStatus status = InvestorProposalStatus.Offered,
        long? acceptedAtTick = null,
        long? conditionDeadlineTick = null,
        int? baselineRequiredKindCount = null,
        long amountClawedBack = 0)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("InvestorProposal.Id must not be empty.", nameof(id));
        }

        if (fundingAmountThb <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fundingAmountThb), "A proposal must offer a positive, definite (capped) amount.");
        }

        if (conditionWindowTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(conditionWindowTicks));
        }

        if (offerExpiryTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offerExpiryTick));
        }

        if (amountClawedBack < 0 || amountClawedBack > fundingAmountThb)
        {
            throw new ArgumentOutOfRangeException(nameof(amountClawedBack));
        }

        Id = id;
        FundingAmountThb = fundingAmountThb;
        LedgerKind = ledgerKind;
        RequiredProjectKind = requiredProjectKind;
        ConditionWindowTicks = conditionWindowTicks;
        OfferExpiryTick = offerExpiryTick;
        Status = status;
        AcceptedAtTick = acceptedAtTick;
        ConditionDeadlineTick = conditionDeadlineTick;
        BaselineRequiredKindCount = baselineRequiredKindCount;
        AmountClawedBack = amountClawedBack;
    }

    public string Id { get; }
    public long FundingAmountThb { get; }
    public LedgerAccountKind LedgerKind { get; }
    public ProjectKind RequiredProjectKind { get; }
    public long ConditionWindowTicks { get; }

    /// <summary>Absolute tick by which the OFFER (not the condition) must
    /// be accepted or it lapses to <see cref="InvestorProposalStatus.Expired"/>.</summary>
    public long OfferExpiryTick { get; }

    public InvestorProposalStatus Status { get; }
    public long? AcceptedAtTick { get; }

    /// <summary>Absolute tick by which <see cref="RequiredProjectKind"/>
    /// must have been committed, or the funding is withdrawn. Set only
    /// once <see cref="Status"/> reaches <see cref="InvestorProposalStatus.Accepted"/>.</summary>
    public long? ConditionDeadlineTick { get; }

    /// <summary>How many committed (non-cancelled) projects of
    /// <see cref="RequiredProjectKind"/> already existed at the moment of
    /// acceptance -- the condition is satisfied only once the count rises
    /// ABOVE this baseline, so a project committed before the offer was
    /// even accepted can never satisfy it. See
    /// <see cref="InvestorProposalEngine"/>'s doc comment for why this
    /// avoids needing a commit timestamp on <see cref="CommittedProject"/>.</summary>
    public int? BaselineRequiredKindCount { get; }

    /// <summary>How much was actually clawed back on
    /// <see cref="InvestorProposalStatus.Withdrawn"/> -- may be LESS than
    /// <see cref="FundingAmountThb"/> if the player had already spent
    /// below that amount by the deadline (a stated simplification: this
    /// system never forces cash negative -- see
    /// <see cref="InvestorProposalEngine"/>'s doc comment).</summary>
    public long AmountClawedBack { get; }

    public InvestorProposal WithAccepted(long currentTick, int baselineRequiredKindCount) =>
        new(Id, FundingAmountThb, LedgerKind, RequiredProjectKind, ConditionWindowTicks, OfferExpiryTick,
            InvestorProposalStatus.Accepted, currentTick, currentTick + ConditionWindowTicks, baselineRequiredKindCount);

    public InvestorProposal WithDeclined() =>
        new(Id, FundingAmountThb, LedgerKind, RequiredProjectKind, ConditionWindowTicks, OfferExpiryTick,
            InvestorProposalStatus.Declined, AcceptedAtTick, ConditionDeadlineTick, BaselineRequiredKindCount, AmountClawedBack);

    public InvestorProposal WithExpired() =>
        new(Id, FundingAmountThb, LedgerKind, RequiredProjectKind, ConditionWindowTicks, OfferExpiryTick,
            InvestorProposalStatus.Expired, AcceptedAtTick, ConditionDeadlineTick, BaselineRequiredKindCount, AmountClawedBack);

    public InvestorProposal WithFulfilled() =>
        new(Id, FundingAmountThb, LedgerKind, RequiredProjectKind, ConditionWindowTicks, OfferExpiryTick,
            InvestorProposalStatus.Fulfilled, AcceptedAtTick, ConditionDeadlineTick, BaselineRequiredKindCount, AmountClawedBack);

    public InvestorProposal WithWithdrawn(long amountClawedBack) =>
        new(Id, FundingAmountThb, LedgerKind, RequiredProjectKind, ConditionWindowTicks, OfferExpiryTick,
            InvestorProposalStatus.Withdrawn, AcceptedAtTick, ConditionDeadlineTick, BaselineRequiredKindCount, amountClawedBack);
}
