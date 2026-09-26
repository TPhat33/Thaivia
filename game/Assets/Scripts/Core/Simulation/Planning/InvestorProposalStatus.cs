// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Planning;

/// <summary>One <see cref="InvestorProposal"/>'s lifecycle state. See that
/// type's doc comment for the full accept/decline/expire/withdraw flow.</summary>
public enum InvestorProposalStatus
{
    /// <summary>Offered, not yet accepted or declined. Can still expire
    /// (<see cref="InvestorProposal.OfferExpiryTick"/>) with no ledger
    /// effect at all.</summary>
    Offered,

    /// <summary>The player declined before the offer expired. Terminal.
    /// No ledger effect ever touched this proposal.</summary>
    Declined,

    /// <summary>The offer window passed while still Offered (the player
    /// neither accepted nor declined in time). Terminal. No ledger effect
    /// ever touched this proposal.</summary>
    Expired,

    /// <summary>Accepted: funding was credited, and the condition window
    /// is running. Not terminal -- resolves to either
    /// <see cref="Fulfilled"/> or <see cref="Withdrawn"/>.</summary>
    Accepted,

    /// <summary>The required project kind was committed within the
    /// condition window. Terminal. The funding stays with the player.</summary>
    Fulfilled,

    /// <summary>The condition window elapsed without the required project
    /// kind being committed. Terminal. The funding (or as much of it as
    /// was still available) was clawed back -- see
    /// <see cref="InvestorProposal.AmountClawedBack"/>.</summary>
    Withdrawn,
}
