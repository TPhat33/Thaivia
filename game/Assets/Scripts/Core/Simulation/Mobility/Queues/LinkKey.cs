// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Mobility.Queues;

/// <summary>Identifies one directed traversal of one road_graph way --
/// the unit network queues/capacity are tracked per. `Forward` means "in
/// the way's NodeRefs[0] -&gt; NodeRefs[^1] order"; the reverse direction of
/// a two-way street is tracked as a SEPARATE queue (its own capacity/
/// backlog), matching how a real road has independent congestion per
/// direction.</summary>
public readonly record struct LinkKey(long WayId, bool Forward);
