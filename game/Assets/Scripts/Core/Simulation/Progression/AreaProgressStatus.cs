// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Progression;

/// <summary>One area's evaluated state within a
/// <see cref="CountryProgression"/> (G6-10). See that type's doc comment
/// for how these are derived from recorded completion, never from
/// wall-clock time.</summary>
public enum AreaProgressStatus
{
    /// <summary>Not yet reachable: the area before it in the ordered
    /// sequence has not been recorded complete. Never player-selectable
    /// -- a locked area must render as visibly locked, never silently
    /// playable (plan §16, G6-11's acceptance criteria).</summary>
    Locked,

    /// <summary>Reachable and selectable, but the player has not
    /// recorded any progress in it yet.</summary>
    Unlocked,

    /// <summary>Reachable, and the player has recorded progress (a save
    /// exists) but has not completed it.</summary>
    InProgress,

    Completed,
}
