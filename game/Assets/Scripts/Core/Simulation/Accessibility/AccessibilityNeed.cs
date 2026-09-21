// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Accessibility;

/// <summary>Turns a network distance (from <see cref="AccessibilityGraph"/>
/// only -- never a straight-line distance) into a 0-100 need score. A
/// null distance (unreachable through the graph) scores 0 -- the worst
/// possible score, never treated as "fine" -- mirroring the "no data is
/// not empty/fine" discipline the GeographyBase layer uses for missing
/// source facts.</summary>
public static class AccessibilityNeed
{
    /// <summary>The network distance treated as "fully accessible" (score
    /// 100). A documented game-design constant, not a measured walking
    /// study.</summary>
    public const double ReferenceDistanceMeters = 500.0;

    public static int ComputeScore(double? networkDistanceMeters)
    {
        if (networkDistanceMeters is null)
        {
            return 0;
        }

        var score = 100.0 * Math.Max(0.0, 1.0 - networkDistanceMeters.Value / ReferenceDistanceMeters);
        return (int)Math.Clamp(Math.Round(score), 0, 100);
    }
}
