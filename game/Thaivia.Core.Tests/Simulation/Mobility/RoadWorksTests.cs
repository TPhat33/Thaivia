using System.Collections.Generic;
using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation.Mobility.Queues;
using Thaivia.Core.Simulation.Mobility.RoadWorks;
using Xunit;

namespace Thaivia.Core.Tests.Simulation.Mobility;

public class RoadWorksTests
{
    private static RoadEdge MakeEdgeWithLanes(long wayId, int lanes)
    {
        var tags = new SourceTags(new Dictionary<string, string> { ["lanes"] = lanes.ToString() });
        var nodeRefs = new List<long> { 1, 2 };
        var coords = new List<Vec2> { new(0, 0), new(1, 0) };
        return new RoadEdge(wayId, tags, new List<AssumptionRecord>(), new List<AssumptionRecord>(), nodeRefs, coords, coords,
            OnewayDirection.No, layer: 0, bridge: false, tunnel: false, gradeSeparated: false,
            accessModes: new Dictionary<string, string>(), fromNode: 1, toNode: 2);
    }

    [Fact]
    public void LanesTag_DerivesBaseCapacity_NotAHandTunedConstant()
    {
        var oneLane = MakeEdgeWithLanes(1, 1);
        var threeLanes = MakeEdgeWithLanes(2, 3);

        Assert.Equal(LinkCapacity.VehPerTickPerLane, LinkCapacity.BaseCapacityVehPerTick(oneLane));
        Assert.Equal(3 * LinkCapacity.VehPerTickPerLane, LinkCapacity.BaseCapacityVehPerTick(threeLanes));
    }

    /// <summary>The test the supervising engineer asked for by name: road
    /// works must degrade access WHILE under construction, not only once
    /// finished. Ticks strictly before/after the works window get full
    /// capacity; ticks inside it get the reduced capacity -- and the queue
    /// backlog this produces is measurably worse during construction.</summary>
    [Fact]
    public void RoadWorksZone_DegradesCapacity_OnlyWhileActive_NotBeforeOrAfter()
    {
        var edge = MakeEdgeWithLanes(42, lanes: 2); // base capacity = 4 veh/tick.
        var zone = new RoadWorksZone("works-1", wayId: 42, startTick: 100, durationTicks: 50, capacityMultiplierDuringConstruction: 0.25, projectId: "proj-1");
        var zones = new List<RoadWorksZone> { zone };

        var before = LinkCapacity.EffectiveCapacityVehPerTick(edge, tick: 99, zones);
        var duringStart = LinkCapacity.EffectiveCapacityVehPerTick(edge, tick: 100, zones);
        var duringMiddle = LinkCapacity.EffectiveCapacityVehPerTick(edge, tick: 125, zones);
        var duringLastTick = LinkCapacity.EffectiveCapacityVehPerTick(edge, tick: 149, zones);
        var after = LinkCapacity.EffectiveCapacityVehPerTick(edge, tick: 150, zones);

        Assert.Equal(4, before);
        Assert.Equal(1, duringStart); // floor(4 * 0.25) = 1.
        Assert.Equal(1, duringMiddle);
        Assert.Equal(1, duringLastTick);
        Assert.Equal(4, after); // construction window is a HALF-open [start, start+duration).
    }

    [Fact]
    public void RoadWorks_MeasurablyWorsensQueueingDuringConstruction_ComparedToBeforeAndAfter()
    {
        var edge = MakeEdgeWithLanes(42, lanes: 2); // base capacity = 4 veh/tick.
        var zone = new RoadWorksZone("works-1", wayId: 42, startTick: 10, durationTicks: 10, capacityMultiplierDuringConstruction: 0.5, projectId: "proj-1");
        var zones = new List<RoadWorksZone> { zone };
        var sim = new LinkQueueSimulator();
        var link = new LinkKey(42, Forward: true);

        const int arrivalsPerTick = 3; // below full capacity (4), above degraded capacity (2).

        // Before construction: capacity 4 >= arrivals 3, so the queue never grows.
        for (long tick = 0; tick < 10; tick++)
        {
            sim.Step(link, arrivalsPerTick, LinkCapacity.EffectiveCapacityVehPerTick(edge, tick, zones));
        }

        Assert.Equal(0, sim.QueueLengthOf(link));

        // During construction: capacity drops to 2 < arrivals 3, so the queue backs up by 1/tick.
        for (long tick = 10; tick < 20; tick++)
        {
            sim.Step(link, arrivalsPerTick, LinkCapacity.EffectiveCapacityVehPerTick(edge, tick, zones));
        }

        var queueAtEndOfConstruction = sim.QueueLengthOf(link);
        Assert.Equal(10, queueAtEndOfConstruction); // exactly (3-2) * 10 ticks.
        Assert.True(queueAtEndOfConstruction > 0, "road works must measurably degrade access while active.");

        // After construction: capacity is back to 4 >= arrivals 3, so the backlog drains again.
        for (long tick = 20; tick < 30; tick++)
        {
            sim.Step(link, arrivalsPerTick, LinkCapacity.EffectiveCapacityVehPerTick(edge, tick, zones));
        }

        Assert.Equal(0, sim.QueueLengthOf(link)); // fully recovered once the works window ends.
    }
}
