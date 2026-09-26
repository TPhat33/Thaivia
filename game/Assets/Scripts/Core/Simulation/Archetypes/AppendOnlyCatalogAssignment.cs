// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;
using Thaivia.Core.Simulation.RandomStreams;

namespace Thaivia.Core.Simulation.Archetypes;

/// <summary>
/// The generic, type-agnostic mechanism <see cref="ArchetypeCatalog"/> is
/// built on: a pure, deterministic hash-then-index lookup into ONE
/// explicit, caller-supplied ordered list. Extracted on its own (rather
/// than inlined into <c>WorldState.AssignArchetype</c>) for two reasons:
///
///   1. It makes the actual bug this type's sibling (<see cref="ArchetypeCatalog"/>)
///      fixes reusable for anything else that ever needs a stable,
///      append-only id -&gt; category assignment, not just building
///      archetypes.
///   2. It gives <c>ArchetypeCatalogGrowthTests</c> a seam to prove the
///      GENERAL mathematical property ("assigning against a catalog list
///      that has since grown a new entry never changes what an id already
///      assigned against the OLD, shorter list would get, as long as you
///      keep resolving against that same old list") without needing to
///      add a throwaway member to the real, reviewed
///      <see cref="Simulation.Archetypes.BuildingArchetype"/> enum just to
///      exercise growth in a test.
///
/// This method itself does NOT solve catalog growth by itself -- calling
/// it with a list that has grown (bigger <c>Count</c>) than the one used
/// last time DOES reshuffle results, exactly like the hash-mod-length
/// scheme this whole type supersedes (see ADR-0038, which supersedes
/// ADR-0030 section 4). The growth-safety guarantee comes from
/// <see cref="ArchetypeCatalog"/> NEVER mutating an already-shipped
/// version's list and from <c>WorldState.ArchetypeCatalogVersion</c>
/// pinning which frozen list a given world/save resolves against forever
/// -- this method is just the shared, testable primitive both of those
/// build on.
/// </summary>
public static class AppendOnlyCatalogAssignment
{
    /// <summary>Deterministic, pure hash-to-index assignment -- NOT an RNG
    /// draw, so it never depends on call order relative to other RNG
    /// consumption (mirrors <see cref="DeterministicRandom.HashStep"/>'s
    /// doc comment). Throws for an empty catalog (there is no valid
    /// assignment) rather than dividing by zero.</summary>
    public static T Assign<T>(long sourceId, IReadOnlyList<T> orderedCatalog)
    {
        if (orderedCatalog is null || orderedCatalog.Count == 0)
        {
            throw new ArgumentException("orderedCatalog must contain at least one entry.", nameof(orderedCatalog));
        }

        var mixed = DeterministicRandom.HashStep(unchecked((ulong)sourceId));
        return orderedCatalog[(int)(mixed % (ulong)orderedCatalog.Count)];
    }
}
