// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Graph;

/// <summary>
/// The disclosed, temporary gameplay-routing rule for
/// <see cref="TurnDecision.Unsupported"/> (owed from the G2 wave -- see
/// docs/decisions/0016-turn-decision-unsupported-gameplay-routing-rule.md).
/// <see cref="RoadGraphIndex.EvaluateTurn"/> already refuses to silently
/// treat an unsupported restriction (e.g. a via-way restriction the
/// pipeline's graph builder does not model) as "no restriction" -- it
/// returns a distinct third state instead of guessing. This type is the
/// single place that decides what a GAMEPLAY routing consumer (pathfinding,
/// accessibility, future bus/traffic routing) should actually DO with
/// that third state, so the decision lives in one reviewable, tested
/// place rather than being re-decided ad hoc at every call site.
///
/// THE RULE: <see cref="TurnDecision.Unsupported"/> is treated exactly
/// like <see cref="TurnDecision.Denied"/> for gameplay routing -- i.e.
/// fail SAFE (refuse the turn) rather than fail OPEN (allow it). This is
/// deliberately conservative and may over-restrict routes at restrictions
/// this codebase cannot yet fully model (e.g. via-way restrictions); it
/// is explicitly NOT claimed to be correct routing, only non-silent and
/// non-dangerous. A future router with real via-way-restriction support
/// should replace this fail-safe with an actual evaluation, at which
/// point this policy (and its ADR) should be revisited, not silently
/// bypassed.
/// </summary>
public static class GameplayTurnPolicy
{
    public static bool IsAllowedForGameplayRouting(TurnDecision decision) => decision == TurnDecision.Allowed;
}
