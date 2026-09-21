using System;
using System.Linq;
using System.Reflection;
using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// G3 companion to ImmutabilityTests' G2 layer-separation checks (AGENTS.md
/// rule 3): every G3 simulation-layer type (WorldState's own new state --
/// BuildingSimState, HouseholdCohort, CommittedProject, ProjectDraft and
/// its subclasses, PlannedRoadSegment, SaveGame and its DTOs) must share
/// no base type with GeographyBase-layer types, and WorldState must never
/// reassign or mutate the SimulationInitialization/GeographyBase
/// references it was constructed with.
/// </summary>
public class LayerSeparationTests
{
    [Fact]
    public void NoSimulationNamespaceType_IsAssignableToOrFromAnyMapPackType()
    {
        var coreAssembly = typeof(GeographyBase).Assembly;
        var mapPackTypes = coreAssembly.GetTypes().Where(t => t.Namespace == "Thaivia.Core.MapPack" && t.IsClass).ToList();
        var simulationTypes = coreAssembly.GetTypes()
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("Thaivia.Core.Simulation", StringComparison.Ordinal))
            .Where(t => t.IsClass || t.IsValueType)
            .ToList();

        Assert.NotEmpty(mapPackTypes);
        Assert.NotEmpty(simulationTypes);

        var violations = new System.Collections.Generic.List<string>();
        foreach (var simType in simulationTypes)
        {
            foreach (var mapType in mapPackTypes)
            {
                if (mapType.IsAssignableFrom(simType) || simType.IsAssignableFrom(mapType))
                {
                    violations.Add($"{simType.FullName} <-> {mapType.FullName}");
                }
            }
        }

        Assert.True(violations.Count == 0, "Simulation-layer type shares a base with a GeographyBase-layer type: " + string.Join("; ", violations));
    }

    [Fact]
    public void WorldState_GeographyBaseAndSimulationInitialization_AreGetOnly_NeverReassignable()
    {
        var type = typeof(WorldState);
        var geoProp = type.GetProperty(nameof(WorldState.GeographyBase))!;
        var simProp = type.GetProperty(nameof(WorldState.SimulationInitialization))!;

        Assert.Null(geoProp.GetSetMethod(nonPublic: false));
        Assert.Null(simProp.GetSetMethod(nonPublic: false));
    }

    [Fact]
    public void WorldState_HasNoMethodThatWritesIntoSimulationInitializationOrGeographyBase()
    {
        var methodNames = typeof(WorldState).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();

        foreach (var suspicious in new[] { "SetGeographyBase", "SetSimulationInitialization", "MergeIntoGeographyBase", "ApplyToSource", "WriteToGeographyBase" })
        {
            Assert.DoesNotContain(suspicious, methodNames);
        }
    }

    [Fact]
    public void SimulationInitialization_ReferenceHeldByWorldState_IsTheSameInstanceThroughoutASession()
    {
        var simInit = SimulationFixtures.BuildSimulationInitialization();
        var world = new WorldState(SimulationFixtures.BuildGeographyBase(), simInit, SimulationFixtures.BuildRoadGraph(), Thaivia.Core.Simulation.Scenario.ScenarioConfig.Default(1));

        for (var i = 0; i < 5; i++)
        {
            world.SimulateTick();
        }

        Assert.Same(simInit, world.SimulationInitialization);
    }
}
