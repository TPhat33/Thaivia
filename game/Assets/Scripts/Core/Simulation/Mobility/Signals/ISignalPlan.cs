// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Mobility.Signals;

/// <summary>A traffic-signal timing plan for one two-approach intersection
/// (spec §9: "fixed signals ก่อน adaptive" -- both implement this one
/// interface so a caller can swap between them without changing anything
/// else about how the intersection is simulated).</summary>
public interface ISignalPlan
{
    long CycleTicks { get; }

    /// <summary>Decides how the next cycle's <see cref="CycleTicks"/> is
    /// split between approach A and approach B, given each approach's
    /// queue length AT THE START of that cycle. The two returned values
    /// always sum to <see cref="CycleTicks"/>.</summary>
    (int GreenTicksA, int GreenTicksB) AllocateGreenTicks(long queueA, long queueB);
}
