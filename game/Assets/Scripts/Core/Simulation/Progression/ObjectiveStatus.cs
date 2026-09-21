// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Progression;

/// <summary>One objective's evaluated state within a
/// <see cref="ScenarioProgression"/> (see its doc comment for how these
/// are derived, in order, from a WorldState).</summary>
public enum ObjectiveStatus
{
    /// <summary>A prior objective in the sequence has not been completed
    /// yet -- this objective's check is never even evaluated (see
    /// ScenarioProgressionTests' spy-check control test).</summary>
    Locked,

    /// <summary>Every prior objective is Completed, but this one's check
    /// has not yet been satisfied.</summary>
    InProgress,

    Completed,
}
