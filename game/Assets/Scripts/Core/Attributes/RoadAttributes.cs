// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Globalization;
using Thaivia.Core.MapPack;
using Thaivia.Core.Values;

namespace Thaivia.Core.Attributes;

public static class RoadAttributes
{
    public static SourceValue<double> GetWidthMeters(RoadEdge edge)
    {
        var raw = edge.SourceTags.Get("width");
        if (raw != null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
        {
            return SourceValue<double>.Known(width);
        }

        foreach (var a in edge.VisualAssumptions)
        {
            if (a.Field == "width" && a.Value.TryGetDouble(out var assumed))
            {
                return SourceValue<double>.Assumed(assumed, a.Rule, AssumptionKind.Visual);
            }
        }

        return SourceValue<double>.Unknown();
    }

    public static SourceValue<int> GetLaneCount(RoadEdge edge)
    {
        var raw = edge.SourceTags.Get("lanes");
        if (raw != null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lanes))
        {
            return SourceValue<int>.Known(lanes);
        }

        foreach (var a in edge.VisualAssumptions)
        {
            if (a.Field == "lanes" && a.Value.TryGetInt32(out var assumed))
            {
                return SourceValue<int>.Assumed(assumed, a.Rule, AssumptionKind.Visual);
            }
        }

        return SourceValue<int>.Unknown();
    }

    public static SourceValue<int> GetMaxSpeedKph(RoadEdge edge)
    {
        var raw = edge.SourceTags.Get("maxspeed");
        if (raw != null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var speed))
        {
            return SourceValue<int>.Known(speed);
        }

        foreach (var a in edge.SimulationAssumptions)
        {
            if (a.Field == "maxspeed" && a.Value.TryGetInt32(out var assumed))
            {
                return SourceValue<int>.Assumed(assumed, a.Rule, AssumptionKind.Simulation);
            }
        }

        return SourceValue<int>.Unknown();
    }
}
