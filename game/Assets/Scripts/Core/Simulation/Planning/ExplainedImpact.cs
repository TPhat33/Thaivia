// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;

namespace Thaivia.Core.Simulation.Planning;

/// <summary>
/// A predicted impact (spec §12) that carries BOTH its uncertainty band
/// (<see cref="Impact"/>, an <see cref="ImpactRange"/> -- never a bare
/// single number) AND the reasoning inputs that produced it
/// (<see cref="ReasoningInputs"/>) -- never presented as an opaque
/// confident figure a player has to trust blindly. This type never
/// carries a project's <c>FixedCostThb</c>: fixed cost stays a separate,
/// definite field on <see cref="ProjectDraft"/> precisely so a caller
/// cannot conflate "what this will definitely cost" with "what we predict
/// it will do" (see <see cref="ImpactRange"/>'s own doc comment for the
/// same discipline at the type-shape level).
/// </summary>
public readonly record struct ExplainedImpact(ImpactRange Impact, IReadOnlyList<ReasoningInput> ReasoningInputs)
{
    public double ExpectedValue => Impact.ExpectedValue;
    public double MinValue => Impact.MinValue;
    public double MaxValue => Impact.MaxValue;
    public string MetricName => Impact.MetricName;
}
