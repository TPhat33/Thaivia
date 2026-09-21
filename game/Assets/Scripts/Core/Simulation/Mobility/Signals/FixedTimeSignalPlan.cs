// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Mobility.Signals;

/// <summary>A constant-split signal plan (spec §9: "fixed signals ก่อน
/// adaptive" -- this is the "fixed" half, and deliberately implemented and
/// tested first, per the plan). The split never reacts to queue length --
/// <see cref="AllocateGreenTicks"/> ignores both parameters on
/// purpose.</summary>
public sealed class FixedTimeSignalPlan : ISignalPlan
{
    public FixedTimeSignalPlan(long cycleTicks, int greenTicksApproachA)
    {
        if (cycleTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cycleTicks));
        }

        if (greenTicksApproachA < 0 || greenTicksApproachA > cycleTicks)
        {
            throw new ArgumentOutOfRangeException(nameof(greenTicksApproachA));
        }

        CycleTicks = cycleTicks;
        GreenTicksApproachA = greenTicksApproachA;
    }

    public long CycleTicks { get; }
    public int GreenTicksApproachA { get; }

    public (int GreenTicksA, int GreenTicksB) AllocateGreenTicks(long queueA, long queueB) =>
        (GreenTicksApproachA, (int)(CycleTicks - GreenTicksApproachA));
}
