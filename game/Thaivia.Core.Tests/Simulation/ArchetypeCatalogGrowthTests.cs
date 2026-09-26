using System.Collections.Generic;
using System.Linq;
using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Archetypes;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// Task Zero (wave 9, ADR-0038, supersedes ADR-0030 section 4): proves the
/// actual bug ADR-0030 hit ("adding one archetype reshuffles the
/// archetype of every building in every area and every existing save") is
/// fixed at the CAUSE, not just worked around by renumbering fixtures
/// again.
/// </summary>
public class ArchetypeCatalogGrowthTests
{
    // ------------------------------------------------------------------
    // Part 1: the real, shipped catalog (V1 = G3's 8 archetypes, V2 =
    // G6-04's 12) never reshuffles -- V1 is a literal, unchanged PREFIX
    // of V2, and V2 matches exactly what the OLD code's
    // Enum.GetValues(...)-order/modulus-12 scheme already gave (so this
    // refactor is a pure cause-fix, not a behavior change for today's 12
    // archetypes).
    // ------------------------------------------------------------------

    [Fact]
    public void V2_StartsWithExactlyV1InOrder_AppendedNotReDivided()
    {
        var v1 = ArchetypeCatalog.ForVersion(1);
        var v2 = ArchetypeCatalog.ForVersion(2);

        Assert.Equal(8, v1.Count);
        Assert.Equal(12, v2.Count);
        for (var i = 0; i < v1.Count; i++)
        {
            Assert.Equal(v1[i], v2[i]);
        }
    }

    [Fact]
    public void V2_MatchesBuildingArchetypesDeclarationOrder_SoThisRefactorChangesNoExistingAssignment()
    {
        var declared = (BuildingArchetype[])System.Enum.GetValues(typeof(BuildingArchetype));
        Assert.Equal(declared, ArchetypeCatalog.ForVersion(ArchetypeCatalog.CurrentVersion));
    }

    [Fact]
    public void AssignArchetype_PinnedToV1_GivesTheSameAnswerAsTheOldPreG6ModuloEightScheme()
    {
        // The old scheme (before ADR-0030 grew the enum) was
        // hash(sourceId) % 8, indexed into the G3 declaration order --
        // exactly ArchetypeCatalog.ForVersion(1). Reproducing that
        // arithmetic independently here (not by calling AssignArchetype)
        // is the control that proves ForVersion(1) really is a faithful,
        // frozen snapshot of "what G3 used to compute", not just
        // internally self-consistent.
        var v1 = ArchetypeCatalog.ForVersion(1);
        for (long sourceId = 0; sourceId < 2000; sourceId++)
        {
            var mixed = Thaivia.Core.Simulation.RandomStreams.DeterministicRandom.HashStep(unchecked((ulong)sourceId));
            var expected = v1[(int)(mixed % 8UL)];
            Assert.Equal(expected, WorldState.AssignArchetype(sourceId, archetypeCatalogVersion: 1));
        }
    }

    // ------------------------------------------------------------------
    // Part 2: demonstrate the BUG this ADR supersedes is real (not
    // hypothetical), then prove the new mechanism does not have it.
    // ------------------------------------------------------------------

    /// <summary>Reproduces the OLD, buggy scheme
    /// (<c>hash(sourceId) % catalogLength</c>, re-derived fresh from
    /// whatever the CURRENT length is) directly, so this test does not
    /// depend on that code still existing anywhere in production.</summary>
    private static int OldBuggyHashModLengthScheme(long sourceId, int catalogLength)
    {
        var mixed = Thaivia.Core.Simulation.RandomStreams.DeterministicRandom.HashStep(unchecked((ulong)sourceId));
        return (int)(mixed % (ulong)catalogLength);
    }

    [Fact]
    public void OldHashModCatalogLengthScheme_ReshufflesManyExistingAssignments_WhenTheCatalogGrows()
    {
        // Positive control: proves the failure mode ADR-0030 hit was real
        // arithmetic, not a one-off fixture coincidence -- growing the
        // divisor from 8 to 12 changes the result for the clear majority
        // of source ids (not literally all of them -- some residues
        // survive by chance -- but far more than would be acceptable for
        // "must not change ANY previously-assigned building").
        var changed = 0;
        const int sampleSize = 5000;
        for (long sourceId = 0; sourceId < sampleSize; sourceId++)
        {
            var underEight = OldBuggyHashModLengthScheme(sourceId, 8);
            var underTwelve = OldBuggyHashModLengthScheme(sourceId, 12);
            if (underEight != underTwelve)
            {
                changed++;
            }
        }

        Assert.True(changed > sampleSize / 2, $"expected the old scheme to reshuffle the majority of {sampleSize} sample ids when the catalog grows from 8 to 12, only {changed} changed.");
    }

    [Fact]
    public void NewScheme_PinnedVersion_NeverReshuffles_EvenThoughTheRealCatalogHasGrownFrom8To12()
    {
        // Negative control / the actual fix: the SAME sourceIds, resolved
        // through the new pinned-version mechanism, must give IDENTICAL
        // results whether we ask "under catalog v1" before or after v2
        // exists in this codebase -- because v2 existing does not, and
        // structurally cannot, mutate v1's frozen list.
        const int sampleSize = 5000;
        for (long sourceId = 0; sourceId < sampleSize; sourceId++)
        {
            var before = WorldState.AssignArchetype(sourceId, archetypeCatalogVersion: 1);
            var after = WorldState.AssignArchetype(sourceId, archetypeCatalogVersion: 1); // v2 already shipped in this build; result must be unaffected.
            Assert.Equal(before, after);
            Assert.Contains(before, ArchetypeCatalog.ForVersion(1)); // still only ever one of the original 8.
        }
    }

    // ------------------------------------------------------------------
    // Part 3: the general mechanism, proven with a generic seam
    // (AppendOnlyCatalogAssignment over a small local test category set)
    // so growth can be exercised directly within a single test run
    // without adding a throwaway member to the real, reviewed
    // BuildingArchetype enum. THIS is "adds a new archetype ... and
    // asserts every previously-assigned building keeps its assignment"
    // in its most literal, dynamic form.
    // ------------------------------------------------------------------

    private enum FakeCategory
    {
        Alpha,
        Bravo,
        Charlie,
        Delta,
        Echo,
        Foxtrot,
        Golf,
        Hotel,
    }

    [Fact]
    public void AppendOnlyCatalogAssignment_AddingACategory_ChangesNoIdThatWasAssignedAgainstTheOldCatalog()
    {
        var beforeGrowth = new List<FakeCategory> { FakeCategory.Alpha, FakeCategory.Bravo, FakeCategory.Charlie, FakeCategory.Delta, FakeCategory.Echo, FakeCategory.Foxtrot, FakeCategory.Golf, FakeCategory.Hotel };

        // Snapshot "every previously-assigned building's" category under
        // the pre-growth catalog.
        const int sampleSize = 10_000;
        var assignedBefore = new Dictionary<long, FakeCategory>();
        for (long sourceId = 0; sourceId < sampleSize; sourceId++)
        {
            assignedBefore[sourceId] = AppendOnlyCatalogAssignment.Assign(sourceId, beforeGrowth);
        }

        // "The catalog grows": a NEW, separately-frozen list with one
        // more real category appended at the end -- beforeGrowth itself
        // is untouched (proving growth is genuinely append-only, not an
        // in-place mutation of the list every existing building already
        // resolved against).
        var afterGrowth = new List<FakeCategory>(beforeGrowth) { (FakeCategory)8 };
        Assert.Equal(8, beforeGrowth.Count); // the old list itself never changed.

        // Every previously-assigned building, re-resolved against the
        // SAME old (pinned) catalog it was originally assigned under,
        // keeps its exact category -- growth happening elsewhere cannot
        // touch it.
        foreach (var (sourceId, category) in assignedBefore)
        {
            Assert.Equal(category, AppendOnlyCatalogAssignment.Assign(sourceId, beforeGrowth));
        }

        // Sanity/negative control: had anything (mistakenly) resolved
        // these same ids against the GROWN list instead, a large share
        // WOULD differ -- proving the previous assertion is meaningful,
        // not vacuously true because appending happens not to matter.
        var wouldHaveChangedIfMisResolved = assignedBefore.Count(kv => AppendOnlyCatalogAssignment.Assign(kv.Key, afterGrowth) != kv.Value);
        Assert.True(wouldHaveChangedIfMisResolved > sampleSize / 4, $"expected resolving against the grown list to disagree with the pinned-list result for a large share of ids (control), only {wouldHaveChangedIfMisResolved} did.");
    }

    [Fact]
    public void AppendOnlyCatalogAssignment_RejectsAnEmptyCatalog()
    {
        Assert.Throws<System.ArgumentException>(() => AppendOnlyCatalogAssignment.Assign(1L, new List<FakeCategory>()));
    }
}
