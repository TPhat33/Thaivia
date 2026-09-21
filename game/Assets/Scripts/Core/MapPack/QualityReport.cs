// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;

namespace Thaivia.Core.MapPack;

public sealed class QualityReport
{
    public QualityReport(
        IReadOnlyDictionary<string, int> unknownCounts,
        IReadOnlyList<QualityIssueRecord> dropped,
        IReadOnlyList<QualityIssueRecord> quarantined,
        IReadOnlyList<QualityIssueRecord> incompleteRelations,
        IReadOnlyList<QualityIssueRecord> unsupportedRestrictions,
        IReadOnlyList<QualityIssueRecord> topologyConflicts,
        IReadOnlyList<QualityIssueRecord> duplicateAssociations,
        IReadOnlyDictionary<string, int> retainedCounts,
        IReadOnlyDictionary<string, int> rejectedCounts,
        IReadOnlyDictionary<string, double> roundtripErrorM)
    {
        UnknownCounts = unknownCounts;
        Dropped = dropped;
        Quarantined = quarantined;
        IncompleteRelations = incompleteRelations;
        UnsupportedRestrictions = unsupportedRestrictions;
        TopologyConflicts = topologyConflicts;
        DuplicateAssociations = duplicateAssociations;
        RetainedCounts = retainedCounts;
        RejectedCounts = rejectedCounts;
        RoundtripErrorM = roundtripErrorM;
    }

    public IReadOnlyDictionary<string, int> UnknownCounts { get; }
    public IReadOnlyList<QualityIssueRecord> Dropped { get; }
    public IReadOnlyList<QualityIssueRecord> Quarantined { get; }
    public IReadOnlyList<QualityIssueRecord> IncompleteRelations { get; }
    public IReadOnlyList<QualityIssueRecord> UnsupportedRestrictions { get; }
    public IReadOnlyList<QualityIssueRecord> TopologyConflicts { get; }
    public IReadOnlyList<QualityIssueRecord> DuplicateAssociations { get; }
    public IReadOnlyDictionary<string, int> RetainedCounts { get; }
    public IReadOnlyDictionary<string, int> RejectedCounts { get; }

    /// <summary>Keys "max"/"mean"/"sample_count" -- measured, not asserted,
    /// by the Python pipeline; this loader passes it through unchanged.</summary>
    public IReadOnlyDictionary<string, double> RoundtripErrorM { get; }
}
