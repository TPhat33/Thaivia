// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.MapPack;

/// <summary>
/// Where this MapPack's source bytes came from, and -- critically --
/// whether it is synthetic. <see cref="Synthetic"/> is never inferred by
/// this loader; it is read straight from the file the pipeline wrote, and
/// <see cref="Thaivia.Core.Serialization.MapPackLoader"/> exposes it
/// unmissable on <see cref="MapPackDocument"/> so a viewer can refuse to
/// present a synthetic pack as a real place (AGENTS.md rule 2).
/// </summary>
public sealed class Provenance
{
    public Provenance(
        ProvenanceSourceKind sourceKind,
        string sourceLocation,
        string sha256,
        long sizeBytes,
        string? sourceSnapshotTimestamp,
        string? sourceSnapshotUnknownReason,
        string settingsHash,
        bool synthetic,
        string? syntheticNotice,
        Attribution attribution)
    {
        SourceKind = sourceKind;
        SourceLocation = sourceLocation;
        Sha256 = sha256;
        SizeBytes = sizeBytes;
        SourceSnapshotTimestamp = sourceSnapshotTimestamp;
        SourceSnapshotUnknownReason = sourceSnapshotUnknownReason;
        SettingsHash = settingsHash;
        Synthetic = synthetic;
        SyntheticNotice = syntheticNotice;
        Attribution = attribution;
    }

    public ProvenanceSourceKind SourceKind { get; }
    public string SourceLocation { get; }
    public string Sha256 { get; }
    public long SizeBytes { get; }
    public string? SourceSnapshotTimestamp { get; }
    public string? SourceSnapshotUnknownReason { get; }
    public string SettingsHash { get; }
    public bool Synthetic { get; }
    public string? SyntheticNotice { get; }
    public Attribution Attribution { get; }
}
