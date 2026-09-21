// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Mobility.Signals;

/// <summary>Which <see cref="ISignalPlan"/> a <see cref="SignalInstance"/>
/// runs -- persisted alongside its config so a save can rebuild the exact
/// same plan object on restore.</summary>
public enum SignalPlanKind
{
    Fixed,
    Adaptive,
}
