// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.RandomStreams;

/// <summary>
/// A splitmix64-based deterministic PRNG (Steele, Lea &amp; Flood 2014;
/// the well-known public-domain splitmix64 mixing step). Not
/// cryptographic -- it is fast, has a trivially small/portable state (one
/// ulong), and is fully specified by that state plus a draw count, which
/// is exactly what a save file needs to restore a stream bit-for-bit (see
/// Thaivia.Core.Simulation.Save). Chosen over System.Random specifically
/// because System.Random's algorithm/implementation is not guaranteed
/// stable across .NET versions -- see docs/decisions for the ADR.
/// </summary>
public sealed class DeterministicRandom
{
    private ulong _state;

    public DeterministicRandom(ulong seed)
        : this(seed, 0)
    {
    }

    /// <summary>Restores a stream to an exact prior state -- used only by
    /// save/load. <paramref name="drawCount"/> is carried purely for
    /// reporting/testing (e.g. showing "this stream has drawn N values
    /// this session"); it does not affect the sequence, which is fully
    /// determined by <paramref name="state"/> alone.</summary>
    public DeterministicRandom(ulong state, long drawCount)
    {
        _state = state;
        DrawCount = drawCount;
    }

    /// <summary>How many values have been drawn from this stream since it
    /// was constructed (or since the state it was restored from).</summary>
    public long DrawCount { get; private set; }

    /// <summary>The raw internal state -- the entirety of what save/load
    /// needs to resume this stream exactly where it left off.</summary>
    public ulong State => _state;

    public ulong NextUInt64()
    {
        DrawCount++;
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>A double in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>An integer in [minInclusive, maxExclusive).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");
        }

        var range = (uint)(maxExclusive - minInclusive);
        var value = NextUInt64() % range;
        return minInclusive + (int)value;
    }

    /// <summary>One pure splitmix64 mixing step over an arbitrary input,
    /// with no stream state at all -- used where the codebase needs a
    /// small, stable, reproducible hash (e.g. deterministic archetype
    /// assignment from a building's source id) that must NOT consume or
    /// disturb any <see cref="DeterministicRandom"/> stream's position.
    /// Deliberately not <see cref="object.GetHashCode"/>, whose algorithm
    /// is not guaranteed stable across runs/.NET versions.</summary>
    public static ulong HashStep(ulong input)
    {
        var z = input + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
