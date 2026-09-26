// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;

namespace Thaivia.Core.Simulation.Archetypes;

/// <summary>
/// Append-only, versioned registry of which <see cref="BuildingArchetype"/>
/// values existed at each content-catalog version, in a FIXED order that
/// is frozen forever the moment a version ships.
///
/// ADR-0038 supersedes the mechanism ADR-0030 section 4 flagged as a
/// footgun: the old <c>WorldState.AssignArchetype</c> computed
/// <c>hash(sourceId) % Enum.GetValues(typeof(BuildingArchetype)).Length</c>
/// -- every time an archetype was ADDED to the enum, that divisor grew,
/// which silently re-divides the ENTIRE hash keyspace and reassigns the
/// archetype of every previously-assigned building (the G6-04 wave hit
/// this for real: fixture ids 1000/3000 changed archetype under the new
/// modulus, and were "fixed" by renumbering the fixtures -- a symptom
/// fix, not a cause fix).
///
/// This type instead freezes ONE ordered list PER VERSION, forever. A
/// later version's list is a NEW, separately-frozen list (a prior
/// version's list plus new entries appended at the end) -- it is never a
/// mutation of an earlier version's list, and earlier entries never move.
/// <c>WorldState.AssignArchetype</c> always resolves against ONE
/// explicit, pinned version (<see cref="Simulation.WorldState.ArchetypeCatalogVersion"/>,
/// captured once per world and carried in every <see cref="Simulation.Save.SaveGame"/>),
/// never "whatever <see cref="BuildingArchetype"/> currently declares" --
/// so a building already assigned under version N keeps that assignment
/// forever, no matter how many versions ship after N. See
/// <c>ArchetypeCatalogGrowthTests</c> for the test proving this generically,
/// and <c>SaveLoadTests</c>/<c>StorylineAndUtilitySaveRoundTripTests</c>
/// for the save-level proof.
///
/// To add archetype(s) in a future wave: add the new
/// <see cref="BuildingArchetype"/> member(s), define a NEW
/// <c>V{n}</c> list here that is the PREVIOUS version's list with the new
/// member(s) appended at the end (never reordered, never removed from),
/// register it in <see cref="ByVersion"/>, and bump
/// <see cref="CurrentVersion"/>. Do not edit any existing V-list.
/// </summary>
public static class ArchetypeCatalog
{
    /// <summary>Version 1 -- the original G3 baseline (plan §4: "6-8").</summary>
    private static readonly IReadOnlyList<BuildingArchetype> V1 = new[]
    {
        BuildingArchetype.Residential,
        BuildingArchetype.LateNightFoodStreet,
        BuildingArchetype.SmallFactory,
        BuildingArchetype.Temple,
        BuildingArchetype.Market,
        BuildingArchetype.Office,
        BuildingArchetype.School,
        BuildingArchetype.Retail,
    };

    /// <summary>Version 2 -- the G6-04 expansion (ADR-0030): the same 8
    /// entries as V1, in the same order, with 4 more appended. This is
    /// also today's <see cref="CurrentVersion"/>, and its order/count
    /// exactly matches what the pre-ADR-0038 code computed (declaration
    /// order of <see cref="BuildingArchetype"/>, modulus 12) -- so this
    /// refactor changes no existing test's expected assignment.</summary>
    private static readonly IReadOnlyList<BuildingArchetype> V2 = BuildList(V1,
        BuildingArchetype.Hospital,
        BuildingArchetype.Hotel,
        BuildingArchetype.Warehouse,
        BuildingArchetype.ConvenienceStore);

    private static readonly IReadOnlyDictionary<int, IReadOnlyList<BuildingArchetype>> ByVersion =
        new Dictionary<int, IReadOnlyList<BuildingArchetype>>
        {
            [1] = V1,
            [2] = V2,
        };

    /// <summary>The catalog version every NEW <see cref="Simulation.WorldState"/>
    /// is seeded under. Bump this only together with adding a new frozen
    /// V-list above that appends to the previous one.</summary>
    public const int CurrentVersion = 2;

    /// <summary>Returns the frozen, ordered archetype list for
    /// <paramref name="version"/>. Throws
    /// <see cref="ArchetypeCatalogVersionUnknownException"/> for a version
    /// this build has never heard of (see that type's doc comment for why
    /// that must be a specific exception, not a generic
    /// <c>KeyNotFoundException</c>).</summary>
    public static IReadOnlyList<BuildingArchetype> ForVersion(int version)
    {
        if (!ByVersion.TryGetValue(version, out var list))
        {
            throw new ArchetypeCatalogVersionUnknownException(version, CurrentVersion);
        }

        return list;
    }

    private static IReadOnlyList<BuildingArchetype> BuildList(IReadOnlyList<BuildingArchetype> previous, params BuildingArchetype[] appended)
    {
        var list = new List<BuildingArchetype>(previous.Count + appended.Length);
        list.AddRange(previous);
        list.AddRange(appended);
        return list;
    }
}
