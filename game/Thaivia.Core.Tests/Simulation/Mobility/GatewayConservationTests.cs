using System;
using Thaivia.Core.Simulation.Mobility.Gateways;
using Xunit;
using Xunit.Abstractions;

namespace Thaivia.Core.Tests.Simulation.Mobility;

public class GatewayConservationTests
{
    private readonly ITestOutputHelper _output;

    public GatewayConservationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>The flagship test the supervising engineer said they would
    /// scrutinise hardest: tracks total trips generated/completed/queued
    /// across many ticks, including a gateway CLOSE and re-OPEN partway
    /// through, and asserts the books balance EXACTLY -- an integer ledger,
    /// not a tolerance. Two gateways are simulated together so closing one
    /// cannot be "compensated" by the other's numbers.</summary>
    [Fact]
    public void TotalTripsAcrossManyTicks_WithAGatewayCloseAndReopen_ConserveExactly()
    {
        var gatewayA = new GatewayFlow(gatewayNodeId: 1001, inboundCapacityPerTick: 3, outboundCapacityPerTick: 2);
        var gatewayB = new GatewayFlow(gatewayNodeId: 1002, inboundCapacityPerTick: 1, outboundCapacityPerTick: 4);
        var network = new GatewayNetwork(new[] { gatewayA, gatewayB });

        const long totalTicks = 500;
        for (long tick = 0; tick < totalTicks; tick++)
        {
            // Deterministic, varying demand pattern -- no RNG needed for
            // this structural proof, but the pattern is not constant
            // either (a constant pattern would under-exercise the queue
            // draining/backing-up paths).
            gatewayA.GenerateOutboundDemand(tick % 5);
            gatewayA.GenerateInboundDemand((tick + 2) % 4);
            gatewayB.GenerateOutboundDemand((tick * 3) % 6);
            gatewayB.GenerateInboundDemand(tick % 2);

            // Close gateway A for a whole window (demand keeps arriving
            // and queueing while closed), then reopen it.
            if (tick == 100)
            {
                gatewayA.Close();
            }

            if (tick == 220)
            {
                gatewayA.Open();
            }

            network.StepAll();

            // Assert the invariant holds after EVERY tick, not just at the
            // end -- a bug that only breaks conservation transiently would
            // otherwise slip through.
            Assert.True(gatewayA.OutboundLedger.IsConserved, $"gateway A outbound broke conservation at tick {tick}: {gatewayA.OutboundLedger}");
            Assert.True(gatewayA.InboundLedger.IsConserved, $"gateway A inbound broke conservation at tick {tick}: {gatewayA.InboundLedger}");
            Assert.True(gatewayB.OutboundLedger.IsConserved, $"gateway B outbound broke conservation at tick {tick}: {gatewayB.OutboundLedger}");
            Assert.True(gatewayB.InboundLedger.IsConserved, $"gateway B inbound broke conservation at tick {tick}: {gatewayB.InboundLedger}");
        }

        var totalOutbound = network.TotalOutboundLedger();
        var totalInbound = network.TotalInboundLedger();

        _output.WriteLine($"Outbound -- generated: {totalOutbound.Generated}, completed: {totalOutbound.Completed}, queued: {totalOutbound.Queued}");
        _output.WriteLine($"Inbound  -- generated: {totalInbound.Generated}, completed: {totalInbound.Completed}, queued: {totalInbound.Queued}");

        Assert.Equal(totalOutbound.Generated, totalOutbound.Completed + totalOutbound.Queued);
        Assert.Equal(totalInbound.Generated, totalInbound.Completed + totalInbound.Queued);

        // The close window must have actually produced a real backlog --
        // otherwise this test never exercised the "closing pushes
        // queueing back into the network" behavior at all.
        Assert.True(gatewayA.OutboundLedger.Queued > 0, "closing gateway A should have produced a real, nonzero backlog -- otherwise this test proves nothing about closed gateways.");
    }

    [Fact]
    public void ClosedGateway_NeverDischargesTrips_TheyOnlyQueueUp()
    {
        var gateway = new GatewayFlow(gatewayNodeId: 5, inboundCapacityPerTick: 10, outboundCapacityPerTick: 10, isOpen: false);
        gateway.GenerateOutboundDemand(7);

        for (var i = 0; i < 20; i++)
        {
            gateway.Step();
        }

        Assert.Equal(0, gateway.CompletedOutbound);
        Assert.Equal(7, gateway.PendingOutbound);
        Assert.Equal(7, gateway.GeneratedOutbound);
    }

    [Fact]
    public void ReopeningAGateway_DrainsThePreviouslyQueuedBacklog_WithoutLosingOrDuplicatingAnyOfIt()
    {
        var gateway = new GatewayFlow(gatewayNodeId: 5, inboundCapacityPerTick: 0, outboundCapacityPerTick: 3, isOpen: false);
        gateway.GenerateOutboundDemand(10);
        for (var i = 0; i < 5; i++)
        {
            gateway.Step(); // stays closed -- all 10 remain queued.
        }

        Assert.Equal(10, gateway.PendingOutbound);

        gateway.Open();
        for (var i = 0; i < 4; i++)
        {
            gateway.Step();
        }

        // 3/tick * 4 ticks = 12 capacity available, but only 10 were ever
        // generated -- completed must be exactly 10, not more (no
        // duplication) and pending must be exactly 0 (no loss).
        Assert.Equal(10, gateway.CompletedOutbound);
        Assert.Equal(0, gateway.PendingOutbound);
        Assert.Equal(10, gateway.GeneratedOutbound);
    }

    /// <summary>
    /// The control the supervising engineer explicitly asked for: a
    /// deliberately leaky gateway variant (test-only, mirrors GatewayFlow's
    /// public shape but silently drops queued demand on Close, i.e. it
    /// deletes demand instead of pushing it back into the network) must
    /// make the conservation assertion FAIL. This proves the assertion
    /// above actually bites a bug of exactly this shape, rather than being
    /// vacuously true.
    /// </summary>
    [Fact]
    public void LeakyGatewayVariant_ThatDropsQueuedDemandOnClose_ViolatesConservation_ProvingTheAboveTestBites()
    {
        var leaky = new LeakyGatewayFlow(outboundCapacityPerTick: 2);
        leaky.GenerateOutboundDemand(20);
        leaky.Step();
        leaky.Step();
        Assert.True(leaky.OutboundLedger.IsConserved); // still fine before the leak.

        leaky.Close(); // BUG: this variant drops PendingOutbound to 0 right here.
        for (var i = 0; i < 10; i++)
        {
            leaky.Step();
        }

        // Generated (20) != Completed (4) + Queued (0) == 4. The 16 lost
        // trips are exactly the bug class this whole namespace exists to
        // prevent -- and the leaky variant reproduces it on purpose so the
        // conservation assertion can be shown to actually catch it.
        Assert.False(leaky.OutboundLedger.IsConserved, "the leaky variant was expected to violate conservation -- if it didn't, the leak itself is not doing what this control claims.");
        Assert.Throws<Xunit.Sdk.EqualException>(() =>
            Assert.Equal(leaky.OutboundLedger.Generated, leaky.OutboundLedger.Completed + leaky.OutboundLedger.Queued));
    }

    /// <summary>Deliberately buggy stand-in for GatewayFlow, used ONLY by
    /// the control test above -- never referenced by production code.
    /// Reproduces exactly the "closing a gateway deletes demand instead of
    /// pushing it back into the network" bug class AGENTS.md/plan §10
    /// forbid.</summary>
    private sealed class LeakyGatewayFlow
    {
        private readonly int _outboundCapacityPerTick;
        private bool _isOpen = true;

        public LeakyGatewayFlow(int outboundCapacityPerTick)
        {
            _outboundCapacityPerTick = outboundCapacityPerTick;
        }

        public long GeneratedOutbound { get; private set; }
        public long CompletedOutbound { get; private set; }
        public long PendingOutbound { get; private set; }

        public TripLedgerSnapshot OutboundLedger => new(GeneratedOutbound, CompletedOutbound, PendingOutbound);

        public void GenerateOutboundDemand(long amount)
        {
            GeneratedOutbound += amount;
            PendingOutbound += amount;
        }

        /// <summary>THE BUG: a real Close() must never touch PendingOutbound.
        /// This one does -- it deletes the queued demand instead of leaving
        /// it to back up, exactly the failure mode the real GatewayFlow's
        /// doc comment says it structurally cannot have.</summary>
        public void Close()
        {
            _isOpen = false;
            PendingOutbound = 0; // <-- the leak: demand silently vanishes.
        }

        public void Step()
        {
            var capacity = _isOpen ? _outboundCapacityPerTick : 0;
            var departed = Math.Min(PendingOutbound, capacity);
            PendingOutbound -= departed;
            CompletedOutbound += departed;
        }
    }
}
