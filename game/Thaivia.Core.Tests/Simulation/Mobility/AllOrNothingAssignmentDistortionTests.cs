using Thaivia.Core.Simulation.Mobility.Demand;
using Thaivia.Core.Simulation.Mobility.Queues;
using Thaivia.Core.Simulation.Mobility.Routing;
using Xunit;

namespace Thaivia.Core.Tests.Simulation.Mobility;

/// <summary>
/// Answers the g5 supervisor brief's honesty question directly, with a
/// measured example rather than a hand-wave: does
/// <see cref="NetworkDemandAssignment.AssignToWaysAllOrNothing"/> (the
/// original ADR-0022 model, kept -- not deleted -- as ADR-0027's
/// documented "before" baseline) materially distort results when a
/// congestion-aware alternative exists?
///
/// YES, when a genuine alternate route exists: <see cref="TwoRouteAssignmentFixtures"/>
/// gives node A to node B two paths -- a SHORT one with LOW capacity, and a
/// LONGER one with HIGH (never-exhausted) capacity. Every real-world router
/// with any congestion awareness would eventually shift some demand onto
/// the under-used longer path once the short one backs up. All-or-nothing
/// assignment recomputes the SAME shortest (by free-flow distance,
/// ignoring current queue state) path every tick, so 100% of demand keeps
/// landing on the short/low-capacity way forever -- an unbounded queue
/// forms there while the alternate way sits at zero, even though total
/// network capacity would easily clear all demand if it were split. This
/// is a real, measurable distortion, not a rare edge case: it happens
/// wherever a real network has more than one route between two points and
/// one is even slightly shorter, which the pilot-scale fixture already
/// reproduces at 5 ways.
///
/// ADR-0027 replaces this model in <see cref="Thaivia.Core.Simulation.WorldState.SimulateTick"/>
/// with <see cref="NetworkDemandAssignment.AssignToWaysCongestionAware"/> --
/// see <see cref="CongestionAwareAssignmentTests"/> for the SAME fixture
/// proving the fix.
/// </summary>
public class AllOrNothingAssignmentDistortionTests
{
    [Fact]
    public void AllOrNothingAssignment_NeverShiftsDemandOntoAnIdleAlternateRoute_EvenAsTheShortRouteBacksUpWithoutBound()
    {
        var roadGraph = TwoRouteAssignmentFixtures.BuildTwoRouteGraph();
        var vehicleGraph = new MobilityGraph(roadGraph, TravelMode.Vehicle);
        var queues = new LinkQueueSimulator();
        var shortLink = new LinkKey(TwoRouteAssignmentFixtures.ShortLowCapacityWay, Forward: true);
        var detourLink1 = new LinkKey(TwoRouteAssignmentFixtures.LongHighCapacityWay1, Forward: true);
        var detourLink2 = new LinkKey(TwoRouteAssignmentFixtures.LongHighCapacityWay2, Forward: true);

        const long vehiclesPerTickDemand = 6; // comfortably more than the short route's 2/tick capacity, comfortably less than the alternate route's 20/tick.
        const int ticks = 200;

        for (var tick = 0; tick < ticks; tick++)
        {
            var batches = new[] { new OdBatch(TwoRouteAssignmentFixtures.NodeA, TwoRouteAssignmentFixtures.NodeB, TravelMode.Vehicle, vehiclesPerTickDemand, "distortion-test-cohort") };
            var arrivalsByWay = NetworkDemandAssignment.AssignToWaysAllOrNothing(vehicleGraph, batches);

            queues.Step(shortLink, arrivalsByWay.TryGetValue(TwoRouteAssignmentFixtures.ShortLowCapacityWay, out var sa) ? sa : 0, capacityVehPerTick: 2);
            queues.Step(detourLink1, arrivalsByWay.TryGetValue(TwoRouteAssignmentFixtures.LongHighCapacityWay1, out var da1) ? da1 : 0, capacityVehPerTick: 20);
            queues.Step(detourLink2, arrivalsByWay.TryGetValue(TwoRouteAssignmentFixtures.LongHighCapacityWay2, out var da2) ? da2 : 0, capacityVehPerTick: 20);
        }

        var shortQueue = queues.QueueLengthOf(shortLink);
        var detourArrived1 = queues.TotalArrived.TryGetValue(detourLink1, out var t1) ? t1 : 0;
        var detourArrived2 = queues.TotalArrived.TryGetValue(detourLink2, out var t2) ? t2 : 0;

        // The measured distortion: an unbounded, ever-growing backlog on
        // the short route (exactly (6-2)*200 = 800 -- integer-exact, no
        // rerouting ever kicked in)...
        Assert.Equal((vehiclesPerTickDemand - 2) * ticks, shortQueue);

        // ...while the alternate route received ZERO arrivals across all
        // 200 ticks, despite having 10x the spare capacity that would have
        // cleared 100% of demand with room to spare.
        Assert.Equal(0, detourArrived1);
        Assert.Equal(0, detourArrived2);
    }
}
