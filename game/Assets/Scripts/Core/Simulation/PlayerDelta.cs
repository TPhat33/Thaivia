// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation;

public enum PlayerDeltaKind
{
    NewRoadProposal,
    RoadRemovalProposal,
    BuildingRelocationProposal,
    BuildingDemolitionProposal,
    RoadWorksProposal,
}

/// <summary>
/// The PlayerDelta layer (AGENTS.md rule 3, layer 3): a single
/// player-authored change (a proposed new road, a proposed relocation,
/// ...) plus an optional simulation result once it has been evaluated.
/// This type shares no base type, interface, or field with
/// <see cref="Thaivia.Core.MapPack.GeographyBase"/> or
/// <see cref="Thaivia.Core.MapPack.SimulationInitialization"/> --
/// PlayerDeltas are collected separately by <see cref="WorldState"/> and
/// are never written back into a loaded MapPack (AGENTS.md rule 3: "never
/// send PlayerDelta back to OSM").
/// </summary>
public sealed class PlayerDelta
{
    public PlayerDelta(string id, PlayerDeltaKind kind, DateTimeOffset createdAt, string description)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("PlayerDelta.Id must not be empty.", nameof(id));
        }

        Id = id;
        Kind = kind;
        CreatedAt = createdAt;
        Description = description;
    }

    public string Id { get; }
    public PlayerDeltaKind Kind { get; }
    public DateTimeOffset CreatedAt { get; }
    public string Description { get; }
}
