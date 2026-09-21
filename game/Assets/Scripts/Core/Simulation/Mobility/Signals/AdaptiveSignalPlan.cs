// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Mobility.Signals;

/// <summary>
/// A demand-responsive signal plan (spec §9: "...adaptive" -- the second
/// half, implemented after <see cref="FixedTimeSignalPlan"/> per the
/// plan's explicit ordering). Each cycle's green split is proportional to
/// the two approaches' queue lengths at the start of that cycle, clamped
/// to [<see cref="MinGreenTicks"/>, CycleTicks - MinGreenTicks] so neither
/// approach is ever starved to zero green even under extreme imbalance.
/// This makes it MEASURABLY different from a fixed split whenever demand
/// is asymmetric (see SignalsTests for the proof, and ADR-0018 for the
/// trade-off this reallocation produces: it reduces the busier approach's
/// delay at the direct expense of the lighter approach's delay).
/// </summary>
public sealed class AdaptiveSignalPlan : ISignalPlan
{
    public AdaptiveSignalPlan(long cycleTicks, int minGreenTicks)
    {
        if (cycleTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cycleTicks));
        }

        if (minGreenTicks < 0 || minGreenTicks * 2 > cycleTicks)
        {
            throw new ArgumentOutOfRangeException(nameof(minGreenTicks), "minGreenTicks must leave room for both approaches within one cycle.");
        }

        CycleTicks = cycleTicks;
        MinGreenTicks = minGreenTicks;
    }

    public long CycleTicks { get; }
    public int MinGreenTicks { get; }

    public (int GreenTicksA, int GreenTicksB) AllocateGreenTicks(long queueA, long queueB)
    {
        var total = queueA + queueB;
        var maxGreenA = (int)CycleTicks - MinGreenTicks;

        int greenA;
        if (total <= 0)
        {
            // No signal to react to yet -- fall back to an even split,
            // same starting point a fixed 50/50 plan would use.
            greenA = (int)(CycleTicks / 2);
        }
        else
        {
            var share = (double)queueA / total;
            greenA = (int)Math.Round(share * CycleTicks, MidpointRounding.AwayFromZero);
        }

        greenA = Math.Clamp(greenA, MinGreenTicks, maxGreenA);
        var greenB = (int)(CycleTicks - greenA);
        return (greenA, greenB);
    }
}
