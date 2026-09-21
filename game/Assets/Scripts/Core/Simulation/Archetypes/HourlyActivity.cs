// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Archetypes;

/// <summary>One archetype's activity level for one hour of the game day,
/// each component in [0, 1]. All three are simulation-layer numbers by
/// construction (never a source fact) -- an archetype's profile is a
/// scenario design choice, not a measurement of any real place.</summary>
public readonly record struct HourlyActivity(double NoiseContribution, double TripGeneration, double ServiceLoad);
