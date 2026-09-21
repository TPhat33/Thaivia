// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Globalization;
using Thaivia.Core.MapPack;
using Thaivia.Core.Values;

namespace Thaivia.Core.Attributes;

/// <summary>
/// Turns a <see cref="PolygonFeature"/>'s raw source tags + assumption
/// arrays into the tri-state <see cref="SourceValue{T}"/> an inspector
/// panel actually wants to show, instead of every call site re-deriving
/// "is height known, assumed, or unknown" by hand. This is the intended
/// consumer of `SourceValue` for GeographyBase features (see G2-04's
/// inspector requirement: source / unknown / assumption must render as
/// three visually distinct categories).
/// </summary>
public static class BuildingAttributes
{
    public static SourceValue<double> GetHeightMeters(PolygonFeature feature)
    {
        var raw = feature.SourceTags.Get("height");
        if (raw != null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
        {
            return SourceValue<double>.Known(height);
        }

        foreach (var a in feature.VisualAssumptions)
        {
            if (a.Field == "height" && a.Value.TryGetDouble(out var assumed))
            {
                return SourceValue<double>.Assumed(assumed, a.Rule, AssumptionKind.Visual);
            }
        }

        return SourceValue<double>.Unknown();
    }

    public static SourceValue<string> GetBuildingUse(PolygonFeature feature)
    {
        var raw = feature.SourceTags.Get("building");
        if (raw != null && raw != "yes")
        {
            return SourceValue<string>.Known(raw);
        }

        foreach (var a in feature.VisualAssumptions)
        {
            if (a.Field == "building_use" && a.Value.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return SourceValue<string>.Assumed(a.Value.GetString()!, a.Rule, AssumptionKind.Visual);
            }
        }

        // `building=yes` (or the tag missing entirely) means the source
        // says "this is a building" but not what kind -- that is Unknown,
        // never silently "yes" treated as a use.
        return SourceValue<string>.Unknown();
    }
}
