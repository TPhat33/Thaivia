// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Storyline;

/// <summary>
/// G6-09: one fictional procurement/corruption case, tied to a REAL
/// <see cref="Planning.CommittedProject"/> the player actually committed
/// (<see cref="RelatedCommittedProjectId"/>), never a project this
/// subsystem invents on its own.
///
/// ETHICAL CONSTRAINT (AGENTS.md rule 9, non-negotiable -- plan §7: "ห้าม
/// ใช้ชื่อบุคคล/กิจการจริงเป็นผู้ผิด... ต้องตรวจการเชื่อมโยงกลับถึง
/// สถานที่จริง ไม่ใช่เชื่อว่ามี disclaimer แล้วทุกอย่างปลอดปัญหา"):
///   - <see cref="ContractorLabel"/> is a generic, invented PLACEHOLDER
///     label (see <see cref="ScriptedStoryline"/>'s doc comment for the
///     one shipped storyline's actual labels and how they were checked)
///     -- never a real company/agency/person's name.
///   - This type, and every type in this namespace, structurally CANNOT
///     reference a real source feature: no constructor, method, or
///     property anywhere in <c>Thaivia.Core.Simulation.Storyline</c>
///     takes or returns a <c>Thaivia.Core.MapPack</c>-namespace type
///     (a <c>PolygonFeature</c>, <c>LineFeature</c>, <c>SourceTags</c>,
///     <c>RoadEdge</c>, or anything else GeographyBase-layer) -- see
///     CorruptionCaseEthicalControlTests' reflection-based structural
///     control, which would fail the build's test suite the moment a
///     future contributor tried to add a real-feature-id/name/address
///     parameter here, exactly the way IncidentEngineTests' control does
///     for BuildingArchetype and G6-04's does for a bare scalar score.
///   - Asset recovery (<see cref="RecoveredAmountThb"/>) is a ONE-OFF
///     ledger credit (plan §12: "ยึดทรัพย์ใน storyline เป็น one-off
///     recovery ไม่ใช่ระบบรายได้หลัก") -- a Resolved case is terminal and
///     can never be resolved a second time (see
///     <see cref="CorruptionCaseEngine.ResolveWithRecovery"/>), so this
///     can never become a repeatable income source. Nothing here cites
///     or simulates a real law/statute -- recovery is described only as
///     "a ledger credit representing recovered funds", never tied to a
///     named real legal provision.
/// </summary>
public sealed class CorruptionCase
{
    public CorruptionCase(
        string caseId,
        string relatedCommittedProjectId,
        string contractorLabel,
        long allegedOverpaymentThb,
        CorruptionCaseStatus status = CorruptionCaseStatus.Open,
        long recoveredAmountThb = 0)
    {
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new ArgumentException("CorruptionCase.CaseId must not be empty.", nameof(caseId));
        }

        if (string.IsNullOrWhiteSpace(relatedCommittedProjectId))
        {
            throw new ArgumentException("CorruptionCase.RelatedCommittedProjectId must not be empty.", nameof(relatedCommittedProjectId));
        }

        if (string.IsNullOrWhiteSpace(contractorLabel))
        {
            throw new ArgumentException("CorruptionCase.ContractorLabel must not be empty.", nameof(contractorLabel));
        }

        if (allegedOverpaymentThb <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(allegedOverpaymentThb), "An alleged overpayment must be a positive amount.");
        }

        if (recoveredAmountThb < 0 || recoveredAmountThb > allegedOverpaymentThb)
        {
            throw new ArgumentOutOfRangeException(nameof(recoveredAmountThb), "Recovery can never exceed the amount alleged.");
        }

        CaseId = caseId;
        RelatedCommittedProjectId = relatedCommittedProjectId;
        ContractorLabel = contractorLabel;
        AllegedOverpaymentThb = allegedOverpaymentThb;
        Status = status;
        RecoveredAmountThb = recoveredAmountThb;
    }

    public string CaseId { get; }

    /// <summary>The id of a REAL <see cref="Planning.CommittedProject"/>
    /// this case is scripted around -- <see cref="CorruptionCaseEngine.OpenCase"/>
    /// rejects any id that is not an actual entry in
    /// <see cref="Simulation.WorldState.Projects"/>.</summary>
    public string RelatedCommittedProjectId { get; }

    /// <summary>A generic, invented placeholder label (e.g. "Contractor
    /// Alpha") -- never a real company/agency/person's name. This type's
    /// constructor cannot validate that in general (there is no
    /// computable test for "is this string a real company"), so the
    /// actual guarantee is upstream: the ONE shipped storyline
    /// (<see cref="ScriptedStoryline"/>) hard-codes deliberately generic
    /// labels, and this constructor at least rejects an empty one.</summary>
    public string ContractorLabel { get; }

    /// <summary>Capped by construction to what was ACTUALLY paid on the
    /// related project at case-opening time (enforced by
    /// <see cref="CorruptionCaseEngine.OpenCase"/>, which reads the real
    /// <see cref="Planning.CommittedProject.TotalPaid"/>) -- this fiction
    /// is tied to real ledger history, never an arbitrary invented
    /// number.</summary>
    public long AllegedOverpaymentThb { get; }

    public CorruptionCaseStatus Status { get; }

    /// <summary>0 until <see cref="CorruptionCaseStatus.Resolved"/>, then
    /// the ONE-OFF amount actually credited back to the ledger.</summary>
    public long RecoveredAmountThb { get; }

    public CorruptionCase WithStatus(CorruptionCaseStatus newStatus) =>
        new(CaseId, RelatedCommittedProjectId, ContractorLabel, AllegedOverpaymentThb, newStatus, RecoveredAmountThb);

    public CorruptionCase WithResolved(long recoveredAmountThb) =>
        new(CaseId, RelatedCommittedProjectId, ContractorLabel, AllegedOverpaymentThb, CorruptionCaseStatus.Resolved, recoveredAmountThb);
}
