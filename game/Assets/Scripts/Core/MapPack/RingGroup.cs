// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;

namespace Thaivia.Core.MapPack;

/// <summary>One outer ring plus its holes (a single polygon of a possibly
/// multi-polygon feature), mirroring the pipeline's `ring_groups_*`
/// entries.</summary>
public sealed class RingGroup
{
    public RingGroup(IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>> holes)
    {
        Outer = outer;
        Holes = holes;
    }

    public IReadOnlyList<Vec2> Outer { get; }

    public IReadOnlyList<IReadOnlyList<Vec2>> Holes { get; }
}
