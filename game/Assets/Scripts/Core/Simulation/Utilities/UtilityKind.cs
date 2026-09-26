// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Utilities;

/// <summary>The three utility service kinds plan §16 names for G6:
/// "utilities แบบ capacity/reach" -- water/power/waste service
/// coverage.</summary>
public enum UtilityKind
{
    Water,
    Power,
    Waste,
}
