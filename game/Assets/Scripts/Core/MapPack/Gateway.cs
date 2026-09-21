// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.MapPack;

/// <summary>A bounded external demand/capacity gateway where an edge
/// leaves the AOI (mirrors `map_pipeline.pipeline.boundary.Gateway`). The
/// demand/capacity fields are always a simulation_assumption -- there is
/// no "real" gateway data source -- which is why `Namespace` is present
/// and always "simulation_assumption", never omitted or defaulted to
/// looking like a source fact.</summary>
public sealed class Gateway
{
    public Gateway(
        long nodeId,
        long wayId,
        double lon,
        double lat,
        int inboundDemandVehPerHour,
        int outboundDemandVehPerHour,
        int externalCapacityVehPerHour,
        string rule,
        string @namespace)
    {
        NodeId = nodeId;
        WayId = wayId;
        Lon = lon;
        Lat = lat;
        InboundDemandVehPerHour = inboundDemandVehPerHour;
        OutboundDemandVehPerHour = outboundDemandVehPerHour;
        ExternalCapacityVehPerHour = externalCapacityVehPerHour;
        Rule = rule;
        Namespace = @namespace;
    }

    public long NodeId { get; }
    public long WayId { get; }
    public double Lon { get; }
    public double Lat { get; }
    public int InboundDemandVehPerHour { get; }
    public int OutboundDemandVehPerHour { get; }
    public int ExternalCapacityVehPerHour { get; }
    public string Rule { get; }
    public string Namespace { get; }
}
