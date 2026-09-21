// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Mobility.Crossings;

/// <summary>
/// A player-committed pedestrian crossing between two existing road-graph
/// nodes (spec §9: "ทางข้ามพร้อม accessible-path flag ตามแบบจำลอง"). Lives
/// in Thaivia.Core.Simulation.Mobility, never Thaivia.Core.MapPack -- same
/// discipline as <see cref="Accessibility.PlannedRoadSegment"/>: it is
/// combined with the base graph at query time by
/// <see cref="Routing.MobilityGraph"/>, never merged into the loaded
/// MapPack's RoadGraph.
///
/// <see cref="AccessibleFlag"/> is not decorative: a walking route computed
/// for a traveller who NEEDS an accessible path (a documented mobility
/// need, e.g. a wheelchair user or someone with a pushcart) only uses this
/// crossing when the flag is true. A general walking route (no such need)
/// may use ANY built crossing regardless of the flag. See
/// <see cref="Routing.MobilityGraph"/>'s `requireAccessibleCrossings`
/// parameter and CrossingsTests for the test that proves the flag actually
/// changes which routes are computed, not just what is displayed.
/// </summary>
public readonly record struct PedestrianCrossing(string Id, long NodeAId, long NodeBId, double LengthMeters, bool AccessibleFlag);
