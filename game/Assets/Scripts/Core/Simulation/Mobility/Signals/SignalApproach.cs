// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Mobility.Signals;

/// <summary>One of the (currently always two, A/B) competing approaches at
/// a single intersection signal.</summary>
public enum SignalApproach
{
    A,
    B,
}
