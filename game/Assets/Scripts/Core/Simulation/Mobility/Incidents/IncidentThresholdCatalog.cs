// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Mobility.Incidents;

/// <summary>
/// The one place per-strand <see cref="IncidentThresholds"/> are written
/// down for LIVE, per-tick evaluation (see <see cref="WorldState"/>'s
/// SimulateTick, which is the only production caller). Every value here
/// is a documented simulation_assumption scenario number -- no measured
/// incident-rate data exists for any strand or any AOI (same discipline
/// as <see cref="IncidentThresholds"/>'s own doc comment). Kept separate
/// from <see cref="IncidentConditions"/>/<see cref="IncidentEngine"/> so
/// those two stay pure risk/state-machine primitives with zero scenario
/// tuning baked in -- only this catalog and callers of it know the actual
/// numbers.
/// </summary>
public static class IncidentThresholdCatalog
{
    public static readonly IncidentThresholds NightDisorder = new(
        RiskThreshold: 0.55,
        WarningLeadTicks: 15,
        DurationTicks: 30,
        CooldownTicks: 60);

    public static readonly IncidentThresholds StreetRacing = new(
        RiskThreshold: 0.6,
        WarningLeadTicks: 10,
        DurationTicks: 20,
        CooldownTicks: 60);

    /// <summary>G6-07. A shorter cooldown than the two G4 strands is
    /// deliberate (a construction backup can recur sooner than a
    /// nightlife-driven strand can), but still a real, nonzero floor --
    /// never per-tick.</summary>
    public static readonly IncidentThresholds RoadworksGridlock = new(
        RiskThreshold: 0.55,
        WarningLeadTicks: 8,
        DurationTicks: 15,
        CooldownTicks: 40);

    public static readonly IncidentThresholds IllegalWasteDumping = new(
        RiskThreshold: 0.6,
        WarningLeadTicks: 12,
        DurationTicks: 25,
        CooldownTicks: 80);

    public static IncidentThresholds For(IncidentStrand strand) => strand switch
    {
        IncidentStrand.NightDisorder => NightDisorder,
        IncidentStrand.StreetRacing => StreetRacing,
        IncidentStrand.RoadworksGridlock => RoadworksGridlock,
        IncidentStrand.IllegalWasteDumping => IllegalWasteDumping,
        _ => throw new ArgumentOutOfRangeException(nameof(strand), strand, "Unhandled incident strand."),
    };
}
