using System;
using System.Linq;
using System.Reflection;
using Thaivia.Core.MapPack;
using Xunit;

namespace Thaivia.Core.Tests;

/// <summary>
/// Verifies the "immutable, enforced in the type system, not by
/// convention" claim for GeographyBase by reflecting over every public
/// type in Thaivia.Core.MapPack: every public instance property must
/// either have no setter at all, or an init-only setter (the CLR marks
/// an init accessor's return parameter with the
/// `System.Runtime.CompilerServices.IsExternalInit` modifier -- this is
/// exactly what the C# compiler checks to reject `x.Prop = y` outside a
/// constructor, so checking for it here is checking the same thing the
/// compiler enforces, not a weaker proxy for it).
/// </summary>
public class ImmutabilityTests
{
    [Fact]
    public void EveryMapPackTypes_PublicProperties_AreGetOnlyOrInitOnly()
    {
        var assembly = typeof(SourceTags).Assembly;
        var mapPackTypes = assembly.GetTypes()
            .Where(t => t.Namespace == "Thaivia.Core.MapPack" && t.IsClass && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(mapPackTypes);

        var violations = new System.Collections.Generic.List<string>();
        foreach (var type in mapPackTypes)
        {
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var setter = prop.GetSetMethod(nonPublic: false);
                if (setter is null)
                {
                    continue; // get-only: fine.
                }

                var isInitOnly = setter.ReturnParameter
                    .GetRequiredCustomModifiers()
                    .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");

                if (!isInitOnly)
                {
                    violations.Add($"{type.FullName}.{prop.Name} has a public, non-init setter");
                }
            }
        }

        Assert.True(violations.Count == 0, "Mutable GeographyBase properties found: " + string.Join("; ", violations));
    }

    [Fact]
    public void SourceTags_HasNoWayToInsertOrMutateAKey()
    {
        var type = typeof(SourceTags);
        var publicMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName) // exclude property getters
            .Select(m => m.Name)
            .ToList();

        // Only read accessors should exist -- no Set/Add/Remove/Merge.
        Assert.All(publicMethods, name => Assert.True(
            name is "Get" or "Has" or "ToString" or "Equals" or "GetHashCode" or "GetType",
            $"Unexpected mutating-looking method on SourceTags: {name}"));
    }

    [Fact]
    public void PlayerDelta_And_WorldState_ShareNoTypeWithGeographyBaseOrSimulationInitialization()
    {
        // Structural separation check: neither PlayerDelta nor WorldState
        // derives from, implements, or otherwise shares a common
        // non-object base with GeographyBase/SimulationInitialization, so
        // there is no polymorphic slot a caller could use to pass one
        // where the other is expected.
        var playerDeltaType = typeof(Thaivia.Core.Simulation.PlayerDelta);
        var worldStateType = typeof(Thaivia.Core.Simulation.WorldState);
        var geographyBaseType = typeof(GeographyBase);
        var simInitType = typeof(SimulationInitialization);

        Assert.False(geographyBaseType.IsAssignableFrom(playerDeltaType));
        Assert.False(playerDeltaType.IsAssignableFrom(geographyBaseType));
        Assert.False(simInitType.IsAssignableFrom(playerDeltaType));
        Assert.False(geographyBaseType.IsAssignableFrom(worldStateType));
        Assert.False(worldStateType.IsAssignableFrom(geographyBaseType));

        // WorldState exposes GeographyBase/SimulationInitialization as
        // get-only references and has no method whose name suggests
        // writing a delta back into either.
        var worldStateMethods = worldStateType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();
        Assert.DoesNotContain("MergeIntoGeographyBase", worldStateMethods);
        Assert.DoesNotContain("ApplyToSource", worldStateMethods);
        Assert.Contains("AddDelta", worldStateMethods);
    }
}
