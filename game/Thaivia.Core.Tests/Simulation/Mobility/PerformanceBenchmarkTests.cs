using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Thaivia.Core.Simulation.Save;
using Xunit;
using Xunit.Abstractions;

namespace Thaivia.Core.Tests.Simulation.Mobility;

/// <summary>
/// Measures real per-tick wall-clock cost of WorldState.SimulateTick
/// against spec §15's experimental targets (fixed tick 5Hz, "p95 work per
/// tick &lt;= 5 ms"). Run from `dotnet test` (see g5 supervisor brief --
/// this project already has xUnit wired up, a fixed-tick-rate simulation
/// benchmark needs no GUI/profiler infrastructure a console project would
/// add, and keeping it in the same test binary means the SAME build that
/// is already proven correct by the rest of the suite is what gets timed,
/// not a separately-built artifact that could drift out of sync).
///
/// HONESTY NOTE (see AGENTS.md rule 5/"Definition of Done"): this
/// benchmark does NOT hard-assert the 5ms p95 budget as an xUnit
/// assertion that would fail `dotnet test`. Per the supervisor's explicit
/// instruction, a missed budget must be REPORTED with its real number,
/// never hidden by shrinking the scenario until it passes, and also never
/// by turning the whole test suite red over a performance target that is
/// itself labelled "experimental" in the plan (spec §15). The real
/// p50/p95/max numbers are always printed via ITestOutputHelper (captured
/// verbatim in docs/evidence/g5-benchmark-*.log) and reported honestly in
/// docs/progress.md, whatever they are. The scenario/soak size is NEVER
/// tuned down after seeing a result -- the fixture's size is fixed by
/// PerformanceBenchmarkFixtures, committed before this file's numbers
/// were ever read.
/// </summary>
public class PerformanceBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const int TicksPerSecond = 5; // Thaivia.Core.Simulation.Time.TickConfig.TicksPerSecond.
    private const int WarmupTicks = 100; // JIT/first-access warmup, excluded from reported stats (standard microbenchmark practice, documented here rather than silently applied).
    private const int SoakTicks = 30 * 60 * TicksPerSecond; // a 30-minute-equivalent soak at 5Hz = 9,000 ticks.

    [Fact]
    public void SimulateTick_PerTickCost_OnAPilotSizedSyntheticGrid_30MinuteSoak_WithPauseResumeAndASaveInterruption()
    {
        var world = PerformanceBenchmarkFixtures.BuildFullyPopulatedWorldState();

        _output.WriteLine($"Scenario: {PerformanceBenchmarkFixtures.GridSize}x{PerformanceBenchmarkFixtures.GridSize} synthetic grid " +
            $"({world.RoadGraph.Nodes.Count} road nodes, {world.RoadGraph.Edges.Count} edges, {world.GeographyBase.Buildings.Count} buildings, " +
            $"{world.Cohorts.Count} cohorts, {world.Signals.Count} signals, {world.BusRoutes.Count} bus route(s), {world.IncidentSites.Count} incident sites, " +
            $"{world.RoadGraph.Gateways.Count} gateway(s)). THIS IS A SYNTHETIC STAND-IN, NOT THE REAL PILOT AOI -- no real OSM data is imported yet (ADR-0003).");

        // Warm-up: JIT the hot path, let any one-time allocation amortize,
        // before timing begins. Not included in reported stats.
        for (var i = 0; i < WarmupTicks; i++)
        {
            world.SimulateTick();
        }

        var samplesMs = new List<double>(SoakTicks);
        var sw = new Stopwatch();

        for (var tick = 0; tick < SoakTicks; tick++)
        {
            // Pause/resume partway through the soak -- must cost nothing
            // extra on the ticks around it (SimClock's doc comment: no
            // wall-clock catch-up), and must not corrupt subsequent
            // timing.
            if (tick == SoakTicks / 4)
            {
                world.Clock.Pause();
                world.Clock.Resume();
            }

            // A save interruption partway through the soak -- captures +
            // serializes + deserializes a full SaveGame mid-run. This is
            // NOT included in the per-tick stats below (it is deliberately
            // a one-off event, not a steady-state tick cost), but its own
            // wall-clock cost is measured and reported separately.
            if (tick == SoakTicks / 2)
            {
                var saveSw = Stopwatch.StartNew();
                var save = world.CaptureSave("bench-map", "sha256:bench", "0.1.0", "0.1.0");
                var json = SaveSerializer.Serialize(save);
                var roundTripped = SaveSerializer.Deserialize(json);
                saveSw.Stop();
                _output.WriteLine($"Save interruption at tick {tick}: capture+serialize+deserialize took {saveSw.Elapsed.TotalMilliseconds:F3} ms " +
                    $"(save file: {json.Length} bytes, CurrentTick={roundTripped.CurrentTick}).");
                Assert.Equal(world.Clock.CurrentTick, roundTripped.CurrentTick); // sanity: the save interruption captured the real mid-soak tick, not a stale one.
            }

            sw.Restart();
            world.SimulateTick();
            sw.Stop();
            samplesMs.Add(sw.Elapsed.TotalMilliseconds);
        }

        samplesMs.Sort();
        var p50 = Percentile(samplesMs, 0.50);
        var p95 = Percentile(samplesMs, 0.95);
        var max = samplesMs[^1];
        var mean = samplesMs.Average();
        const double budgetMs = 5.0;

        _output.WriteLine($"--- SimulateTick per-tick cost over {SoakTicks} ticks (30 in-game minutes at {TicksPerSecond}Hz), after {WarmupTicks}-tick warm-up ---");
        _output.WriteLine($"p50: {p50:F4} ms | p95: {p95:F4} ms | max: {max:F4} ms | mean: {mean:F4} ms");
        _output.WriteLine($"Budget (spec §15, experimental): p95 <= {budgetMs} ms -- {(p95 <= budgetMs ? "PASS" : "MISS")} (measured p95 = {p95:F4} ms).");

        // Regression guard only -- NOT the performance budget itself (see
        // this class's doc comment for why the 5ms budget is reported,
        // not asserted). This catches only a catastrophic, orders-of-
        // magnitude regression (e.g. an accidental O(n^2) blow-up), with
        // enormous headroom above any plausible real result.
        Assert.True(max < 2000.0, $"a single tick took {max:F1} ms -- something is pathologically wrong (not merely over the experimental 5ms budget).");
        Assert.Equal(SoakTicks, samplesMs.Count);
    }

    private static double Percentile(IReadOnlyList<double> sortedAscending, double fraction)
    {
        if (sortedAscending.Count == 1)
        {
            return sortedAscending[0];
        }

        var rank = fraction * (sortedAscending.Count - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);
        if (lowerIndex == upperIndex)
        {
            return sortedAscending[lowerIndex];
        }

        var fracPart = rank - lowerIndex;
        return sortedAscending[lowerIndex] + (sortedAscending[upperIndex] - sortedAscending[lowerIndex]) * fracPart;
    }
}
