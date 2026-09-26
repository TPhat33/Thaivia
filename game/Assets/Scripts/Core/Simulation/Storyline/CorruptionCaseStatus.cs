// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Storyline;

/// <summary>One <see cref="CorruptionCase"/>'s lifecycle. See that type's
/// doc comment and <see cref="CorruptionCaseEngine"/> for the full flow.</summary>
public enum CorruptionCaseStatus
{
    Open,
    UnderInvestigation,

    /// <summary>Terminal: resolved with a one-off recovery credited to
    /// the ledger. A Resolved case can never be resolved again -- see
    /// <see cref="CorruptionCaseEngine.ResolveWithRecovery"/>.</summary>
    Resolved,

    /// <summary>Terminal: investigated and closed with NO recovery (e.g.
    /// insufficient evidence in-fiction). No ledger effect.</summary>
    Dismissed,
}
