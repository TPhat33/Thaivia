// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Mobility.Signals;

/// <summary>
/// A persistable, identified signal at one road-graph node: config (which
/// <see cref="ISignalPlan"/>, its parameters) + the live
/// <see cref="SignalIntersectionSimulator"/> state. This is the type
/// <see cref="WorldState"/> owns a list of and what
/// <see cref="Save.SavedSignal"/> mirrors.
/// </summary>
public sealed class SignalInstance
{
    public SignalInstance(string id, long nodeId, SignalPlanKind kind, long cycleTicks, int configValue, int dischargeRatePerGreenTick)
    {
        Id = id;
        NodeId = nodeId;
        Kind = kind;
        CycleTicks = cycleTicks;
        ConfigValue = configValue;
        DischargeRatePerGreenTick = dischargeRatePerGreenTick;
        Simulator = new SignalIntersectionSimulator(BuildPlan(kind, cycleTicks, configValue), dischargeRatePerGreenTick);
    }

    /// <summary>Restore constructor -- used only by save/load.</summary>
    public SignalInstance(
        string id, long nodeId, SignalPlanKind kind, long cycleTicks, int configValue, int dischargeRatePerGreenTick,
        long queueA, long queueB, int currentGreenTicksA, int currentGreenTicksB, long cumulativeQueueTicksA, long cumulativeQueueTicksB, long ticksSimulated)
    {
        Id = id;
        NodeId = nodeId;
        Kind = kind;
        CycleTicks = cycleTicks;
        ConfigValue = configValue;
        DischargeRatePerGreenTick = dischargeRatePerGreenTick;
        Simulator = new SignalIntersectionSimulator(BuildPlan(kind, cycleTicks, configValue), dischargeRatePerGreenTick);
        Simulator.LoadState(queueA, queueB, currentGreenTicksA, currentGreenTicksB, cumulativeQueueTicksA, cumulativeQueueTicksB, ticksSimulated);
    }

    public string Id { get; }
    public long NodeId { get; }
    public SignalPlanKind Kind { get; }
    public long CycleTicks { get; }

    /// <summary>GreenTicksApproachA for <see cref="SignalPlanKind.Fixed"/>,
    /// MinGreenTicks for <see cref="SignalPlanKind.Adaptive"/>.</summary>
    public int ConfigValue { get; }

    public int DischargeRatePerGreenTick { get; }
    public SignalIntersectionSimulator Simulator { get; }

    public void Step(int arrivalsA, int arrivalsB) => Simulator.Step(arrivalsA, arrivalsB);

    private static ISignalPlan BuildPlan(SignalPlanKind kind, long cycleTicks, int configValue) => kind switch
    {
        SignalPlanKind.Fixed => new FixedTimeSignalPlan(cycleTicks, configValue),
        SignalPlanKind.Adaptive => new AdaptiveSignalPlan(cycleTicks, configValue),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
