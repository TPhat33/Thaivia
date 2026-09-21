// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Mobility.Incidents;

/// <summary>
/// Pure, deterministic risk functions -- NO <see cref="System.Random"/> or
/// <see cref="Thaivia.Core.Simulation.RandomStreams.DeterministicRandom"/>
/// anywhere in this type. Every signal is a simulated CONDITION already
/// present elsewhere in the simulation (activity clock, noise index,
/// traffic congestion state), matching spec's requirement that incidents
/// are "ผูกกับสภาพจำลอง" (tied to simulated conditions), never a raw dice
/// roll. See <see cref="IncidentStrand"/>'s doc comment for the ethical
/// constraint this signature list enforces structurally: no archetype, no
/// place identity, no source id -- only generic environmental numbers.
/// </summary>
public static class IncidentConditions
{
    /// <summary>Risk in [0,1] that a night-disorder incident could begin at
    /// a site, from how late it is, how loud the area already is, and how
    /// quiet (low-traffic, i.e. few passers-by) the streets are right now.
    /// Deliberately takes no archetype/place-identity parameter.</summary>
    public static double NightDisorderRisk(int hourOfDay, int noiseIndex0To100, double congestionRatio0To1)
    {
        var nightFactor = LateNightFactor(hourOfDay);
        var noiseFactor = Math.Clamp(noiseIndex0To100 / 100.0, 0, 1);
        var quietStreetFactor = 1.0 - Math.Clamp(congestionRatio0To1, 0, 1);
        return Math.Clamp(nightFactor * 0.5 + noiseFactor * 0.3 + quietStreetFactor * 0.2, 0, 1);
    }

    /// <summary>Risk in [0,1] that a street-racing incident could begin, from
    /// how late it is and how empty (low-congestion, i.e. open road) the
    /// network currently is. Also takes no archetype/place-identity
    /// parameter.</summary>
    public static double StreetRacingRisk(int hourOfDay, double congestionRatio0To1)
    {
        var nightFactor = LateNightFactor(hourOfDay);
        var openRoadFactor = 1.0 - Math.Clamp(congestionRatio0To1, 0, 1);
        return Math.Clamp(nightFactor * 0.6 + openRoadFactor * 0.4, 0, 1);
    }

    /// <summary>1.0 at the dead of night (around 01:00-02:00), smoothly
    /// falling to ~0 by mid-morning and staying ~0 through the afternoon --
    /// a documented simulation_assumption shape, not measured incident
    /// data (none exists).</summary>
    private static double LateNightFactor(int hourOfDay)
    {
        if (hourOfDay < 0 || hourOfDay > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(hourOfDay));
        }

        const double peakHour = 1.5;
        const double widthHours = 3.5;
        var raw = Math.Abs(hourOfDay - peakHour);
        var circularDistance = Math.Min(raw, 24 - raw);
        return Math.Exp(-(circularDistance * circularDistance) / (2 * widthHours * widthHours));
    }
}
