using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Accessibility;
using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.Planning;
using Thaivia.Core.Simulation.Save;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// G6-05: a small, named, finite road preset catalog (plan §9: "polyline
/// ใหม่ 2-3 control points... ไม่เริ่ม arbitrary Bezier editor") feeding
/// PlanningEngine's new-road-connector draft/commit flow.
/// </summary>
public class RoadPresetCatalogTests
{
    [Fact]
    public void ExactlyThreePresetsAreOffered()
    {
        Assert.Equal(3, RoadPresetCatalog.AllPresets.Count);
    }

    [Theory]
    [InlineData(RoadPreset.Soi, 1)]
    [InlineData(RoadPreset.Local, 2)]
    [InlineData(RoadPreset.Arterial, 4)]
    public void LaneCountMatchesTheDocumentedPreset(RoadPreset preset, int expectedLanes)
    {
        Assert.Equal(expectedLanes, RoadPresetCatalog.LaneCount(preset));
    }

    [Theory]
    [InlineData(RoadPreset.Soi)]
    [InlineData(RoadPreset.Local)]
    [InlineData(RoadPreset.Arterial)]
    public void CapacityUsesTheSamePerLaneConstantAsAnExistingOsmEdge(RoadPreset preset)
    {
        // Build a real RoadEdge with the SAME lane count as this preset,
        // via a `lanes` tag (the way LinkCapacity reads an existing OSM
        // way) and assert the two capacities agree exactly -- proving
        // the preset is not a separately-tuned number.
        var tags = new SourceTags(new System.Collections.Generic.Dictionary<string, string> { ["lanes"] = RoadPresetCatalog.LaneCount(preset).ToString() });
        var edge = new RoadEdge(
            wayId: 1,
            tags,
            new System.Collections.Generic.List<AssumptionRecord>(),
            new System.Collections.Generic.List<AssumptionRecord>(),
            new long[] { 1, 2 },
            new System.Collections.Generic.List<Vec2> { new(0, 0), new(1, 0) },
            new System.Collections.Generic.List<Vec2> { new(0, 0), new(1, 0) },
            OnewayDirection.No,
            layer: 0,
            bridge: false,
            tunnel: false,
            gradeSeparated: false,
            new System.Collections.Generic.Dictionary<string, string>(),
            fromNode: 1,
            toNode: 2);

        Assert.Equal(
            Thaivia.Core.Simulation.Mobility.Queues.LinkCapacity.BaseCapacityVehPerTick(edge),
            RoadPresetCatalog.CapacityVehPerTick(preset));
    }

    [Fact]
    public void ArterialHasStrictlyMoreCapacityThanLocalHasMoreThanSoi()
    {
        var soi = RoadPresetCatalog.CapacityVehPerTick(RoadPreset.Soi);
        var local = RoadPresetCatalog.CapacityVehPerTick(RoadPreset.Local);
        var arterial = RoadPresetCatalog.CapacityVehPerTick(RoadPreset.Arterial);
        Assert.True(soi < local, $"expected Soi ({soi}) < Local ({local})");
        Assert.True(local < arterial, $"expected Local ({local}) < Arterial ({arterial})");
    }

    [Fact]
    public void EveryPresetHasANonEmptyDisplayName()
    {
        foreach (var preset in RoadPresetCatalog.AllPresets)
        {
            Assert.False(string.IsNullOrWhiteSpace(RoadPresetCatalog.DisplayName(preset)));
        }
    }
}

/// <summary>PlanningEngine's new-road-connector flow actually carries the
/// chosen preset through commit into the registered PlannedRoadSegment
/// (not merely accepted-and-discarded).</summary>
public class NewRoadConnectorPresetFlowTests
{
    [Fact]
    public void DefaultPreset_WhenNotSpecified_IsLocal()
    {
        var world = SimulationFixtures.BuildWorldState();
        var draft = new NewRoadConnectorDraft("road-default", world.Revision, 100_000,
            PlanningEngine.EstimateNewRoadAccessImpact(world, SimulationFixtures.Node2, SimulationFixtures.Node3, SimulationFixtures.ResidentialBuildingId),
            SimulationFixtures.Node2, SimulationFixtures.Node3);

        Assert.Equal(RoadPreset.Local, draft.Preset);
    }

    [Theory]
    [InlineData(RoadPreset.Soi)]
    [InlineData(RoadPreset.Local)]
    [InlineData(RoadPreset.Arterial)]
    public void CommittedSegment_CarriesTheDraftsChosenPreset(RoadPreset preset)
    {
        var world = SimulationFixtures.BuildWorldState();
        var impact = PlanningEngine.EstimateNewRoadAccessImpact(world, SimulationFixtures.Node2, SimulationFixtures.Node3, SimulationFixtures.ResidentialBuildingId);
        var draft = new NewRoadConnectorDraft("road-" + preset, world.Revision, 100_000, impact,
            SimulationFixtures.Node2, SimulationFixtures.Node3, preset);

        var result = PlanningEngine.CommitNewRoadConnector(world, draft, LedgerAccountKind.Capex, new long[] { 100_000 });

        Assert.True(result.Success, string.Join("; ", result.Failures));
        var segment = Assert.Single(world.PlannedRoadSegments);
        Assert.Equal(preset, segment.Preset);
    }

    [Fact]
    public void SaveLoadRoundTrip_PreservesTheCommittedPreset()
    {
        var world = SimulationFixtures.BuildWorldState();
        var impact = PlanningEngine.EstimateNewRoadAccessImpact(world, SimulationFixtures.Node2, SimulationFixtures.Node3, SimulationFixtures.ResidentialBuildingId);
        var draft = new NewRoadConnectorDraft("road-arterial", world.Revision, 100_000, impact,
            SimulationFixtures.Node2, SimulationFixtures.Node3, RoadPreset.Arterial);
        PlanningEngine.CommitNewRoadConnector(world, draft, LedgerAccountKind.Capex, new long[] { 100_000 });

        var save = world.CaptureSave("map-x", "hash-x", "sim-1", "content-1");
        var json = SaveSerializer.Serialize(save);
        var reloadedSave = SaveSerializer.Deserialize(json);
        var reloaded = WorldState.Restore(reloadedSave, SimulationFixtures.BuildGeographyBase(), SimulationFixtures.BuildSimulationInitialization(), SimulationFixtures.BuildRoadGraph(), new Thaivia.Core.Simulation.Scenario.ScenarioConfig(42, 5_000_000));

        var segment = Assert.Single(reloaded.PlannedRoadSegments);
        Assert.Equal(RoadPreset.Arterial, segment.Preset);
    }

    /// <summary>A save file written before G6-05 (no "preset" field at
    /// all in its planned_road_segments entries) must still load, with
    /// the documented default rather than a load failure.</summary>
    [Fact]
    public void SaveMissingPresetField_LoadsWithDocumentedDefault_Local()
    {
        var world = SimulationFixtures.BuildWorldState();
        var impact = PlanningEngine.EstimateNewRoadAccessImpact(world, SimulationFixtures.Node2, SimulationFixtures.Node3, SimulationFixtures.ResidentialBuildingId);
        var draft = new NewRoadConnectorDraft("road-pre-g6-05", world.Revision, 100_000, impact,
            SimulationFixtures.Node2, SimulationFixtures.Node3, RoadPreset.Arterial);
        PlanningEngine.CommitNewRoadConnector(world, draft, LedgerAccountKind.Capex, new long[] { 100_000 });

        var save = world.CaptureSave("map-x", "hash-x", "sim-1", "content-1");
        var json = SaveSerializer.Serialize(save);

        // Simulate a pre-G6-05 save by stripping the "preset" field out
        // of the serialized JSON entirely, rather than merely trusting
        // the reader to accept it when present (that would not prove
        // backward compatibility for a file that genuinely lacks it).
        var withoutPreset = System.Text.RegularExpressions.Regex.Replace(json, ",\"preset\":\"[A-Za-z]+\"", "");
        Assert.DoesNotContain("\"preset\"", withoutPreset);

        var reloadedSave = SaveSerializer.Deserialize(withoutPreset);
        var reloaded = WorldState.Restore(reloadedSave, SimulationFixtures.BuildGeographyBase(), SimulationFixtures.BuildSimulationInitialization(), SimulationFixtures.BuildRoadGraph(), new Thaivia.Core.Simulation.Scenario.ScenarioConfig(42, 5_000_000));

        var segment = Assert.Single(reloaded.PlannedRoadSegments);
        Assert.Equal(RoadPreset.Local, segment.Preset);
    }
}
