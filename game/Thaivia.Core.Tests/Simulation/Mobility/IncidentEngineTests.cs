using System;
using System.Linq;
using System.Reflection;
using Thaivia.Core.Simulation.Archetypes;
using Thaivia.Core.Simulation.Mobility.Incidents;
using Thaivia.Core.Simulation.RandomStreams;
using Xunit;
using Xunit.Abstractions;

namespace Thaivia.Core.Tests.Simulation.Mobility;

public class IncidentEngineTests
{
    private readonly ITestOutputHelper _output;

    public IncidentEngineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly IncidentThresholds Thresholds = new(RiskThreshold: 0.5, WarningLeadTicks: 5, DurationTicks: 10, CooldownTicks: 20);

    [Fact]
    public void Idle_BelowThreshold_NeverStartsAWarning()
    {
        var site = new IncidentSite("site-1", IncidentStrand.NightDisorder);
        for (var i = 0; i < 100; i++)
        {
            IncidentEngine.Step(site, risk: 0.1, Thresholds);
        }

        Assert.Equal(IncidentPhase.Idle, site.Phase);
        Assert.Equal(0, site.WarningsIssued);
        Assert.Equal(0, site.IncidentsTriggered);
    }

    [Fact]
    public void SustainedHighRisk_ProgressesWarningThenActiveThenCooldownThenIdle()
    {
        var site = new IncidentSite("site-1", IncidentStrand.NightDisorder);
        const double highRisk = 0.9;

        // 1 call to enter Warning (from Idle) + WarningLeadTicks calls
        // spent in Warning = Active.
        for (var i = 0; i < Thresholds.WarningLeadTicks + 1; i++)
        {
            IncidentEngine.Step(site, highRisk, Thresholds);
        }

        Assert.Equal(IncidentPhase.Active, site.Phase);
        Assert.Equal(1, site.IncidentsTriggered);
        Assert.InRange(site.LastSeverity, 1, 100);

        // DurationTicks = 10: after 10 more steps, Cooldown.
        for (var i = 0; i < 10; i++)
        {
            IncidentEngine.Step(site, highRisk, Thresholds);
        }

        Assert.Equal(IncidentPhase.Cooldown, site.Phase);

        // CooldownTicks = 20: after 20 more steps, back to Idle -- even
        // though risk is STILL high the whole time.
        for (var i = 0; i < 20; i++)
        {
            IncidentEngine.Step(site, highRisk, Thresholds);
        }

        Assert.Equal(IncidentPhase.Idle, site.Phase);
        Assert.Equal(1, site.IncidentsTriggered); // still only ONE incident despite risk staying high throughout.
    }

    [Fact]
    public void PreventionLever_CanCancelAWarningBeforeItBecomesActive()
    {
        var withoutLever = new IncidentSite("site-a", IncidentStrand.NightDisorder);
        var withLever = new IncidentSite("site-b", IncidentStrand.NightDisorder);
        const double baseRisk = 0.6; // above the 0.5 threshold on its own.

        // Both sites see the SAME unmitigated risk for the first 2 ticks
        // (both enter Warning identically) -- the lever is applied only
        // from tick 3 onward, so this exercises the Warning->Idle
        // cancellation path, not "the lever prevented Warning from ever
        // starting".
        for (var tick = 0; tick < 2; tick++)
        {
            IncidentEngine.Step(withoutLever, baseRisk, Thresholds);
            IncidentEngine.Step(withLever, baseRisk, Thresholds);
        }

        Assert.Equal(IncidentPhase.Warning, withLever.Phase);
        Assert.Equal(1, withLever.WarningsIssued);

        for (var tick = 0; tick < Thresholds.WarningLeadTicks; tick++)
        {
            IncidentEngine.Step(withoutLever, baseRisk, Thresholds);

            // A patrol lever strong enough to push risk under threshold.
            var mitigatedRisk = IncidentLevers.ApplyPatrol(baseRisk, patrolStrength: 0.5); // 0.6 * 0.5 = 0.3 < 0.5.
            IncidentEngine.Step(withLever, mitigatedRisk, Thresholds);
        }

        Assert.Equal(IncidentPhase.Active, withoutLever.Phase);
        Assert.Equal(1, withoutLever.IncidentsTriggered);

        Assert.Equal(IncidentPhase.Idle, withLever.Phase);
        Assert.Equal(0, withLever.IncidentsTriggered);
        Assert.Equal(1, withLever.WarningsIssued); // the warning happened, then was cancelled -- never became an incident.
    }

    [Fact]
    public void ResponseLever_ShortensAnActiveIncidentsDuration()
    {
        var normalDuration = Thresholds.DurationTicks;
        var fasterResponseDuration = IncidentLevers.ApplyFasterResponse(normalDuration, responseStrength: 0.6);
        Assert.True(fasterResponseDuration < normalDuration);

        var respondedThresholds = Thresholds with { DurationTicks = fasterResponseDuration };
        var site = new IncidentSite("site-1", IncidentStrand.NightDisorder);
        for (var i = 0; i < Thresholds.WarningLeadTicks + 1; i++)
        {
            IncidentEngine.Step(site, 0.9, respondedThresholds);
        }

        Assert.Equal(IncidentPhase.Active, site.Phase);

        var ticksToCooldown = 0;
        while (site.Phase == IncidentPhase.Active)
        {
            IncidentEngine.Step(site, 0.9, respondedThresholds);
            ticksToCooldown++;
        }

        Assert.Equal(fasterResponseDuration, ticksToCooldown);
        Assert.True(ticksToCooldown < normalDuration);
    }

    /// <summary>The test the supervising engineer asked for by name:
    /// triggering is driven by conditions, not by the RNG stream. Running
    /// the SAME risk time series through two engines seeded with
    /// DIFFERENT master seeds must produce the IDENTICAL phase-transition
    /// sequence -- only severity (a bounded jitter value, never a gate) is
    /// allowed to differ.</summary>
    [Fact]
    public void Triggering_IsDrivenByConditions_NotByTheRngSeed_OnlySeverityDiffersBetweenSeeds()
    {
        var siteSeedA = new IncidentSite("site-1", IncidentStrand.NightDisorder);
        var siteSeedB = new IncidentSite("site-1", IncidentStrand.NightDisorder);
        var streamA = new NamedRandomStreams(masterSeed: 111).Stream(RandomStreamName.Incidents);
        var streamB = new NamedRandomStreams(masterSeed: 999).Stream(RandomStreamName.Incidents);

        // A deterministic, non-constant risk time series (rises, sustains,
        // falls) -- entirely a function of `tick`, no RNG anywhere in it.
        double RiskAt(int tick) => tick % 40 < 20 ? 0.9 : 0.1;

        var phaseSequenceA = new System.Collections.Generic.List<IncidentPhase>();
        var phaseSequenceB = new System.Collections.Generic.List<IncidentPhase>();

        for (var tick = 0; tick < 200; tick++)
        {
            IncidentEngine.Step(siteSeedA, RiskAt(tick), Thresholds, streamA);
            IncidentEngine.Step(siteSeedB, RiskAt(tick), Thresholds, streamB);
            phaseSequenceA.Add(siteSeedA.Phase);
            phaseSequenceB.Add(siteSeedB.Phase);
        }

        Assert.Equal(phaseSequenceA, phaseSequenceB);
        Assert.Equal(siteSeedA.IncidentsTriggered, siteSeedB.IncidentsTriggered);
        Assert.True(siteSeedA.IncidentsTriggered > 0, "the test must actually exercise triggering, or this proves nothing.");

        // The only thing allowed to differ between the two seeds.
        _output.WriteLine($"seed 111 last severity: {siteSeedA.LastSeverity}, seed 999 last severity: {siteSeedB.LastSeverity}");
        Assert.NotEqual(siteSeedA.LastSeverity, siteSeedB.LastSeverity);
    }

    /// <summary>The test the supervising engineer asked for by name: a
    /// hard bound on incident rate over a long run -- no per-tick
    /// spawning. Runs risk pinned at maximum (the worst case for rate) for
    /// 100,000 ticks and asserts the observed incident count never exceeds
    /// what MinimumFullCycleTicks allows, and is orders of magnitude below
    /// the tick count.</summary>
    [Fact]
    public void IncidentRate_IsHardBoundedOverALongRun_NeverPerTickSpawning()
    {
        var site = new IncidentSite("site-1", IncidentStrand.StreetRacing);
        const long totalTicks = 100_000;

        for (long tick = 0; tick < totalTicks; tick++)
        {
            IncidentEngine.Step(site, risk: 1.0, Thresholds); // worst case: condition always met.
        }

        var theoreticalMax = totalTicks / Thresholds.MinimumFullCycleTicks + 1; // +1 for a partial final cycle.
        _output.WriteLine($"IncidentsTriggered over {totalTicks} ticks: {site.IncidentsTriggered} (theoretical max: {theoreticalMax}, MinimumFullCycleTicks: {Thresholds.MinimumFullCycleTicks})");

        Assert.True(site.IncidentsTriggered <= theoreticalMax);
        // Far below "one incident per tick" -- bounded to at most one per
        // MinimumFullCycleTicks (35 here), i.e. well under 5% of ticks.
        Assert.True(site.IncidentsTriggered < totalTicks / 20);
    }

    /// <summary>Structural ethical control (AGENTS.md rule 9): no public
    /// method anywhere in the Incidents namespace accepts a
    /// BuildingArchetype (or any archetype-shaped) parameter -- risk can
    /// only ever be computed from generic, already-anonymised
    /// environmental signals, never from "what kind of place is this".
    /// Mirrors the codebase's existing reflection-based structural tests
    /// (e.g. ImmutabilityTests, LayerSeparationTests).</summary>
    [Fact]
    public void EthicalControl_NoIncidentsApiTakesABuildingArchetypeParameter()
    {
        var incidentsNamespaceTypes = typeof(IncidentEngine).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(IncidentEngine).Namespace);

        foreach (var type in incidentsNamespaceTypes)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var parameter in method.GetParameters())
                {
                    Assert.False(
                        parameter.ParameterType == typeof(BuildingArchetype),
                        $"{type.FullName}.{method.Name}({parameter.Name}) must not take a BuildingArchetype -- incident risk must never be computed from place identity (AGENTS.md rule 9).");
                }
            }
        }
    }
}
