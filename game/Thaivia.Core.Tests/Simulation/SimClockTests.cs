using System;
using System.IO;
using System.Linq;
using Thaivia.Core.Simulation.Time;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

public class SimClockTests
{
    [Fact]
    public void AdvanceTicks_OnlyMovesByTheExplicitCountGiven()
    {
        var clock = new SimClock();
        clock.AdvanceTicks(3);
        Assert.Equal(3, clock.CurrentTick);
        clock.AdvanceTicks(7);
        Assert.Equal(10, clock.CurrentTick);
    }

    [Fact]
    public void PauseThenResume_AddsZeroTicksOnItsOwn()
    {
        // Simulates "app backgrounded for a long time": Pause, do nothing
        // that looks like elapsed wall time (there IS no such input to
        // give this type -- see the next test), Resume, then advance
        // explicitly by exactly the ticks the game's own fixed-step loop
        // decides to run. No matter how long a real clock would say we
        // waited, CurrentTick only ever reflects AdvanceTicks calls.
        var clock = new SimClock();
        clock.AdvanceTicks(5);
        clock.Pause();
        System.Threading.Thread.Sleep(50); // a real-world gap the clock must not see.
        clock.Resume();
        Assert.Equal(5, clock.CurrentTick); // unchanged by the pause/resume/sleep.
        clock.AdvanceTicks(1);
        Assert.Equal(6, clock.CurrentTick);
    }

    [Fact]
    public void AdvanceTicks_WhilePaused_Throws()
    {
        var clock = new SimClock();
        clock.Pause();
        Assert.Throws<InvalidOperationException>(() => clock.AdvanceTicks(1));
    }

    [Fact]
    public void AdvanceTicks_RejectsNegativeCounts()
    {
        var clock = new SimClock();
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceTicks(-1));
    }

    /// <summary>Resume's signature itself is the strongest evidence: it
    /// takes no parameters at all, so there is no argument slot a
    /// wall-clock-based catch-up computation could ever read from without
    /// changing this reflected signature.</summary>
    [Fact]
    public void Resume_TakesNoParameters()
    {
        var method = typeof(SimClock).GetMethod("Resume");
        Assert.NotNull(method);
        Assert.Empty(method!.GetParameters());
    }

    /// <summary>Source-scan control test: SimClock.cs and WorldState.cs
    /// must contain no wall-clock read at all. Proven not to be a vacuous
    /// scan by first running it against a synthetic string that DOES
    /// contain a wall-clock call and confirming the same detector flags
    /// it (mirrors the standard set by
    /// NoUnityEngineReferenceTests in this same test project).</summary>
    [Fact]
    public void Detector_FlagsASyntheticWallClockUsage_ControlCase()
    {
        const string tampered = "var elapsed = DateTime.Now - lastTick; CurrentTick += (long)(elapsed.TotalSeconds * TickConfig.TicksPerSecond);";
        Assert.True(ContainsWallClockRead(tampered), "Control case failed: the detector itself does not catch an obvious DateTime.Now catch-up computation.");
    }

    [Fact]
    public void SimClockAndWorldStateSource_ContainNoWallClockRead()
    {
        var repoRoot = FindRepoRoot();
        var coreDir = Path.Combine(repoRoot, "game", "Assets", "Scripts", "Core", "Simulation");
        var files = Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        var offenders = new System.Collections.Generic.List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);

            // Scan CODE lines only: doc comments in this very file (and
            // in this test file's control case below) legitimately
            // mention "DateTime.Now" etc. as prose explaining what must
            // NOT appear in executable code, which would otherwise be a
            // false positive against the file that documents the rule
            // best.
            var codeOnly = string.Join('\n', text.Split('\n').Where(line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal)));
            if (ContainsWallClockRead(codeOnly))
            {
                offenders.Add(file);
            }
        }

        Assert.True(offenders.Count == 0, "Wall-clock read found in simulation source (violates 'no wall-clock catch-up'): " + string.Join(", ", offenders));
    }

    private static bool ContainsWallClockRead(string text) =>
        text.Contains("DateTime.Now", StringComparison.Ordinal)
        || text.Contains("DateTime.UtcNow", StringComparison.Ordinal)
        || text.Contains("Environment.TickCount", StringComparison.Ordinal)
        || text.Contains("Stopwatch", StringComparison.Ordinal);

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "game", "Assets", "Scripts", "Core")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Could not locate repo root from test output directory.");
        }

        return dir;
    }
}
