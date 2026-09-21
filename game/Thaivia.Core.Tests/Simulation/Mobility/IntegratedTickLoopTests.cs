using System;
using System.Linq;
using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.Mobility.Incidents;
using Thaivia.Core.Simulation.Mobility.Queues;
using Thaivia.Core.Simulation.Planning;
using Xunit;
using Xunit.Abstractions;

namespace Thaivia.Core.Tests.Simulation.Mobility;

/// <summary>
/// Task Zero (g5 wave): WorldState.SimulateTick used to be a stub (clock
/// advance + one Traffic-stream draw, see git history) with every G4
/// primitive implemented but never called automatically. These tests
/// prove the REAL per-tick sequence documented on SimulateTick's doc
/// comment actually runs, end to end, on IntegratedTickFixtures'
/// connected network -- not just that each primitive works in isolation
/// (that was already proven by the other Mobility/*Tests.cs files).
/// </summary>
public class IntegratedTickLoopTests
{
    private readonly ITestOutputHelper _output;

    public IntegratedTickLoopTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>The most basic "is this actually live" proof: run the loop
    /// long enough to cross several hours-of-day (so Residential trip
    /// generation is nonzero for at least some ticks) and assert real
    /// traffic actually appeared on the bottleneck way -- not merely that
    /// nothing crashed.</summary>
    [Fact]
    public void SimulateTick_ProducesRealTrafficOnTheBottleneckWay_NotJustAClockAdvance()
    {
        var world = IntegratedTickFixtures.BuildFullyPopulatedWorldState();
        const int ticksPerHour = 5 * 3600;

        // Six in-game hours is enough to guarantee at least one hour where
        // Residential's activity clock (peak at hour 20) is comfortably
        // above baseline, so demand is not a fluke of hour 0.
        for (var i = 0; i < ticksPerHour * 6; i++)
        {
            world.SimulateTick();
        }

        var bottleneck = new LinkKey(IntegratedTickFixtures.Way1002, Forward: true);
        var totalArrived = world.LinkQueues.TotalArrived.TryGetValue(bottleneck, out var a) ? a : 0;
        _output.WriteLine($"Total arrivals on bottleneck way {IntegratedTickFixtures.Way1002} over {ticksPerHour * 6} ticks: {totalArrived}");

        Assert.True(totalArrived > 0, "the bottleneck way between the residential cohort and the office never saw any real assigned demand -- the loop is not actually generating/assigning traffic.");
    }

    /// <summary>Determinism must survive integration: two independently
    /// constructed worlds, same seed, fed the exact same long tick run PLUS
    /// a mid-run committed project, must hash identically. This is the
    /// same discipline as WorldStateDeterminismTests, but now with the
    /// full live G4 loop running (demand/assignment/queues/signals/buses/
    /// gateways/incidents), which is exactly the part that was previously
    /// dead code and therefore untested for determinism under real use.</summary>
    [Fact]
    public void TwoWorlds_SameSeedSameIntegratedTickRun_ProduceIdenticalStructuralHash()
    {
        var worldA = IntegratedTickFixtures.BuildFullyPopulatedWorldState(masterSeed: 2024);
        var worldB = IntegratedTickFixtures.BuildFullyPopulatedWorldState(masterSeed: 2024);

        RunIntegratedScenario(worldA);
        RunIntegratedScenario(worldB);

        Assert.Equal(worldA.ComputeStructuralHash(), worldB.ComputeStructuralHash());

        // A control the supervising engineer's brief specifically asks
        // for: a DIFFERENT seed must diverge (otherwise this hash could be
        // trivially satisfied by a function that ignores its inputs).
        var worldC = IntegratedTickFixtures.BuildFullyPopulatedWorldState(masterSeed: 999999);
        RunIntegratedScenario(worldC);
        Assert.NotEqual(worldA.ComputeStructuralHash(), worldC.ComputeStructuralHash());
    }

    private static void RunIntegratedScenario(Thaivia.Core.Simulation.WorldState world)
    {
        const int ticksPerHour = 5 * 3600;
        for (var i = 0; i < ticksPerHour * 3; i++)
        {
            world.SimulateTick();
        }

        var draft = new RoadWorksDraft(
            "det-roadworks", world.Revision, 50_000,
            PlanningEngine.EstimateRoadWorksCapacityImpact(world, IntegratedTickFixtures.Way1002, 0.5),
            IntegratedTickFixtures.Way1002, durationTicks: 500, capacityMultiplierDuringConstruction: 0.5);
        PlanningEngine.CommitRoadWorks(world, draft, LedgerAccountKind.NonRecurring, new long[] { 50_000 });

        for (var i = 0; i < ticksPerHour * 2; i++)
        {
            world.SimulateTick();
        }
    }

    /// <summary>Gateway conservation must still hold with the FULL loop
    /// running (not just the isolated GatewayFlow/GatewayNetwork unit
    /// test) -- thousands of ticks, with the gateway closed and reopened
    /// mid-run, while demand/queues/signals/incidents are all live at the
    /// same time.</summary>
    [Fact]
    public void GatewayConservation_HoldsAcrossThousandsOfTicks_WithTheFullLoopRunning_AndACloseReopenMidRun()
    {
        var world = IntegratedTickFixtures.BuildFullyPopulatedWorldState();
        var gateway = world.GatewayFlows[IntegratedTickFixtures.N40];
        const long totalTicks = 5000;

        for (long tick = 0; tick < totalTicks; tick++)
        {
            if (tick == 1500)
            {
                gateway.Close();
            }

            if (tick == 3200)
            {
                gateway.Open();
            }

            world.SimulateTick();

            Assert.True(gateway.OutboundLedger.IsConserved, $"outbound conservation broke at tick {tick}: {gateway.OutboundLedger}");
            Assert.True(gateway.InboundLedger.IsConserved, $"inbound conservation broke at tick {tick}: {gateway.InboundLedger}");
        }

        _output.WriteLine($"Outbound ledger after {totalTicks} integrated ticks: {gateway.OutboundLedger}");
        _output.WriteLine($"Inbound ledger after {totalTicks} integrated ticks: {gateway.InboundLedger}");

        Assert.Equal(gateway.OutboundLedger.Generated, gateway.OutboundLedger.Completed + gateway.OutboundLedger.Queued);
        Assert.Equal(gateway.InboundLedger.Generated, gateway.InboundLedger.Completed + gateway.InboundLedger.Queued);
        Assert.True(gateway.OutboundLedger.Generated > 0, "the gateway never generated any real demand across 5000 integrated ticks -- this test would prove nothing about conservation under load.");
    }

    /// <summary>Incident rate must stay bounded under REAL, live-generated
    /// conditions (real hour-of-day cycling, real congestion from the
    /// bottleneck way, real noise from seeded buildings) -- not only the
    /// synthetic risk=1.0-always worst case IncidentEngineTests already
    /// covers. Runs long enough to cross multiple full days.</summary>
    [Fact]
    public void IncidentRate_StaysBoundedUnderLiveConditions_AcrossMultipleFullDays()
    {
        var world = IntegratedTickFixtures.BuildFullyPopulatedWorldState();
        const int ticksPerDay = 5 * 3600 * 24;
        const int days = 3;
        const long totalTicks = (long)ticksPerDay * days;

        for (long tick = 0; tick < totalTicks; tick++)
        {
            world.SimulateTick();
        }

        var nightDisorderSite = world.IncidentSites.Single(s => s.SiteId == IntegratedTickFixtures.NightDisorderSiteId);
        var streetRacingSite = world.IncidentSites.Single(s => s.SiteId == IntegratedTickFixtures.StreetRacingSiteId);

        var nightMax = totalTicks / IncidentThresholdCatalog.NightDisorder.MinimumFullCycleTicks + 1;
        var racingMax = totalTicks / IncidentThresholdCatalog.StreetRacing.MinimumFullCycleTicks + 1;

        _output.WriteLine($"NightDisorder incidents over {totalTicks} live ticks ({days} days): {nightDisorderSite.IncidentsTriggered} (hard max {nightMax})");
        _output.WriteLine($"StreetRacing incidents over {totalTicks} live ticks ({days} days): {streetRacingSite.IncidentsTriggered} (hard max {racingMax})");

        Assert.True(nightDisorderSite.IncidentsTriggered <= nightMax);
        Assert.True(streetRacingSite.IncidentsTriggered <= racingMax);

        // Far below "one incident per tick" under real conditions too.
        Assert.True(nightDisorderSite.IncidentsTriggered < totalTicks / 20);
        Assert.True(streetRacingSite.IncidentsTriggered < totalTicks / 20);
    }

    /// <summary>Save/load round-trip must cover the now-live state: save
    /// mid-run, restore, continue N ticks, and the result must be
    /// identical to never having saved at all (same discipline as
    /// SaveLoadTests.SaveThenRestore_ThenContinue_..., but now with every
    /// G4 subsystem actually live and accumulating state via the real
    /// loop, not hand-populated by a test).</summary>
    [Fact]
    public void SaveThenRestore_ThenContinueTheIntegratedLoop_ProducesTheIdenticalHashAsNeverHavingSaved()
    {
        const string mapId = "integrated-tick-test-map";
        const string mapHash = "sha256:integrated";
        const int ticksBeforeSave = 5 * 3600 * 2; // 2 in-game hours.
        const int ticksAfterSave = 5 * 3600 * 3; // 3 more in-game hours.

        var neverSaved = IntegratedTickFixtures.BuildFullyPopulatedWorldState(masterSeed: 55);
        for (var i = 0; i < ticksBeforeSave + ticksAfterSave; i++)
        {
            neverSaved.SimulateTick();
        }

        var savedThenContinued = IntegratedTickFixtures.BuildFullyPopulatedWorldState(masterSeed: 55);
        for (var i = 0; i < ticksBeforeSave; i++)
        {
            savedThenContinued.SimulateTick();
        }

        var save = savedThenContinued.CaptureSave(mapId, mapHash, "0.1.0", "0.1.0");
        var json = Thaivia.Core.Simulation.Save.SaveSerializer.Serialize(save);
        var roundTripped = Thaivia.Core.Simulation.Save.SaveSerializer.Deserialize(json);
        var restored = Thaivia.Core.Simulation.WorldState.Restore(
            roundTripped, IntegratedTickFixtures.BuildGeographyBase(), IntegratedTickFixtures.BuildSimulationInitialization(),
            IntegratedTickFixtures.BuildRoadGraph(), new Thaivia.Core.Simulation.Scenario.ScenarioConfig(55, 5_000_000));

        // Restoring from save does not carry over registered signals/bus
        // routes/incident sites automatically UNLESS they were part of
        // the save -- they are (see SavedRecords.cs), but re-register is
        // not needed since Restore rebuilds them from the save file.
        for (var i = 0; i < ticksAfterSave; i++)
        {
            restored.SimulateTick();
        }

        Assert.Equal(neverSaved.ComputeStructuralHash(), restored.ComputeStructuralHash());

        // Sanity: the run actually produced real, nonzero live state --
        // otherwise this equality would be true for a trivial/empty
        // reason.
        Assert.True(neverSaved.LinkQueues.TotalArrived.Values.Sum() > 0);
    }

    /// <summary>Estimate must remain non-mutating even after the loop has
    /// run for a while and produced real, live G4 state (queues, gateway
    /// backlog, signal state, incident sites mid-Warning) -- this is
    /// exactly the kind of invariant integration breaks if Estimate*
    /// accidentally reads/writes anything beyond the pure accessibility
    /// graph it is supposed to.</summary>
    [Fact]
    public void EstimateStaysNonMutating_AfterTheIntegratedLoopHasProducedRealLiveMobilityState()
    {
        var world = IntegratedTickFixtures.BuildFullyPopulatedWorldState();
        for (var i = 0; i < 5 * 3600 * 2; i++)
        {
            world.SimulateTick();
        }

        var hashBefore = world.ComputeStructuralHash();
        var statesBefore = world.RandomStreams.CaptureStates();
        var drawsBefore = world.RandomStreams.CaptureDrawCounts();
        var cashBefore = world.Ledger.Available;

        _ = PlanningEngine.EstimateRelocationAccessImpact(world, IntegratedTickFixtures.ResidentialBuildingId, 610, 3, IntegratedTickFixtures.N40);
        _ = PlanningEngine.EstimateNewRoadAccessImpact(world, IntegratedTickFixtures.N10, IntegratedTickFixtures.N50, IntegratedTickFixtures.ResidentialBuildingId);
        _ = PlanningEngine.EstimateRoadWorksCapacityImpact(world, IntegratedTickFixtures.Way1002, 0.5);

        Assert.Equal(hashBefore, world.ComputeStructuralHash());
        Assert.Equal(statesBefore, world.RandomStreams.CaptureStates());
        Assert.Equal(drawsBefore, world.RandomStreams.CaptureDrawCounts());
        Assert.Equal(cashBefore, world.Ledger.Available);
    }

    /// <summary>Cohort Safety is no longer G3's flat baseline once
    /// incidents are live and have actually gone Active near a cohort's
    /// home -- proves the "cohort needs update" step is real, not a no-op
    /// left over from G3.</summary>
    [Fact]
    public void CohortSafety_DropsBelowBaseline_OnceANearbyIncidentGoesActiveUnderTheLiveLoop()
    {
        var world = IntegratedTickFixtures.BuildFullyPopulatedWorldState();
        var cohortId = world.Cohorts.Keys.Single(k => world.Cohorts[k].HomeBuildingSourceId == IntegratedTickFixtures.ResidentialBuildingId);

        // Run long enough that, under real live conditions, at least one
        // incident should have gone Active at the intersection (close to
        // the residential cohort's home node N10 -> N20 is one hop).
        const long totalTicks = 5 * 3600 * 24 * 2; // 2 full days.
        var everActive = false;
        for (long tick = 0; tick < totalTicks; tick++)
        {
            world.SimulateTick();
            if (world.IncidentSites.Any(s => s.SiteId == IntegratedTickFixtures.NightDisorderSiteId && s.Phase == IncidentPhase.Active))
            {
                everActive = true;
                break;
            }
        }

        Assert.True(everActive, "no NightDisorder incident ever went Active in 2 simulated days under live conditions -- cannot prove the safety linkage without one.");

        var needsWithIncident = world.ComputeCohortNeeds(cohortId, hourOfDay: 1);
        _output.WriteLine($"Safety with an Active nearby incident: {needsWithIncident.Safety} (baseline would be {Thaivia.Core.Simulation.Cohorts.CohortNeedsCalculator.BaselineSafety})");

        Assert.True(needsWithIncident.Safety < Thaivia.Core.Simulation.Cohorts.CohortNeedsCalculator.BaselineSafety,
            "Safety should be measurably reduced by a real Active incident one hop from home -- if it still reads the flat baseline, the live wiring in ComputeCohortNeeds/ComputeSafetyScore is not actually taking effect.");
    }
}
