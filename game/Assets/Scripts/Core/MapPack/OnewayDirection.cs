// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.MapPack;

/// <summary>Mirrors `map_pipeline.pipeline.graph._normalize_oneway`'s three
/// output values exactly ("no" | "forward" | "reversed"); "reversed" is
/// what `oneway=-1` (and any other value the pipeline maps to it) becomes.</summary>
public enum OnewayDirection
{
    No,
    Forward,
    Reversed,
}
