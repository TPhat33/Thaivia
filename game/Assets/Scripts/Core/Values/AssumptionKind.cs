// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Values;

/// <summary>
/// Which of the two assumption namespaces (see AGENTS.md rule 3/4 and
/// `map_pipeline.pipeline.tags`) an <see cref="SourceValue{T}.Assumed"/>
/// value belongs to. A visual assumption only affects how something is
/// drawn (e.g. a default building height when the source has none); a
/// simulation assumption only affects simulation inputs (e.g. a default
/// gateway demand). Neither is ever a source fact.
/// </summary>
public enum AssumptionKind
{
    Visual,
    Simulation,
}
