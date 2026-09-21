// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Mobility.RoadWorks;

/// <summary>
/// A road-works period on one road_graph way (spec §9: "road works ต้อง
/// กระทบการเข้าถึงระหว่างสร้าง" -- access must degrade WHILE under
/// construction, not only once complete). Its only effect is
/// <see cref="Queues.LinkCapacity.EffectiveCapacityVehPerTick"/> reading
/// <see cref="CapacityMultiplierDuringConstruction"/> for ticks in
/// [<see cref="StartTick"/>, <see cref="StartTick"/> + <see cref="DurationTicks"/>);
/// before or after that window the way's full base capacity applies.
/// </summary>
public sealed class RoadWorksZone
{
    public RoadWorksZone(string id, long wayId, long startTick, long durationTicks, double capacityMultiplierDuringConstruction, string projectId)
    {
        Id = id;
        WayId = wayId;
        StartTick = startTick;
        DurationTicks = durationTicks;
        CapacityMultiplierDuringConstruction = capacityMultiplierDuringConstruction;
        ProjectId = projectId;
    }

    public string Id { get; }
    public long WayId { get; }
    public long StartTick { get; }
    public long DurationTicks { get; }

    /// <summary>In (0, 1]: how much of the way's base capacity remains
    /// usable while construction is active. Never 0 outright by
    /// convention here (a fully closed road is modelled as a very small
    /// but nonzero value, e.g. one lane held open for alternating
    /// traffic) -- callers that truly need a hard closure can still pass
    /// 0.0, which <see cref="Queues.LinkCapacity.EffectiveCapacityVehPerTick"/>
    /// handles by floor-clamping to zero capacity.</summary>
    public double CapacityMultiplierDuringConstruction { get; }

    public string ProjectId { get; }

    public bool IsActiveAt(long tick) => tick >= StartTick && tick < StartTick + DurationTicks;
}
