using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Thaivia.Core.Simulation.Archetypes;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// G6-04 ethical control (AGENTS.md rule 9; plan §11 "ประโยชน์และผล
/// กระทบขึ้นกับ activity/time/location ไม่ตั้งศาสนสถานหรือชุมชนใดเป็น
/// ปัญหาในตัวเอง"): impact must be a function of activity x time x
/// location, never a fixed per-archetype number. `BuildingArchetype`'s
/// doc comment states this, but a comment cannot stop a future author
/// from adding `Dictionary&lt;BuildingArchetype, double&gt; RiskScore`
/// somewhere in this namespace. This test makes that structurally
/// impossible to slip past CI: it reflects over every type in the
/// `Thaivia.Core.Simulation.Archetypes` namespace and fails if ANY field
/// maps a `BuildingArchetype` key directly to a bare numeric scalar
/// (double/float/int/decimal) -- the one shape a "this archetype is
/// worth -5 happiness always" table would have to take. The one field
/// that legitimately maps `BuildingArchetype` to something
/// (`ActivityClockCatalog`'s `Profiles`) is fine because its value type
/// is a structured `DayProfile`/`HourlyActivity`-shaped record that
/// itself requires an hour-of-day to resolve to a number -- exactly the
/// activity x time dependency the rule requires.
///
/// Mirrors the codebase's existing reflection-based structural tests
/// (e.g. IncidentEngineTests.EthicalControl_NoIncidentsApiTakesABuildingArchetypeParameter,
/// ImmutabilityTests, LayerSeparationTests).
/// </summary>
public class BuildingArchetypeEthicalControlTests
{
    private static readonly HashSet<Type> ForbiddenBareScalarTypes = new()
    {
        typeof(double), typeof(float), typeof(int), typeof(long), typeof(decimal),
        typeof(double?), typeof(float?), typeof(int?), typeof(long?), typeof(decimal?),
    };

    [Fact]
    public void EthicalControl_NoArchetypeCarriesAStaticScalarScoreIndependentOfActivityClock()
    {
        var archetypeNamespaceTypes = typeof(BuildingArchetype).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(BuildingArchetype).Namespace);

        var violations = new List<string>();

        foreach (var type in archetypeNamespaceTypes)
        {
            var members = type
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Select(f => (Name: f.Name, Type: f.FieldType))
                .Concat(type
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                    .Select(p => (Name: p.Name, Type: p.PropertyType)));

            foreach (var (name, memberType) in members)
            {
                if (!TryGetArchetypeKeyedValueType(memberType, out var valueType))
                {
                    continue;
                }

                if (ForbiddenBareScalarTypes.Contains(valueType))
                {
                    violations.Add(
                        $"{type.FullName}.{name} maps BuildingArchetype directly to a bare {valueType.Name} scalar. "
                        + "Impact must be a function of activity x time x location (AGENTS.md rule 9), never a fixed "
                        + "per-archetype number. Route it through ActivityClockCatalog/HourlyActivity instead.");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>Positive control: this test's own detector must actually
    /// fire on the shape it claims to forbid, not just pass vacuously
    /// because no such member currently exists anywhere in the
    /// namespace.</summary>
    [Fact]
    public void DetectorItself_FlagsAKnownBadShape()
    {
        var found = TryGetArchetypeKeyedValueType(typeof(Dictionary<BuildingArchetype, double>), out var valueType);
        Assert.True(found);
        Assert.Equal(typeof(double), valueType);
        Assert.Contains(valueType, ForbiddenBareScalarTypes);
    }

    /// <summary>Negative control: the detector must NOT flag the
    /// legitimate shape (BuildingArchetype -&gt; a structured, time-
    /// dependent record), so the positive test above is discriminating,
    /// not just "reject every dictionary".</summary>
    [Fact]
    public void DetectorItself_DoesNotFlagATimeDependentStructuredValue()
    {
        var found = TryGetArchetypeKeyedValueType(typeof(Dictionary<BuildingArchetype, HourlyActivity>), out var valueType);
        Assert.True(found);
        Assert.DoesNotContain(valueType, ForbiddenBareScalarTypes);
    }

    private static bool TryGetArchetypeKeyedValueType(Type candidate, out Type valueType)
    {
        valueType = null!;
        if (!candidate.IsGenericType)
        {
            return false;
        }

        var genericDef = candidate.GetGenericTypeDefinition();
        if (genericDef != typeof(Dictionary<,>) && genericDef != typeof(IReadOnlyDictionary<,>) && genericDef != typeof(IDictionary<,>))
        {
            return false;
        }

        var args = candidate.GetGenericArguments();
        if (args[0] != typeof(BuildingArchetype))
        {
            return false;
        }

        valueType = args[1];
        return true;
    }
}
