// Pure C# -- no UnityEngine reference. See game/README.md.
using Thaivia.Core.Simulation.Archetypes;

namespace Thaivia.Core.Simulation.Buildings;

/// <summary>
/// The simulation-layer state for one GeographyBase building, keyed by
/// that building's immutable <see cref="SourceId"/>. This type lives in
/// Thaivia.Core.Simulation, never Thaivia.Core.MapPack -- it references a
/// building by id only, and shares no base type or field with
/// <see cref="Thaivia.Core.MapPack.PolygonFeature"/> (AGENTS.md rule 3:
/// GeographyBase and simulation state are never mixed into one
/// structure). Relocation is modelled by replacing the dictionary entry
/// with a new, otherwise-identical instance from
/// <see cref="WithLocation"/> -- <see cref="SourceId"/> (the building's
/// stable identity) and <see cref="JobsCount"/> never change across a
/// move, which is what keeps population/jobs conserved automatically:
/// nothing that owns those counts is touched.
/// </summary>
public sealed class BuildingSimState
{
    public BuildingSimState(
        long sourceId,
        BuildingArchetype archetype,
        double localX,
        double localZ,
        long nearestRoadNodeId,
        int jobsCount,
        bool relocated)
    {
        SourceId = sourceId;
        Archetype = archetype;
        LocalX = localX;
        LocalZ = localZ;
        NearestRoadNodeId = nearestRoadNodeId;
        JobsCount = jobsCount;
        Relocated = relocated;
    }

    public long SourceId { get; }
    public BuildingArchetype Archetype { get; }
    public double LocalX { get; }
    public double LocalZ { get; }
    public long NearestRoadNodeId { get; }
    public int JobsCount { get; }
    public bool Relocated { get; }

    /// <summary>Produces the post-relocation state: new location + new
    /// nearest road node, everything else (identity, archetype, jobs)
    /// carried over unchanged -- "occupants/jobs transferred, not
    /// deleted" (plan §8) modelled as "the thing that owns them keeps
    /// existing under the same id", not as a separate transfer step that
    /// could be forgotten.</summary>
    public BuildingSimState WithLocation(double newLocalX, double newLocalZ, long newNearestRoadNodeId) =>
        new(SourceId, Archetype, newLocalX, newLocalZ, newNearestRoadNodeId, JobsCount, relocated: true);
}
