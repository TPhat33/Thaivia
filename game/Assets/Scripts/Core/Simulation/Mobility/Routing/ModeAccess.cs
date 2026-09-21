// Pure C# -- no UnityEngine reference. See game/README.md.
using Thaivia.Core.MapPack;

namespace Thaivia.Core.Simulation.Mobility.Routing;

/// <summary>
/// Reads an OSM-derived <see cref="RoadEdge.AccessModes"/> tag set (mirrors
/// the pipeline's normalized access keys: "foot", "motor_vehicle", "hgv")
/// and decides whether a mode may use an edge at all. An ABSENT key is
/// treated as "not stated by the source" -- per AGENTS.md rule 4 ("no data
/// = unknown, not empty"), this is a documented
/// <c>simulation_assumption</c> (default-open access for a public road),
/// never claimed as a source fact. Only an explicit "no"/"private" value
/// denies a mode; an explicit "destination"/"permit" value is treated the
/// same as denied for through-routing (a through trip should never route
/// itself down a destination-only lane).
/// </summary>
public static class ModeAccess
{
    private static readonly string[] ThroughRoutingDeniedValues = { "no", "private", "destination", "permit", "customers" };

    public static bool IsAllowed(RoadEdge edge, TravelMode mode)
    {
        var key = ModeAccessKey(mode);
        var value = edge.AccessModes.TryGetValue(key, out var v) ? v : null;
        if (value is null)
        {
            return true; // simulation_assumption: unstated access defaults open.
        }

        foreach (var denied in ThroughRoutingDeniedValues)
        {
            if (string.Equals(value, denied, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string ModeAccessKey(TravelMode mode) => mode switch
    {
        TravelMode.Walk => "foot",
        TravelMode.Vehicle => "motor_vehicle",
        TravelMode.Freight => "hgv",
        _ => throw new System.ArgumentOutOfRangeException(nameof(mode)),
    };
}
