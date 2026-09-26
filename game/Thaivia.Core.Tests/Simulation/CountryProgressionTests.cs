using System.Collections.Generic;
using Thaivia.Core.Simulation.Progression;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// G6-10: the country/area-select progress model, driven through
/// multiple areas with no rendering and no Unity dependency at all.
/// </summary>
public class CountryProgressionTests
{
    private static CountryProgression ThreeAreaCountry() => new(new[]
    {
        new AreaDefinition("th-bkk-pilot-001", "Pilot area"),
        new AreaDefinition("th-bkk-candidate-002", "Candidate area 2"),
        new AreaDefinition("th-bkk-candidate-003", "Candidate area 3"),
    });

    private static HashSet<string> Set(params string[] ids) => new(ids);

    [Fact]
    public void FirstArea_IsUnlockedEvenWithNoRecordedProgressAnywhere()
    {
        var country = ThreeAreaCountry();
        var evaluated = country.Evaluate(completedAreaIds: Set());

        Assert.Equal(AreaProgressStatus.Unlocked, evaluated[0].Status);
        Assert.Equal(AreaProgressStatus.Locked, evaluated[1].Status);
        Assert.Equal(AreaProgressStatus.Locked, evaluated[2].Status);
    }

    [Fact]
    public void SecondArea_UnlocksOnlyOnceFirstIsRecordedComplete()
    {
        var country = ThreeAreaCountry();
        var evaluated = country.Evaluate(completedAreaIds: Set("th-bkk-pilot-001"));

        Assert.Equal(AreaProgressStatus.Completed, evaluated[0].Status);
        Assert.Equal(AreaProgressStatus.Unlocked, evaluated[1].Status);
        Assert.Equal(AreaProgressStatus.Locked, evaluated[2].Status);
    }

    [Fact]
    public void StartedButNotCompleted_ReadsAsInProgress()
    {
        var country = ThreeAreaCountry();
        var evaluated = country.Evaluate(
            completedAreaIds: Set(),
            startedAreaIds: Set("th-bkk-pilot-001"));

        Assert.Equal(AreaProgressStatus.InProgress, evaluated[0].Status);
    }

    [Fact]
    public void AllThreeCompleted_MakesTheCountryComplete()
    {
        var country = ThreeAreaCountry();
        var allDone = Set("th-bkk-pilot-001", "th-bkk-candidate-002", "th-bkk-candidate-003");

        Assert.True(country.IsCountryComplete(allDone));
        Assert.False(country.IsCountryComplete(Set("th-bkk-pilot-001")));
    }

    /// <summary>An area marked complete out of order (its predecessor is
    /// NOT complete) must not make that area, or anything after it,
    /// reachable -- this method never trusts an area's own completion
    /// flag to imply legitimate access to it.</summary>
    [Fact]
    public void OutOfOrderCompletionRecord_StillLocksEverythingAfterTheGap()
    {
        var country = ThreeAreaCountry();
        // Candidate 3 recorded complete, but candidate 2 was never even started.
        var evaluated = country.Evaluate(completedAreaIds: Set("th-bkk-candidate-003"));

        Assert.Equal(AreaProgressStatus.Unlocked, evaluated[0].Status); // first area, unaffected.
        Assert.Equal(AreaProgressStatus.Locked, evaluated[1].Status); // predecessor (area 0) not complete.
        Assert.Equal(AreaProgressStatus.Locked, evaluated[2].Status); // its own predecessor (area 1) not complete -- own "completed" flag is not trusted.
    }

    [Fact]
    public void IsAreaSelectable_MatchesEvaluatedLockState()
    {
        var country = ThreeAreaCountry();
        var completed = Set("th-bkk-pilot-001");

        Assert.True(country.IsAreaSelectable("th-bkk-pilot-001", completed));
        Assert.True(country.IsAreaSelectable("th-bkk-candidate-002", completed));
        Assert.False(country.IsAreaSelectable("th-bkk-candidate-003", completed));
        Assert.False(country.IsAreaSelectable("no-such-area", completed));
    }

    /// <summary>Pure/deterministic: the exact same two inputs, evaluated
    /// twice (including with brand-new HashSet instances holding the
    /// same contents, so this cannot pass merely by identity/caching),
    /// must produce an identical result every time -- no clock, no
    /// hidden state.</summary>
    [Fact]
    public void Evaluate_IsPureAndDeterministic()
    {
        var country = ThreeAreaCountry();

        var first = country.Evaluate(Set("th-bkk-pilot-001"), Set("th-bkk-candidate-002"));
        var second = country.Evaluate(new HashSet<string> { "th-bkk-pilot-001" }, new HashSet<string> { "th-bkk-candidate-002" });

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].Area.AreaId, second[i].Area.AreaId);
            Assert.Equal(first[i].Status, second[i].Status);
        }
    }

    [Fact]
    public void DuplicateAreaId_IsRejectedAtConstruction()
    {
        Assert.Throws<System.ArgumentException>(() => new CountryProgression(new[]
        {
            new AreaDefinition("dup", "A"),
            new AreaDefinition("dup", "B"),
        }));
    }

    [Fact]
    public void EmptyAreaList_IsRejectedAtConstruction()
    {
        Assert.Throws<System.ArgumentException>(() => new CountryProgression(System.Array.Empty<AreaDefinition>()));
    }

    /// <summary>Structural control: this type must not reference a wall
    /// clock anywhere -- if it ever did, lock state could silently
    /// depend on when it happened to be called rather than only on the
    /// two explicit recorded-completion parameters (the acceptance
    /// criterion this whole type exists to satisfy).</summary>
    [Fact]
    public void EthicalControl_TypeReferencesNoWallClockApi()
    {
        var forbidden = new[] { "DateTime", "DateTimeOffset", "Environment.TickCount", "Stopwatch" };
        var source = System.IO.File.ReadAllText(FindSourceFile());
        foreach (var token in forbidden)
        {
            Assert.DoesNotContain(token, source);
        }
    }

    private static string FindSourceFile()
    {
        var dir = System.IO.Path.GetDirectoryName(typeof(CountryProgressionTests).Assembly.Location)!;
        // Walk up to the repo's game/ directory and locate the source file directly,
        // rather than embedding a brittle relative path assumption about build output layout.
        var candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, "..", "..", "..", "..", "Assets", "Scripts", "Core", "Simulation", "Progression", "CountryProgression.cs"));
        if (System.IO.File.Exists(candidate))
        {
            return candidate;
        }

        // Fallback: search from the test assembly's directory upward for the file by name --
        // robust to CI layouts that don't match the local relative-path assumption above.
        var current = new System.IO.DirectoryInfo(dir);
        while (current is not null)
        {
            var found = System.IO.Directory.GetFiles(current.FullName, "CountryProgression.cs", System.IO.SearchOption.AllDirectories);
            if (found.Length > 0)
            {
                return found[0];
            }

            current = current.Parent;
        }

        throw new System.IO.FileNotFoundException("Could not locate CountryProgression.cs from the test assembly's location.");
    }
}
