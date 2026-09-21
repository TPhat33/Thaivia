// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;
using Thaivia.Core.Simulation.Archetypes;

namespace Thaivia.Core.Simulation.Noise;

/// <summary>
/// A 0-100 noise index OF THE GAME -- not decibels, not a real acoustic
/// measurement, and not derived from any source tag (spec §11: "noise
/// index 0-100 ของเกม"). It is a simulation_assumption-equivalent
/// scenario number, driven by each nearby source's
/// <see cref="ActivityClockCatalog"/> reading for the given hour and a
/// simple inverse-distance-squared decay, so the exact same archetype
/// contributes very differently at 07:00 vs 22:00 and at 20m vs 300m --
/// impact is always activity x time x location, never a fixed per-place
/// number (AGENTS.md rule 9).
/// </summary>
public static class NoiseIndex
{
    /// <summary>Distance (metres) at which a source's contribution has
    /// decayed to half its at-source value. A documented game-design
    /// constant, not a measured propagation model.</summary>
    public const double HalfDecayDistanceMeters = 150.0;

    public readonly record struct NoiseSource(BuildingArchetype Archetype, double LocalX, double LocalZ);

    public static int ComputeAt(double targetLocalX, double targetLocalZ, int hourOfDay, IEnumerable<NoiseSource> sources)
    {
        double total = 0;
        foreach (var source in sources)
        {
            var activity = ActivityClockCatalog.At(source.Archetype, hourOfDay);
            var dx = source.LocalX - targetLocalX;
            var dz = source.LocalZ - targetLocalZ;
            var distance = Math.Sqrt(dx * dx + dz * dz);
            var normalized = distance / HalfDecayDistanceMeters;
            var decay = 1.0 / (1.0 + normalized * normalized);
            total += activity.NoiseContribution * decay * 100.0;
        }

        return (int)Math.Clamp(Math.Round(total), 0, 100);
    }
}
