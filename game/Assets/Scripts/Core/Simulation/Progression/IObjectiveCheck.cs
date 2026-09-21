// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Progression;

/// <summary>A pure completion check for one <see cref="ObjectiveDefinition"/>
/// (spec §19: tutorial/scenario progression as data + logic, testable
/// headlessly -- no UI dependency of any kind here). Implementations must
/// never mutate <c>world</c>: a check is read-only by contract, the same
/// discipline PlanningEngine's Estimate* methods already follow (see
/// ScenarioProgressionTests for a test that runs Evaluate and asserts the
/// world's structural hash is unchanged).</summary>
public interface IObjectiveCheck
{
    bool IsSatisfied(WorldState world);
}
