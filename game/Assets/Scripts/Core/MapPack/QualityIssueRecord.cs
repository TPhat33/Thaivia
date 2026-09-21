// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.MapPack;

public sealed class QualityIssueRecord
{
    public QualityIssueRecord(string kind, string featureId, string detail)
    {
        Kind = kind;
        FeatureId = featureId;
        Detail = detail;
    }

    public string Kind { get; }
    public string FeatureId { get; }
    public string Detail { get; }
}
