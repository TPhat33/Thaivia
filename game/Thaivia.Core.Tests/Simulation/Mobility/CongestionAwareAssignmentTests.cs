using System.Collections.Generic;
using Thaivia.Core.Simulation.Mobility.Demand;
using Thaivia.Core.Simulation.Mobility.Queues;
using Thaivia.Core.Simulation.Mobility.Routing;
using Xunit;
using Xunit.Abstractions;

namespace Thaivia.Core.Tests.Simulation.Mobility;

/// <summary>
/// ADR-0027's fix for the distortion <see cref="AllOrNothingAssignmentDistortionTests"/>
/// documents, proven on the exact same <see cref="TwoRouteAssignmentFixtures"/>
/// network so the two test classes can be compared directly.
/// </summary>
public class CongestionAwareAssignmentTests
{
    private readonly ITestOutputHelper _output;

    public CongestionAwareAssignmentTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly LinkKey ShortLink = new(TwoRouteAssignmentFixtures.ShortLowCapacityWay, Forward: true);
    private static readonly LinkKey DetourLink1 = new(TwoRouteAssignmentFixtures.LongHighCapacityWay1, Forward: true);
    private static readonly LinkKey DetourLink2 = new(TwoRouteAssignmentFixtures.LongHighCapacityWay2, Forward: true);

    /// <summary>The companion the supervisor's brief explicitly asked for:
    /// with the SAME demand (6 veh/tick, comfortably over the short
    /// route's 2/tick capacity, comfortably under the detour's 20/tick)
    /// that made <see cref="AllOrNothingAssignmentDistortionTests"/> route
    /// 100% onto the short way and 0% onto the detour for all 200 ticks,
    /// congestion-aware assignment must divert a REAL, measured share onto
    /// the detour, and the short route's backlog must stop growing
    /// without bound (it stabilizes once the split matches each way's
    /// capacity) rather than reaching the old test's exact 800.</summary>
    [Fact]
    public void CongestionAwareAssignment_DivertsAMeaningfulShareOfDemandOntoTheHighCapacityAlternative()
    {
        var roadGraph = TwoRouteAssignmentFixtures.BuildTwoRouteGraph();
        var vehicleGraph = new MobilityGraph(roadGraph, TravelMode.Vehicle);
        var queues = new LinkQueueSimulator();
        var capacityByWayId = TwoRouteAssignmentFixtures.CapacityByWayId();

        const long vehiclesPerTickDemand = 6;
        const int ticks = 200;

        for (var tick = 0; tick < ticks; tick++)
        {
            var priorQueueLengthByWay = new Dictionary<long, long>
            {
                [TwoRouteAssignmentFixtures.ShortLowCapacityWay] = queues.QueueLengthOf(ShortLink),
                [TwoRouteAssignmentFixtures.LongHighCapacityWay1] = queues.QueueLengthOf(DetourLink1),
                [TwoRouteAssignmentFixtures.LongHighCapacityWay2] = queues.QueueLengthOf(DetourLink2),
            };

            var batches = new[] { new OdBatch(TwoRouteAssignmentFixtures.NodeA, TwoRouteAssignmentFixtures.NodeB, TravelMode.Vehicle, vehiclesPerTickDemand, "congestion-aware-test-cohort") };
            var arrivalsByWay = NetworkDemandAssignment.AssignToWaysCongestionAware(vehicleGraph, batches, capacityByWayId, priorQueueLengthByWay);

            queues.Step(ShortLink, arrivalsByWay.TryGetValue(TwoRouteAssignmentFixtures.ShortLowCapacityWay, out var sa) ? sa : 0, capacityVehPerTick: 2);
            queues.Step(DetourLink1, arrivalsByWay.TryGetValue(TwoRouteAssignmentFixtures.LongHighCapacityWay1, out var da1) ? da1 : 0, capacityVehPerTick: 20);
            queues.Step(DetourLink2, arrivalsByWay.TryGetValue(TwoRouteAssignmentFixtures.LongHighCapacityWay2, out var da2) ? da2 : 0, capacityVehPerTick: 20);
        }

        var shortQueue = queues.QueueLengthOf(ShortLink);
        var shortArrived = queues.TotalArrived.TryGetValue(ShortLink, out var s) ? s : 0;
        var detourArrived1 = queues.TotalArrived.TryGetValue(DetourLink1, out var t1) ? t1 : 0;
        var detourArrived2 = queues.TotalArrived.TryGetValue(DetourLink2, out var t2) ? t2 : 0;
        var totalDemand = vehiclesPerTickDemand * ticks;

        _output.WriteLine($"Total demand over {ticks} ticks: {totalDemand}");
        _output.WriteLine($"Short/low-capacity way: {shortArrived} arrivals, ending backlog {shortQueue}");
        _output.WriteLine($"Detour way 1: {detourArrived1} arrivals; detour way 2: {detourArrived2} arrivals");
        _output.WriteLine($"Detour share of total demand: {(double)detourArrived1 / totalDemand:P1}");

        // Conservation: every vehicle generated was assigned to exactly one
        // route (the short way, counted once; the detour, counted once per
        // of its two ways since a route accumulates onto EVERY way it
        // crosses -- see NetworkDemandAssignment's per-way accumulation) --
        // never lost, never invented. detourArrived1 and detourArrived2
        // must be equal (same route, same vehicles, two ways).
        Assert.Equal(detourArrived1, detourArrived2);
        Assert.Equal(totalDemand, shortArrived + detourArrived1);

        // The actual fix: the detour, which received EXACTLY ZERO
        // arrivals under all-or-nothing, must now carry a real, meaningful
        // share of demand -- not a token trickle.
        Assert.True(detourArrived1 > totalDemand / 4, $"expected the detour to carry a meaningful share (>25%) of total demand, got {detourArrived1}/{totalDemand}.");

        // And the short route's backlog must stay bounded/stabilized, not
        // reach anywhere near the all-or-nothing case's exact 800 (a queue
        // that grew by the full (6-2) excess on every one of 200 ticks).
        Assert.True(shortQueue < 100, $"expected the short route's backlog to stabilize far below the all-or-nothing case's 800, got {shortQueue}.");
    }

    /// <summary>Determinism under repeated, independent runs of the exact
    /// same scenario -- the iterative/successive-slice assignment must not
    /// introduce any run-to-run drift despite using doubles internally for
    /// its congestion cost (see NetworkDemandAssignment's class doc
    /// comment on why: the doubles only ever influence which INTEGER
    /// route gets chosen, and are never themselves persisted/hashed).
    /// See also <see cref="IntegratedTickLoopTests.TwoWorlds_SameSeedSameIntegratedTickRun_ProduceIdenticalStructuralHash"/>
    /// for the same property proven at the full WorldState/tick-loop
    /// level.</summary>
    [Fact]
    public void CongestionAwareAssignment_IsFullyDeterministic_AcrossIndependentRunsOfTheSameScenario()
    {
        IReadOnlyDictionary<LinkKey, long> RunScenario()
        {
            var roadGraph = TwoRouteAssignmentFixtures.BuildTwoRouteGraph();
            var vehicleGraph = new MobilityGraph(roadGraph, TravelMode.Vehicle);
            var queues = new LinkQueueSimulator();
            var capacityByWayId = TwoRouteAssignmentFixtures.CapacityByWayId();

            for (var tick = 0; tick < 200; tick++)
            {
                var priorQueueLengthByWay = new Dictionary<long, long>
                {
                    [TwoRouteAssignmentFixtures.ShortLowCapacityWay] = queues.QueueLengthOf(ShortLink),
                    [TwoRouteAssignmentFixtures.LongHighCapacityWay1] = queues.QueueLengthOf(DetourLink1),
                    [TwoRouteAssignmentFixtures.LongHighCapacityWay2] = queues.QueueLengthOf(DetourLink2),
                };

                var batches = new[] { new OdBatch(TwoRouteAssignmentFixtures.NodeA, TwoRouteAssignmentFixtures.NodeB, TravelMode.Vehicle, 6, "determinism-cohort") };
                var arrivalsByWay = NetworkDemandAssignment.AssignToWaysCongestionAware(vehicleGraph, batches, capacityByWayId, priorQueueLengthByWay);

                queues.Step(ShortLink, arrivalsByWay.TryGetValue(TwoRouteAssignmentFixtures.ShortLowCapacityWay, out var sa) ? sa : 0, 2);
                queues.Step(DetourLink1, arrivalsByWay.TryGetValue(TwoRouteAssignmentFixtures.LongHighCapacityWay1, out var da1) ? da1 : 0, 20);
                queues.Step(DetourLink2, arrivalsByWay.TryGetValue(TwoRouteAssignmentFixtures.LongHighCapacityWay2, out var da2) ? da2 : 0, 20);
            }

            return queues.TotalArrived;
        }

        var runA = RunScenario();
        var runB = RunScenario();

        Assert.Equal(runA.Count, runB.Count);
        foreach (var (key, value) in runA)
        {
            Assert.True(runB.TryGetValue(key, out var otherValue));
            Assert.Equal(value, otherValue);
        }
    }
}
