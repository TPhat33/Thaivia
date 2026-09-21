// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;

namespace Thaivia.Core.Simulation.Archetypes;

/// <summary>
/// The activity clock for each <see cref="BuildingArchetype"/>: what
/// happens at which hour (spec §4/§11 "activity clock (what happens at
/// which hours) producing emissions/trips/service load"). Each archetype's
/// 24-hour profile is generated from a small, named "peak window" shape
/// (<see cref="DayProfile"/>) rather than 8x24 hand-typed magic numbers,
/// so the shape of each archetype's day is legible from its parameters.
/// These are simulation_assumption-equivalent scenario numbers -- fictional
/// by construction, never presented as measured data (see AGENTS.md rule 2/4).
/// </summary>
public static class ActivityClockCatalog
{
    private readonly record struct DayProfile(double PeakHour, double WidthHours, double PeakLevel, double Baseline);

    // One profile per archetype. PeakHour is the hour-of-day (0-23) where
    // NoiseContribution/TripGeneration/ServiceLoad are highest; WidthHours
    // controls how quickly activity falls off away from the peak;
    // Baseline is the always-present minimum level (never 0, since a
    // building is never "silent" in absolute terms even off-peak).
    private static readonly IReadOnlyDictionary<BuildingArchetype, DayProfile> Profiles = new Dictionary<BuildingArchetype, DayProfile>
    {
        // Two peaks would be more realistic (morning+evening) but a
        // single evening-weighted peak is enough signal for G3's
        // noise/access scenario and keeps the model easy to audit.
        [BuildingArchetype.Residential] = new DayProfile(PeakHour: 20, WidthHours: 5, PeakLevel: 0.55, Baseline: 0.15),
        [BuildingArchetype.LateNightFoodStreet] = new DayProfile(PeakHour: 22, WidthHours: 3, PeakLevel: 1.0, Baseline: 0.05),
        [BuildingArchetype.SmallFactory] = new DayProfile(PeakHour: 11, WidthHours: 5, PeakLevel: 0.75, Baseline: 0.10),
        // Temple: a low, calm baseline almost all day, with one narrow
        // scheduled-activity window (a festival/merit-making hour) --
        // never an elevated constant. See BuildingArchetype's doc comment
        // for why this shape is a hard ethical requirement, not a
        // tuning choice.
        [BuildingArchetype.Temple] = new DayProfile(PeakHour: 7, WidthHours: 1.5, PeakLevel: 0.35, Baseline: 0.05),
        [BuildingArchetype.Market] = new DayProfile(PeakHour: 8, WidthHours: 3, PeakLevel: 0.85, Baseline: 0.10),
        [BuildingArchetype.Office] = new DayProfile(PeakHour: 14, WidthHours: 4, PeakLevel: 0.60, Baseline: 0.05),
        [BuildingArchetype.School] = new DayProfile(PeakHour: 10, WidthHours: 3, PeakLevel: 0.70, Baseline: 0.05),
        [BuildingArchetype.Retail] = new DayProfile(PeakHour: 18, WidthHours: 4, PeakLevel: 0.65, Baseline: 0.10),
    };

    public static HourlyActivity At(BuildingArchetype archetype, int hourOfDay)
    {
        if (hourOfDay < 0 || hourOfDay > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(hourOfDay), "hourOfDay must be in [0, 23].");
        }

        var p = Profiles[archetype];
        var level = LevelAt(p, hourOfDay);

        // All three dimensions currently share the same day-shape but at
        // different scales -- distinct scaling per dimension (e.g. a
        // factory's ServiceLoad shape differing from its NoiseContribution
        // shape) is a plausible future refinement, not required this wave.
        return new HourlyActivity(
            NoiseContribution: level,
            TripGeneration: level * 0.9,
            ServiceLoad: level * 0.6);
    }

    private static double LevelAt(DayProfile p, int hourOfDay)
    {
        // Circular (wrap-around-midnight) distance from the peak hour, so
        // a peak at hour 22 still decays smoothly through hour 1-2 rather
        // than jumping back to baseline at midnight.
        var raw = Math.Abs(hourOfDay - p.PeakHour);
        var circularDistance = Math.Min(raw, 24 - raw);
        var falloff = Math.Exp(-(circularDistance * circularDistance) / (2 * p.WidthHours * p.WidthHours));
        return p.Baseline + (p.PeakLevel - p.Baseline) * falloff;
    }
}
