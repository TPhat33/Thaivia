// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Archetypes;

/// <summary>
/// Thrown when a save (or any other caller) names an archetype catalog
/// version this build does not know how to interpret -- concretely, a
/// version GREATER than <see cref="ArchetypeCatalog.CurrentVersion"/> (a
/// save written by a NEWER build than the one trying to load it).
///
/// Mirrors the discipline <see cref="Thaivia.Core.Serialization.MapPackLoader"/>
/// already uses for schema/importer version mismatches
/// (<c>MapPackVersionMismatchException</c>): a specific, catchable,
/// actionable exception naming both the requested and the highest-known
/// version -- never a generic crash (e.g. an unhandled <c>Enum.Parse</c>
/// failure deep inside building restoration) and never a silently partial
/// load (spec §13's one unacceptable outcome). See
/// <c>WorldState.Restore</c>, which checks this BEFORE it starts
/// rebuilding any other state, so a version-mismatched save fails fast
/// with nothing partially constructed.
/// </summary>
public sealed class ArchetypeCatalogVersionUnknownException : Exception
{
    public int RequestedVersion { get; }
    public int HighestKnownVersion { get; }

    public ArchetypeCatalogVersionUnknownException(int requestedVersion, int highestKnownVersion)
        : base(
            $"Archetype catalog version {requestedVersion} is not known to this build " +
            $"(highest known version is {highestKnownVersion}). This save was very likely " +
            "written by a newer build. Loading it here would require either upgrading this " +
            "build or a tested migration, and neither exists yet -- refusing to load rather " +
            "than guessing at a mapping for an unknown archetype set.")
    {
        RequestedVersion = requestedVersion;
        HighestKnownVersion = highestKnownVersion;
    }
}
