// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Mobility.Signals;

/// <summary>
/// Runs one two-approach signalized intersection tick by tick: arrivals
/// join each approach's queue every tick; at the start of each
/// <see cref="ISignalPlan.CycleTicks"/>-tick cycle the plan decides that
/// cycle's green split; whichever approach is green that tick discharges
/// up to <see cref="_dischargeRatePerGreenTick"/> vehicles. This is the
/// harness both <see cref="FixedTimeSignalPlan"/> and
/// <see cref="AdaptiveSignalPlan"/> are measured against -- see
/// SignalsTests for the fixed-vs-adaptive trade-off this produces.
/// </summary>
public sealed class SignalIntersectionSimulator
{
    private readonly ISignalPlan _plan;
    private readonly int _dischargeRatePerGreenTick;

    public long QueueA { get; private set; }
    public long QueueB { get; private set; }
    public int CurrentGreenTicksA { get; private set; }
    public int CurrentGreenTicksB { get; private set; }

    /// <summary>Sum, over every tick simulated so far, of that tick's
    /// queue length -- a standard discrete delay proxy (total
    /// vehicle-ticks spent waiting). Used to compare plans with a single
    /// number rather than eyeballing a queue-length time series.</summary>
    public long CumulativeQueueTicksA { get; private set; }
    public long CumulativeQueueTicksB { get; private set; }

    public long TicksSimulated { get; private set; }

    public SignalIntersectionSimulator(ISignalPlan plan, int dischargeRatePerGreenTick, long initialQueueA = 0, long initialQueueB = 0)
    {
        _plan = plan;
        _dischargeRatePerGreenTick = dischargeRatePerGreenTick;
        QueueA = initialQueueA;
        QueueB = initialQueueB;
        (CurrentGreenTicksA, CurrentGreenTicksB) = plan.AllocateGreenTicks(initialQueueA, initialQueueB);
    }

    public void Step(int arrivalsA, int arrivalsB)
    {
        if (arrivalsA < 0 || arrivalsB < 0)
        {
            throw new ArgumentOutOfRangeException(arrivalsA < 0 ? nameof(arrivalsA) : nameof(arrivalsB));
        }

        var offsetInCycle = TicksSimulated % _plan.CycleTicks;
        if (offsetInCycle == 0 && TicksSimulated > 0)
        {
            (CurrentGreenTicksA, CurrentGreenTicksB) = _plan.AllocateGreenTicks(QueueA, QueueB);
        }

        QueueA += arrivalsA;
        QueueB += arrivalsB;

        if (offsetInCycle < CurrentGreenTicksA)
        {
            QueueA -= Math.Min(QueueA, _dischargeRatePerGreenTick);
        }
        else
        {
            QueueB -= Math.Min(QueueB, _dischargeRatePerGreenTick);
        }

        CumulativeQueueTicksA += QueueA;
        CumulativeQueueTicksB += QueueB;
        TicksSimulated++;
    }

    public double AverageQueueA => TicksSimulated == 0 ? 0 : (double)CumulativeQueueTicksA / TicksSimulated;
    public double AverageQueueB => TicksSimulated == 0 ? 0 : (double)CumulativeQueueTicksB / TicksSimulated;

    /// <summary>Used only by save/load restore: overwrites every mutable
    /// field with an exact prior snapshot (see
    /// <see cref="Signals.SignalInstance"/>), so a restored simulator
    /// continues exactly where the save was taken rather than from a fresh
    /// plan.AllocateGreenTicks(0, 0) starting point.</summary>
    internal void LoadState(long queueA, long queueB, int currentGreenTicksA, int currentGreenTicksB, long cumulativeQueueTicksA, long cumulativeQueueTicksB, long ticksSimulated)
    {
        QueueA = queueA;
        QueueB = queueB;
        CurrentGreenTicksA = currentGreenTicksA;
        CurrentGreenTicksB = currentGreenTicksB;
        CumulativeQueueTicksA = cumulativeQueueTicksA;
        CumulativeQueueTicksB = cumulativeQueueTicksB;
        TicksSimulated = ticksSimulated;
    }
}
