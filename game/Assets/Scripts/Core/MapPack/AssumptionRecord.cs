// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Text.Json;

namespace Thaivia.Core.MapPack;

/// <summary>
/// A single visual_assumption or simulation_assumption entry as baked by
/// `map_pipeline.pipeline.tags.Assumption.to_json`: a field name, the
/// filled-in value, and the rule that produced it. This type never lives
/// inside <see cref="SourceTags"/> -- it is carried on its own
/// `VisualAssumptions`/`SimulationAssumptions` list on the owning feature,
/// which is how the MapPack (and this loader) keep source facts and
/// assumptions structurally apart.
/// </summary>
public sealed class AssumptionRecord
{
    public AssumptionRecord(string field, JsonElement value, string rule)
    {
        Field = field;
        Value = value.Clone();
        Rule = rule;
    }

    public string Field { get; }

    /// <summary>Raw JSON value (number/string/bool) -- the assumption's
    /// domain type varies by field, so this loader does not narrow it
    /// further than the source JSON; call sites that know the field's
    /// expected shape convert it via the SourceValue-returning helpers in
    /// Thaivia.Core.Attributes.</summary>
    public JsonElement Value { get; }

    public string Rule { get; }
}
