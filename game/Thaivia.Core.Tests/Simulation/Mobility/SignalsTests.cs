using Thaivia.Core.Simulation.Mobility.Signals;
using Xunit;
using Xunit.Abstractions;

namespace Thaivia.Core.Tests.Simulation.Mobility;

public class SignalsTests
{
    private readonly ITestOutputHelper _output;

    public SignalsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void FixedTimeSignalPlan_NeverReactsToQueueLength()
    {
        var plan = new FixedTimeSignalPlan(cycleTicks: 20, greenTicksApproachA: 10);
        Assert.Equal((10, 10), plan.AllocateGreenTicks(queueA: 0, queueB: 0));
        Assert.Equal((10, 10), plan.AllocateGreenTicks(queueA: 1000, queueB: 0));
        Assert.Equal((10, 10), plan.AllocateGreenTicks(queueA: 0, queueB: 1000));
    }

    [Fact]
    public void AdaptiveSignalPlan_ShiftsGreenTowardTheBusierApproach_BoundedByMinGreen()
    {
        var plan = new AdaptiveSignalPlan(cycleTicks: 20, minGreenTicks: 4);

        Assert.Equal((10, 10), plan.AllocateGreenTicks(queueA: 0, queueB: 0)); // no signal yet -> even split.

        var (greenA, greenB) = plan.AllocateGreenTicks(queueA: 90, queueB: 10);
        Assert.True(greenA > 10, "a 9:1 queue imbalance must shift more green to the busier approach than a fixed 50/50 split would.");
        Assert.Equal(20, greenA + greenB);

        // Even total starvation of B is bounded -- never below MinGreenTicks.
        var (extremeGreenA, extremeGreenB) = plan.AllocateGreenTicks(queueA: 10_000, queueB: 0);
        Assert.Equal(4, extremeGreenB);
        Assert.Equal(16, extremeGreenA);
    }

    /// <summary>The flagship test the supervising engineer asked for by
    /// name: adaptive must be demonstrably different from fixed, not a
    /// renamed copy, AND the difference must be a genuine trade-off --
    /// measured, real numbers, one metric improved and the other
    /// measurably worsened by the same lever. See ADR-0018 for the honest
    /// framing of this result.</summary>
    [Fact]
    public void AdaptiveSignal_ImprovesTheBusyApproach_ButMeasurablyWorsensTheLightApproach_ComparedToFixed()
    {
        const int cycleTicks = 20;
        const int minGreenTicks = 4;
        const int dischargeRatePerGreenTick = 4;
        const int arrivalsA = 4; // the busy approach.
        const int arrivalsB = 3; // the lighter approach.
        const int ticksToRun = 4000; // 200 cycles -- long enough for the reallocation effect to dominate any startup transient.

        var fixedSim = new SignalIntersectionSimulator(new FixedTimeSignalPlan(cycleTicks, greenTicksApproachA: 10), dischargeRatePerGreenTick);
        var adaptiveSim = new SignalIntersectionSimulator(new AdaptiveSignalPlan(cycleTicks, minGreenTicks), dischargeRatePerGreenTick);

        for (var tick = 0; tick < ticksToRun; tick++)
        {
            fixedSim.Step(arrivalsA, arrivalsB);
            adaptiveSim.Step(arrivalsA, arrivalsB);
        }

        _output.WriteLine($"Fixed    -- avg queue A: {fixedSim.AverageQueueA:F2}, avg queue B: {fixedSim.AverageQueueB:F2}, final queue A: {fixedSim.QueueA}, final queue B: {fixedSim.QueueB}");
        _output.WriteLine($"Adaptive -- avg queue A: {adaptiveSim.AverageQueueA:F2}, avg queue B: {adaptiveSim.AverageQueueB:F2}, final queue A: {adaptiveSim.QueueA}, final queue B: {adaptiveSim.QueueB}");
        _output.WriteLine($"Adaptive final green split -- A: {adaptiveSim.CurrentGreenTicksA}, B: {adaptiveSim.CurrentGreenTicksB} (fixed split is always 10/10)");

        // Structural difference, not just outcome: adaptive's green split
        // must actually diverge from the fixed 50/50 split at some point.
        Assert.NotEqual(10, adaptiveSim.CurrentGreenTicksA);

        // The trade-off, measured: approach A (busy) does BETTER under
        // adaptive than under fixed...
        Assert.True(adaptiveSim.AverageQueueA < fixedSim.AverageQueueA,
            $"expected adaptive to improve the busy approach's average queue ({adaptiveSim.AverageQueueA:F2}) below fixed's ({fixedSim.AverageQueueA:F2}).");

        // ...and approach B (light) does WORSE under adaptive than under
        // fixed -- the actual cost of the reallocation, not a made-up
        // number.
        Assert.True(adaptiveSim.AverageQueueB > fixedSim.AverageQueueB,
            $"expected adaptive to worsen the light approach's average queue ({adaptiveSim.AverageQueueB:F2}) above fixed's ({fixedSim.AverageQueueB:F2}).");
    }
}
