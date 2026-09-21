// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;

namespace Thaivia.Core.Simulation.RandomStreams;

/// <summary>
/// Holds one independent <see cref="DeterministicRandom"/> per
/// <see cref="RandomStreamName"/>, each seeded by mixing the shared master
/// seed with a fixed, distinct per-stream salt. Because each stream is a
/// genuinely separate <see cref="DeterministicRandom"/> object with its
/// own private state field, drawing from one stream cannot, by
/// construction, change any other stream's state -- there is no shared
/// mutable state between them at all (see
/// Thaivia.Core.Tests.NamedRandomStreamsTests for the test that proves
/// this empirically as well).
/// </summary>
public sealed class NamedRandomStreams
{
    private static readonly IReadOnlyDictionary<RandomStreamName, ulong> Salts = new Dictionary<RandomStreamName, ulong>
    {
        // Large, fixed, distinct odd constants -- arbitrary but permanent
        // (changing one would silently reseed every save from before the
        // change). One splitmix64 step is applied on top of
        // (masterSeed ^ salt) below so two streams never start from
        // trivially related states even when MasterSeed is 0.
        [RandomStreamName.Traffic] = 0xA24BAED4963EE407UL,
        [RandomStreamName.Incidents] = 0x9FB21C651E98DF25UL,
        [RandomStreamName.CohortVariation] = 0xD6E8FEB86659FD93UL,
    };

    private readonly Dictionary<RandomStreamName, DeterministicRandom> _streams = new();

    public NamedRandomStreams(long masterSeed)
    {
        MasterSeed = masterSeed;
        foreach (var name in (RandomStreamName[])Enum.GetValues(typeof(RandomStreamName)))
        {
            var seeded = DeterministicRandom.HashStep((ulong)masterSeed ^ Salts[name]);
            _streams[name] = new DeterministicRandom(seeded);
        }
    }

    private NamedRandomStreams(long masterSeed, Dictionary<RandomStreamName, DeterministicRandom> streams)
    {
        MasterSeed = masterSeed;
        _streams = streams;
    }

    public long MasterSeed { get; }

    public DeterministicRandom Stream(RandomStreamName name) => _streams[name];

    /// <summary>Restores every stream to an exact prior (state, drawCount)
    /// pair -- used only by save/load, so a restored world continues each
    /// named stream from precisely where the save was taken, not from a
    /// freshly reseeded position.</summary>
    public static NamedRandomStreams Restore(
        long masterSeed,
        IReadOnlyDictionary<RandomStreamName, ulong> states,
        IReadOnlyDictionary<RandomStreamName, long> drawCounts)
    {
        var streams = new Dictionary<RandomStreamName, DeterministicRandom>();
        foreach (var name in (RandomStreamName[])Enum.GetValues(typeof(RandomStreamName)))
        {
            streams[name] = new DeterministicRandom(states[name], drawCounts[name]);
        }

        return new NamedRandomStreams(masterSeed, streams);
    }

    public IReadOnlyDictionary<RandomStreamName, ulong> CaptureStates()
    {
        var result = new Dictionary<RandomStreamName, ulong>();
        foreach (var (name, stream) in _streams)
        {
            result[name] = stream.State;
        }

        return result;
    }

    public IReadOnlyDictionary<RandomStreamName, long> CaptureDrawCounts()
    {
        var result = new Dictionary<RandomStreamName, long>();
        foreach (var (name, stream) in _streams)
        {
            result[name] = stream.DrawCount;
        }

        return result;
    }
}
