// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Mobility.Incidents;

/// <summary>Player-facing response/prevention levers (spec: "warnings/
/// cooldown/response/prevention"). Both are plain, pure functions -- a
/// lever is something the caller applies to a risk value or a threshold
/// set before/while calling <see cref="IncidentEngine.Step"/>, not a
/// hidden side effect.</summary>
public static class IncidentLevers
{
    /// <summary>A PREVENTION lever (e.g. "increase patrol presence"):
    /// reduces risk before it is evaluated, so a strong enough patrol can
    /// stop a Warning from ever escalating to Active (see
    /// IncidentEngineTests.PreventionLever_CanCancelAWarningBeforeItBecomesActive).
    /// <paramref name="patrolStrength"/> is in [0,1]; 0 = no effect, 1 =
    /// risk fully suppressed.</summary>
    public static double ApplyPatrol(double risk, double patrolStrength)
    {
        if (patrolStrength < 0 || patrolStrength > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(patrolStrength));
        }

        return risk * (1.0 - patrolStrength);
    }

    /// <summary>A RESPONSE lever (e.g. "faster dispatch"): shortens how
    /// long an ALREADY-ACTIVE incident lasts, by returning a reduced
    /// duration for <see cref="IncidentThresholds.DurationTicks"/>. Never
    /// reduces below 1 tick (an incident that is Active always occupies at
    /// least one tick -- it cannot be instantaneous).</summary>
    public static long ApplyFasterResponse(long durationTicks, double responseStrength)
    {
        if (responseStrength < 0 || responseStrength > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(responseStrength));
        }

        var reduced = (long)Math.Round(durationTicks * (1.0 - responseStrength));
        return Math.Max(1, reduced);
    }
}
