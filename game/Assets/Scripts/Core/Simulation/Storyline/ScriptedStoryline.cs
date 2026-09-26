// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Storyline;

/// <summary>
/// G6-09: the ONE fictional procurement/corruption storyline this wave
/// ships (plan §16: "คดีจัดซื้อแต่งขึ้นหนึ่งสาย" -- ONE strand, not an
/// open-ended catalog). "The overpaid resurfacing milestone": a road
/// project's milestone payment is alleged to have been inflated and
/// routed through a fabricated subcontractor; the player investigates
/// (surfacing through the project's own real MoneyLedger/CommittedProject
/// numbers, via <see cref="CorruptionCaseEngine"/>) and resolves it,
/// recovering some or all of the alleged overpayment as a one-off ledger
/// credit.
///
/// HOW THIS WAS CHECKED (AGENTS.md rule 9 -- "having a disclaimer is not
/// sufficient", so this is a description of an actual check performed,
/// not only a disclaimer):
///   - <see cref="ContractorLabel"/> ("Contractor Alpha") and
///     <see cref="AgencyLabel"/> are deliberately GENERIC, UNNAMED
///     placeholders -- no specific company name, no specific person's
///     name, and no specific province/district/place name is attached to
///     either. <see cref="AgencyLabel"/> names a ROLE ("a local
///     public-works sub-office") with no location at all, precisely so
///     it cannot be read as accusing any real, identifiable office.
///   - Neither label was drawn from, or checked against, any real company
///     registry, government directory, or the loaded MapPack's own
///     `source_tags` (`name`, `addr:*`, `operator`, ...) -- they were
///     authored from nothing but the two English common nouns
///     "contractor" and "office".
///   - <see cref="Open"/> takes only a <see cref="Planning.CommittedProject"/>
///     id string and a plain integer amount -- it CANNOT be passed a
///     <c>Thaivia.Core.MapPack</c> feature, tag, or coordinate at all, so
///     there is no code path by which this storyline could bind itself
///     to a real building, road, or address even if a future author
///     wanted it to (see CorruptionCaseEthicalControlTests' reflection
///     control, which enforces this for the WHOLE
///     <c>Thaivia.Core.Simulation.Storyline</c> namespace, not only this
///     one method).
///   - Recovery is described only as "a ledger credit representing
///     recovered funds" -- no real statute, penal code section, or legal
///     process is named or simulated anywhere in this file or in
///     <see cref="CorruptionCaseEngine"/>.
/// </summary>
public static class ScriptedStoryline
{
    public const string CaseId = "case-overpaid-resurfacing-001";

    /// <summary>Fictional placeholder -- see this type's doc comment.</summary>
    public const string ContractorLabel = "Contractor Alpha";

    /// <summary>Fictional, deliberately UNNAMED and UNLOCATED placeholder
    /// role -- see this type's doc comment for why no place/agency name
    /// is attached.</summary>
    public const string AgencyLabel = "a local public-works sub-office";

    /// <summary>Opens the one shipped storyline case, scripted around
    /// whichever REAL committed project the caller names -- this
    /// storyline never creates its own project, only investigates one
    /// the player already committed and paid on.</summary>
    public static CorruptionCaseResult Open(WorldState world, string relatedCommittedProjectId, long allegedOverpaymentThb) =>
        CorruptionCaseEngine.OpenCase(world, CaseId, relatedCommittedProjectId, ContractorLabel, allegedOverpaymentThb);
}
