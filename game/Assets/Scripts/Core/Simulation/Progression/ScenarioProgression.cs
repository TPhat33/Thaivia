// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;

namespace Thaivia.Core.Simulation.Progression;

/// <summary>One <see cref="ObjectiveDefinition"/> plus its evaluated
/// <see cref="ObjectiveStatus"/> against a particular WorldState.</summary>
public readonly record struct EvaluatedObjective(ObjectiveDefinition Objective, ObjectiveStatus Status);

/// <summary>
/// An ORDERED sequence of objectives (spec §19: tutorial/scenario
/// progression) evaluated headlessly against a <see cref="WorldState"/> --
/// every objective definition, completion condition and check is plain
/// data + logic, with no UI, no Unity, and no dependency on anything this
/// codebase cannot already test with `dotnet test` (ADR-0002/0003: no
/// Unity Editor, no real map data -- neither is needed here).
///
/// Sequencing rule: objective N+1 is only evaluated (i.e. only reaches
/// <see cref="ObjectiveStatus.InProgress"/> or
/// <see cref="ObjectiveStatus.Completed"/>) once objective N is
/// <see cref="ObjectiveStatus.Completed"/> -- everything after the first
/// not-yet-completed objective is <see cref="ObjectiveStatus.Locked"/>
/// and its <see cref="IObjectiveCheck.IsSatisfied"/> is never even called
/// (a real, testable guarantee -- see ScenarioProgressionTests' spy-check
/// control, which would throw if a locked check were evaluated). This
/// models a linear tutorial ("do step 1, then step 2 unlocks..."); a
/// scenario with independent (non-sequential) objectives can simply put
/// each one in its own single-objective ScenarioProgression instead.
/// </summary>
public sealed class ScenarioProgression
{
    public ScenarioProgression(IReadOnlyList<ObjectiveDefinition> objectivesInOrder)
    {
        if (objectivesInOrder is null || objectivesInOrder.Count == 0)
        {
            throw new ArgumentException("ScenarioProgression requires at least one objective.", nameof(objectivesInOrder));
        }

        ObjectivesInOrder = objectivesInOrder;
    }

    public IReadOnlyList<ObjectiveDefinition> ObjectivesInOrder { get; }

    /// <summary>Pure: never mutates <paramref name="world"/> (beyond
    /// whatever an individual IObjectiveCheck implementation does --
    /// every check shipped in this namespace, see
    /// <see cref="Checks"/>, is itself read-only).</summary>
    public IReadOnlyList<EvaluatedObjective> Evaluate(WorldState world)
    {
        var results = new List<EvaluatedObjective>(ObjectivesInOrder.Count);
        var reachedFirstIncomplete = false;

        foreach (var objective in ObjectivesInOrder)
        {
            if (reachedFirstIncomplete)
            {
                results.Add(new EvaluatedObjective(objective, ObjectiveStatus.Locked));
                continue;
            }

            var satisfied = objective.Check.IsSatisfied(world);
            if (satisfied)
            {
                results.Add(new EvaluatedObjective(objective, ObjectiveStatus.Completed));
            }
            else
            {
                results.Add(new EvaluatedObjective(objective, ObjectiveStatus.InProgress));
                reachedFirstIncomplete = true;
            }
        }

        return results;
    }

    /// <summary>The whole scenario is complete once every objective in
    /// the sequence evaluates as Completed.</summary>
    public bool IsScenarioComplete(WorldState world)
    {
        foreach (var evaluated in Evaluate(world))
        {
            if (evaluated.Status != ObjectiveStatus.Completed)
            {
                return false;
            }
        }

        return true;
    }
}
