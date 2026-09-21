// Pure C# -- no UnityEngine reference. See game/README.md.
using Thaivia.Core.Simulation.RandomStreams;

namespace Thaivia.Core.Simulation.Mobility.Incidents;

/// <summary>
/// Advances one <see cref="IncidentSite"/> by exactly one tick. Whether a
/// site transitions Idle -&gt; Warning -&gt; Active is decided ENTIRELY by
/// <paramref name="risk"/> (a value <see cref="IncidentConditions"/>
/// computed from simulated conditions) compared against
/// <see cref="IncidentThresholds.RiskThreshold"/> -- <paramref name="severityStream"/>
/// (the <see cref="RandomStreamName.Incidents"/> stream) is drawn from
/// EXACTLY ONCE, only at the instant an incident becomes Active, only to
/// pick a severity value, and never influences whether or when that
/// transition happens. See IncidentEngineTests' determinism test, which
/// runs the same condition sequence through two different RNG seeds and
/// asserts the phase sequence is identical while only severity differs --
/// the structural proof that triggering is condition-driven, not
/// randomness-driven.
/// </summary>
public static class IncidentEngine
{
    public static void Step(IncidentSite site, double risk, IncidentThresholds thresholds, DeterministicRandom? severityStream = null)
    {
        switch (site.Phase)
        {
            case IncidentPhase.Idle:
                if (risk >= thresholds.RiskThreshold)
                {
                    site.TransitionTo(IncidentPhase.Warning);
                    site.RecordWarningIssued();
                }

                break;

            case IncidentPhase.Warning:
                if (risk < thresholds.RiskThreshold)
                {
                    // The condition that triggered this warning subsided
                    // (or a prevention lever pushed it down) before the
                    // lead time elapsed -- a "positive" resolution: no
                    // incident happens at all (spec CD7: "มีเรื่องบวก").
                    site.TransitionTo(IncidentPhase.Idle);
                    break;
                }

                if (site.TicksInPhase + 1 >= thresholds.WarningLeadTicks)
                {
                    var severity = severityStream is null ? 50 : 1 + (int)(severityStream.NextUInt64() % 100);
                    site.TransitionTo(IncidentPhase.Active);
                    site.RecordIncidentTriggered(severity);
                }
                else
                {
                    site.AdvancePhaseTick();
                }

                break;

            case IncidentPhase.Active:
                if (site.TicksInPhase + 1 >= thresholds.DurationTicks)
                {
                    site.TransitionTo(IncidentPhase.Cooldown);
                }
                else
                {
                    site.AdvancePhaseTick();
                }

                break;

            case IncidentPhase.Cooldown:
                // While in Cooldown, risk is not even consulted -- no new
                // warning can start at this site regardless of how high
                // risk is, which is what makes the cooldown a hard rate
                // bound rather than a soft suggestion.
                if (site.TicksInPhase + 1 >= thresholds.CooldownTicks)
                {
                    site.TransitionTo(IncidentPhase.Idle);
                }
                else
                {
                    site.AdvancePhaseTick();
                }

                break;
        }
    }
}
