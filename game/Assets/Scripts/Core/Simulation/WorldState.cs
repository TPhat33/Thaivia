// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Thaivia.Core.MapPack;

namespace Thaivia.Core.Simulation;

/// <summary>
/// The in-session combination of an immutable GeographyBase (from a
/// loaded MapPack), its SimulationInitialization, and the growing list of
/// player-authored <see cref="PlayerDelta"/>s. WorldState *references*
/// GeographyBase and SimulationInitialization read-only; it has no method
/// that mutates either, and <see cref="AddDelta"/> only ever appends to
/// the PlayerDelta list. This is what "three layers never mix in one
/// structure" looks like at the point where the game actually needs all
/// three at once: one wrapper that keeps each layer in its own property,
/// never a merged dictionary/record.
/// </summary>
public sealed class WorldState
{
    private readonly List<PlayerDelta> _deltas = new();

    public WorldState(GeographyBase geographyBase, SimulationInitialization simulationInitialization)
    {
        GeographyBase = geographyBase;
        SimulationInitialization = simulationInitialization;
        Deltas = new ReadOnlyCollection<PlayerDelta>(_deltas);
    }

    public GeographyBase GeographyBase { get; }
    public SimulationInitialization SimulationInitialization { get; }
    public IReadOnlyList<PlayerDelta> Deltas { get; }

    /// <summary>Appends a player delta. There is deliberately no method on
    /// this class, or on GeographyBase, that lets a delta be folded back
    /// into GeographyBase's source-tag data -- a delta stays a delta.</summary>
    public void AddDelta(PlayerDelta delta) => _deltas.Add(delta);
}
