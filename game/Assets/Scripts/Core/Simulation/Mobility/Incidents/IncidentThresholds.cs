// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Mobility.Incidents;

/// <summary>Timing configuration for one incident strand's state machine.
/// <see cref="RiskThreshold"/>/<see cref="WarningLeadTicks"/>/
/// <see cref="DurationTicks"/>/<see cref="CooldownTicks"/> are documented
/// simulation_assumption scenario numbers (no measured incident data
/// exists for this or any pilot AOI).</summary>
public readonly record struct IncidentThresholds(double RiskThreshold, long WarningLeadTicks, long DurationTicks, long CooldownTicks)
{
    /// <summary>The shortest possible time from one site re-entering
    /// Warning to it being allowed to re-enter Warning again: the full
    /// Warning+Active+Cooldown cycle. This is the hard floor on this
    /// site's incident RATE -- see IncidentEngineTests' bounded-rate proof,
    /// which checks the observed count against exactly this number.</summary>
    public long MinimumFullCycleTicks => WarningLeadTicks + DurationTicks + CooldownTicks;
}
