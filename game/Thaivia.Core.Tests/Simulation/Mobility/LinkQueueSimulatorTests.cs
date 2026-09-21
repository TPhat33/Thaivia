using Thaivia.Core.Simulation.Mobility.Queues;
using Xunit;

namespace Thaivia.Core.Tests.Simulation.Mobility;

public class LinkQueueSimulatorTests
{
    private static readonly LinkKey Link = new(WayId: 100, Forward: true);

    [Fact]
    public void ArrivalsWithinCapacity_NeverBuildsAQueue()
    {
        var sim = new LinkQueueSimulator();
        for (var tick = 0; tick < 20; tick++)
        {
            sim.Step(Link, arrivals: 3, capacityVehPerTick: 5);
        }

        Assert.Equal(0, sim.QueueLengthOf(Link));
        Assert.Equal(20L * 3, sim.TotalCompleted[Link]);
    }

    /// <summary>The test the supervising engineer named explicitly: proves
    /// congestion is a direct, exact arithmetic consequence of capacity,
    /// not a separately tuned score. Sustained arrivals of 7/tick against
    /// a capacity of 5/tick must grow the backlog by EXACTLY 2 per tick --
    /// no fudge factor, no "traffic score" anywhere in the computation.</summary>
    [Fact]
    public void SustainedArrivalsExceedingCapacity_GrowsTheQueueByExactlyTheShortfallPerTick()
    {
        var sim = new LinkQueueSimulator();
        const int arrivalsPerTick = 7;
        const int capacity = 5;

        for (var tick = 1; tick <= 10; tick++)
        {
            sim.Step(Link, arrivalsPerTick, capacity);
            var expectedQueue = (arrivalsPerTick - capacity) * tick;
            Assert.Equal(expectedQueue, sim.QueueLengthOf(Link));
        }

        Assert.Equal(10 * capacity, sim.TotalCompleted[Link]);
        Assert.Equal(10 * arrivalsPerTick, sim.TotalArrived[Link]);
        // Exact conservation at the link level too: arrived == completed + still-queued.
        Assert.Equal(sim.TotalArrived[Link], sim.TotalCompleted[Link] + sim.QueueLengthOf(Link));
    }

    [Fact]
    public void WhenDemandDropsBelowCapacityAgain_TheBacklogDrainsAtExactlyTheSpareCapacityRate()
    {
        var sim = new LinkQueueSimulator();
        // Build a backlog of 20 over 10 ticks (7 in, 5 out => +2/tick).
        for (var tick = 0; tick < 10; tick++)
        {
            sim.Step(Link, arrivals: 7, capacityVehPerTick: 5);
        }

        Assert.Equal(20, sim.QueueLengthOf(Link));

        // Demand now drops to 1/tick; capacity stays 5/tick => drains by 4/tick.
        sim.Step(Link, arrivals: 1, capacityVehPerTick: 5);
        Assert.Equal(16, sim.QueueLengthOf(Link));
        sim.Step(Link, arrivals: 1, capacityVehPerTick: 5);
        Assert.Equal(12, sim.QueueLengthOf(Link));
    }

    [Fact]
    public void TwoLinksAreIndependent_ArrivalsOnOneNeverAffectTheOthersQueue()
    {
        var sim = new LinkQueueSimulator();
        var linkA = new LinkKey(1, true);
        var linkB = new LinkKey(2, true);

        sim.Step(linkA, arrivals: 10, capacityVehPerTick: 1);
        Assert.Equal(9, sim.QueueLengthOf(linkA));
        Assert.Equal(0, sim.QueueLengthOf(linkB));
    }

    [Fact]
    public void RestoredSimulator_ContinuesFromExactlyTheCapturedState()
    {
        var sim = new LinkQueueSimulator();
        sim.Step(Link, arrivals: 9, capacityVehPerTick: 4);
        sim.Step(Link, arrivals: 9, capacityVehPerTick: 4);

        var restored = LinkQueueSimulator.Restore(sim.CaptureQueueLengths(), sim.CaptureTotalCompleted(), sim.CaptureTotalArrived());
        Assert.Equal(sim.QueueLengthOf(Link), restored.QueueLengthOf(Link));
        Assert.Equal(sim.TotalCompleted[Link], restored.TotalCompleted[Link]);
        Assert.Equal(sim.TotalArrived[Link], restored.TotalArrived[Link]);

        // And continuing to step both in lockstep keeps producing identical results.
        var a = sim.Step(Link, 3, 4);
        var b = restored.Step(Link, 3, 4);
        Assert.Equal(a, b);
        Assert.Equal(sim.QueueLengthOf(Link), restored.QueueLengthOf(Link));
    }
}
