// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;

namespace Thaivia.Core.Simulation.Progression;

/// <summary>One area's evaluated status within a <see cref="CountryProgression"/>.</summary>
public readonly record struct EvaluatedArea(AreaDefinition Area, AreaProgressStatus Status);

/// <summary>
/// G6-10: a pure-C# model of which areas (plan §16's country/area-select
/// screen) are unlocked/in-progress/complete, reusing the SAME linear-
/// sequencing discipline <see cref="ScenarioProgression"/> already
/// proves for a single scenario's objectives (G5-05) -- an ORDERED list
/// evaluated headlessly, with no UI/Unity dependency and no rendering,
/// exactly like that type.
///
/// The player-facing "country screen" itself (G6-11) is Unity and stays
/// blocked (no Unity Editor in this environment, ADR-0002) -- this type
/// is the model it will read from once it exists. This split (pure model
/// vs. blocked render layer) mirrors how G3's ScenarioProgression shipped
/// before any Unity UI consumed it.
///
/// HARD RULE (the acceptance criterion this type exists to satisfy): an
/// area's lock state is a PURE FUNCTION of the OTHER areas' RECORDED
/// completion -- never of the current moment in real time, and never of
/// an un-auditable/implicit flag. Concretely:
///   - <see cref="Evaluate"/> takes <paramref name="completedAreaIds"/>/
///     <paramref name="startedAreaIds"/> as explicit, caller-supplied
///     sets (the "recorded completion" -- in practice, read from each
///     area's own persisted save / <see cref="ScenarioProgression.IsScenarioComplete"/>
///     result, never computed here);
///   - this class reads no clock API, no static/global mutable state,
///     and does no file/network I/O of its own, so there is nothing to
///     read from "outside" the two explicit parameters -- see
///     CountryProgressionTests' structural control, which greps this
///     type's own source file for any clock-API token and fails if one
///     is ever added;
///   - the SAME two input sets, in the SAME order, ALWAYS produce the
///     SAME output list -- proven directly by
///     <c>CountryProgressionTests.Evaluate_IsPureAndDeterministic</c>.
/// </summary>
public sealed class CountryProgression
{
    public CountryProgression(IReadOnlyList<AreaDefinition> areasInOrder)
    {
        if (areasInOrder is null || areasInOrder.Count == 0)
        {
            throw new ArgumentException("CountryProgression requires at least one area.", nameof(areasInOrder));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var area in areasInOrder)
        {
            if (!seen.Add(area.AreaId))
            {
                throw new ArgumentException($"Duplicate area id in CountryProgression: '{area.AreaId}'.", nameof(areasInOrder));
            }
        }

        AreasInOrder = areasInOrder;
    }

    public IReadOnlyList<AreaDefinition> AreasInOrder { get; }

    /// <summary>Pure: computes every area's status from exactly the two
    /// sets given, nothing else. The FIRST area in <see cref="AreasInOrder"/>
    /// is always at least Unlocked (a country needs one playable entry
    /// point); area N (for N&gt;=1) is Unlocked/InProgress/Completed only
    /// if area N-1 is Completed, otherwise Locked -- regardless of
    /// whether area N itself appears in either input set (a
    /// "completed-out-of-order" input, e.g. from a corrupted or hand-
    /// edited record, still renders that area as Locked if its
    /// predecessor is not complete; this method does not trust an area's
    /// own completion flag to imply its predecessors were legitimately
    /// played).</summary>
    public IReadOnlyList<EvaluatedArea> Evaluate(IReadOnlySet<string> completedAreaIds, IReadOnlySet<string>? startedAreaIds = null)
    {
        if (completedAreaIds is null)
        {
            throw new ArgumentNullException(nameof(completedAreaIds));
        }

        var results = new List<EvaluatedArea>(AreasInOrder.Count);
        var previousCompleted = true; // the first area's predecessor is vacuously "complete".

        foreach (var area in AreasInOrder)
        {
            if (!previousCompleted)
            {
                results.Add(new EvaluatedArea(area, AreaProgressStatus.Locked));
                previousCompleted = false; // stays locked for every subsequent area too.
                continue;
            }

            AreaProgressStatus status;
            if (completedAreaIds.Contains(area.AreaId))
            {
                status = AreaProgressStatus.Completed;
            }
            else if (startedAreaIds is not null && startedAreaIds.Contains(area.AreaId))
            {
                status = AreaProgressStatus.InProgress;
            }
            else
            {
                status = AreaProgressStatus.Unlocked;
            }

            results.Add(new EvaluatedArea(area, status));
            previousCompleted = status == AreaProgressStatus.Completed;
        }

        return results;
    }

    /// <summary>Convenience: whether the given area id is at least
    /// Unlocked (i.e. NOT Locked) under the given recorded completion --
    /// what a country-select screen needs to decide if an area is
    /// selectable at all.</summary>
    public bool IsAreaSelectable(string areaId, IReadOnlySet<string> completedAreaIds)
    {
        foreach (var evaluated in Evaluate(completedAreaIds))
        {
            if (string.Equals(evaluated.Area.AreaId, areaId, StringComparison.Ordinal))
            {
                return evaluated.Status != AreaProgressStatus.Locked;
            }
        }

        return false;
    }

    public bool IsCountryComplete(IReadOnlySet<string> completedAreaIds)
    {
        foreach (var evaluated in Evaluate(completedAreaIds))
        {
            if (evaluated.Status != AreaProgressStatus.Completed)
            {
                return false;
            }
        }

        return true;
    }
}
