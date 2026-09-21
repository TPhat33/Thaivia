// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Planning;

public enum ProjectKind
{
    BuildingRelocation,
    NewRoadConnector,

    /// <summary>A temporary capacity-reducing construction zone on an
    /// existing road_graph way (see
    /// Thaivia.Core.Simulation.Mobility.RoadWorks.RoadWorksZone). Added in
    /// G4's tick-loop integration wave so road works goes through the same
    /// reserve/commit/pay-milestone/cancel budget flow as every other
    /// project kind, instead of WorldState.AddRoadWorksZone's direct
    /// mutation (which remains available for tests/manual setup only --
    /// see its doc comment).</summary>
    RoadWorks,
}
