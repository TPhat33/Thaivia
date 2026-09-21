// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Planning;

/// <summary>One named, numeric input that fed into an
/// <see cref="ImpactRange"/> prediction -- e.g.
/// ("current_network_distance_m", 620.0, "m"). Spec §12 requires a
/// committed project's predicted impact to carry not just its
/// uncertainty (which <see cref="ImpactRange"/> already does) but the
/// REASONING that produced it, so a UI or a test can show WHY a number is
/// what it is, not present it as an opaque confident figure. Every value
/// here is something the prediction actually read (see
/// <see cref="PlanningEngine"/>'s Explain* methods) -- never a post-hoc
/// justification invented after the fact.</summary>
public readonly record struct ReasoningInput(string Name, double Value, string Unit);
