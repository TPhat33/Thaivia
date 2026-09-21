using System.Linq;
using Thaivia.Core.Simulation.RandomStreams;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

public class NamedRandomStreamsTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalSequencePerStream()
    {
        var a = new NamedRandomStreams(12345);
        var b = new NamedRandomStreams(12345);

        foreach (var name in new[] { RandomStreamName.Traffic, RandomStreamName.Incidents, RandomStreamName.CohortVariation })
        {
            var sa = Enumerable.Range(0, 10).Select(_ => a.Stream(name).NextUInt64()).ToArray();
            var sb = Enumerable.Range(0, 10).Select(_ => b.Stream(name).NextUInt64()).ToArray();
            Assert.Equal(sa, sb);
        }
    }

    [Fact]
    public void DifferentStreams_StartFromDifferentSequences()
    {
        var streams = new NamedRandomStreams(999);
        var traffic = streams.Stream(RandomStreamName.Traffic).NextUInt64();
        var incidents = streams.Stream(RandomStreamName.Incidents).NextUInt64();
        var cohort = streams.Stream(RandomStreamName.CohortVariation).NextUInt64();

        Assert.NotEqual(traffic, incidents);
        Assert.NotEqual(traffic, cohort);
        Assert.NotEqual(incidents, cohort);
    }

    /// <summary>The scrutinised invariant: drawing from stream A must not
    /// shift stream B's sequence at all. Proven by comparing B's next N
    /// draws, taken AFTER heavily drawing from A, against a completely
    /// independent NamedRandomStreams built from the same seed whose B
    /// stream is read FIRST (before anything else is touched) -- if A's
    /// draws had any effect on B, these two sequences would diverge.</summary>
    [Fact]
    public void DrawingFromOneStream_DoesNotShiftAnotherStreamsSequence()
    {
        var streamsUnderTest = new NamedRandomStreams(2024);

        // Heavily exercise Traffic and Incidents first.
        for (var i = 0; i < 500; i++)
        {
            streamsUnderTest.Stream(RandomStreamName.Traffic).NextUInt64();
        }

        for (var i = 0; i < 250; i++)
        {
            streamsUnderTest.Stream(RandomStreamName.Incidents).NextUInt64();
        }

        var cohortAfterOthersDrawn = Enumerable.Range(0, 5)
            .Select(_ => streamsUnderTest.Stream(RandomStreamName.CohortVariation).NextUInt64())
            .ToArray();

        // A pristine set of streams from the identical seed, where
        // CohortVariation is the very first (and only) thing drawn.
        var pristine = new NamedRandomStreams(2024);
        var cohortFromPristine = Enumerable.Range(0, 5)
            .Select(_ => pristine.Stream(RandomStreamName.CohortVariation).NextUInt64())
            .ToArray();

        Assert.Equal(cohortFromPristine, cohortAfterOthersDrawn);
    }

    [Fact]
    public void RestoredStreams_ContinueExactlyWhereCapturedStateLeftOff()
    {
        var original = new NamedRandomStreams(555);
        for (var i = 0; i < 17; i++)
        {
            original.Stream(RandomStreamName.Traffic).NextUInt64();
        }

        var states = original.CaptureStates();
        var drawCounts = original.CaptureDrawCounts();

        var expectedNext = Enumerable.Range(0, 5).Select(_ => original.Stream(RandomStreamName.Traffic).NextUInt64()).ToArray();

        var restored = NamedRandomStreams.Restore(555, states, drawCounts);
        var actualNext = Enumerable.Range(0, 5).Select(_ => restored.Stream(RandomStreamName.Traffic).NextUInt64()).ToArray();

        Assert.Equal(expectedNext, actualNext);
        Assert.Equal(17 + 5, restored.CaptureDrawCounts()[RandomStreamName.Traffic]);
    }

    [Fact]
    public void HashStep_IsPureAndDoesNotConsumeAnyStream()
    {
        var streams = new NamedRandomStreams(1);
        var before = streams.CaptureStates();
        var beforeDraws = streams.CaptureDrawCounts();

        _ = DeterministicRandom.HashStep(123456789UL);
        _ = DeterministicRandom.HashStep(987654321UL);

        Assert.Equal(before, streams.CaptureStates());
        Assert.Equal(beforeDraws, streams.CaptureDrawCounts());
    }
}
