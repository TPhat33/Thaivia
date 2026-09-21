// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;

namespace Thaivia.Core.MapPack;

/// <summary>
/// Mirrors `map_pipeline.pipeline.graph.TurnRestriction`. `Supported ==
/// false` means the pipeline recognised a `type=restriction` relation but
/// could not model it (a via-way restriction, an unrecognized
/// restriction value, or a dangling member) -- Thaivia.Core.Graph never
/// silently drops these; RoadGraphIndex.EvaluateTurn reports them as
/// Unsupported rather than treating them as "no restriction".
/// </summary>
public sealed class TurnRestrictionRecord
{
    public TurnRestrictionRecord(
        long relationId,
        string restrictionType,
        long? fromWay,
        IReadOnlyList<long> via,
        string viaKind,
        long? toWay,
        bool supported,
        string? unsupportedReason)
    {
        RelationId = relationId;
        RestrictionType = restrictionType;
        FromWay = fromWay;
        Via = via;
        ViaKind = viaKind;
        ToWay = toWay;
        Supported = supported;
        UnsupportedReason = unsupportedReason;
    }

    public long RelationId { get; }
    public string RestrictionType { get; }
    public long? FromWay { get; }
    public IReadOnlyList<long> Via { get; }

    /// <summary>"n" (node), "w" (way), or "unknown" -- matches the Python
    /// pipeline's member.type strings verbatim.</summary>
    public string ViaKind { get; }
    public long? ToWay { get; }
    public bool Supported { get; }
    public string? UnsupportedReason { get; }
}
