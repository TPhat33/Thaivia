using System;
using System.IO;
using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.Planning;
using Thaivia.Core.Simulation.Save;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

public class SaveLoadTests
{
    private const string MapId = "test-map";
    private const string MapContentHash = "sha256:deadbeef";

    [Fact]
    public void SaveThenRestore_ThenContinue_ProducesTheIdenticalHashAsNeverHavingSaved()
    {
        // Control run: never saved at all.
        var control = SimulationFixtures.BuildWorldState(masterSeed: 321);
        RunPhaseOne(control);
        RunPhaseTwo(control);
        var controlHash = control.ComputeStructuralHash();

        // Save/restore run: identical phase one, captured to a SaveGame,
        // restored into a brand new WorldState, then the identical phase
        // two continues from there.
        var beforeSave = SimulationFixtures.BuildWorldState(masterSeed: 321);
        RunPhaseOne(beforeSave);
        var save = beforeSave.CaptureSave(MapId, MapContentHash, "0.1.0", "0.1.0");

        var restored = WorldState.Restore(save, SimulationFixtures.BuildGeographyBase(), SimulationFixtures.BuildSimulationInitialization(), SimulationFixtures.BuildRoadGraph(), new Thaivia.Core.Simulation.Scenario.ScenarioConfig(321, 5_000_000));
        RunPhaseTwo(restored);
        var restoredHash = restored.ComputeStructuralHash();

        Assert.Equal(controlHash, restoredHash);
    }

    [Fact]
    public void JsonRoundTrip_PreservesEveryField()
    {
        var world = SimulationFixtures.BuildWorldState(masterSeed: 55);
        RunPhaseOne(world);
        var save = world.CaptureSave(MapId, MapContentHash, "0.1.0", "0.1.0");

        var json = SaveSerializer.Serialize(save);
        var roundTripped = SaveSerializer.Deserialize(json);

        Assert.Equal(save.MapId, roundTripped.MapId);
        Assert.Equal(save.MapContentHash, roundTripped.MapContentHash);
        Assert.Equal(save.CurrentTick, roundTripped.CurrentTick);
        Assert.Equal(save.Revision, roundTripped.Revision);
        Assert.Equal(save.MasterSeed, roundTripped.MasterSeed);
        Assert.Equal(save.Buildings.Count, roundTripped.Buildings.Count);
        Assert.Equal(save.Cohorts.Count, roundTripped.Cohorts.Count);
        Assert.Equal(save.Projects.Count, roundTripped.Projects.Count);

        var restored = WorldState.Restore(roundTripped, SimulationFixtures.BuildGeographyBase(), SimulationFixtures.BuildSimulationInitialization(), SimulationFixtures.BuildRoadGraph(), new Thaivia.Core.Simulation.Scenario.ScenarioConfig(55, 5_000_000));
        Assert.Equal(world.ComputeStructuralHash(), restored.ComputeStructuralHash());
    }

    [Fact]
    public void Store_LoadLatest_MapVersionMismatch_IsRejectedExplicitly_NotPartiallyLoaded()
    {
        var dir = MakeTempDir();
        var store = new SaveGameStore(dir);
        var world = SimulationFixtures.BuildWorldState();
        store.Save(world.CaptureSave(MapId, MapContentHash, "0.1.0", "0.1.0"));

        var result = store.LoadLatest("a-different-map-id", "sha256:different");

        var mismatch = Assert.IsType<LoadResult.MapVersionMismatch>(result);
        Assert.Equal(MapId, mismatch.SavedMapId);
        Assert.Equal(MapContentHash, mismatch.SavedMapContentHash);
        Assert.Equal("a-different-map-id", mismatch.InstalledMapId);
    }

    [Fact]
    public void Store_LoadLatest_MatchingMapVersion_Loads()
    {
        var dir = MakeTempDir();
        var store = new SaveGameStore(dir);
        var world = SimulationFixtures.BuildWorldState();
        RunPhaseOne(world);
        store.Save(world.CaptureSave(MapId, MapContentHash, "0.1.0", "0.1.0"));

        var result = store.LoadLatest(MapId, MapContentHash);

        var loaded = Assert.IsType<LoadResult.Loaded>(result);
        Assert.False(loaded.FellBackToPrevious);
        Assert.Equal(world.Clock.CurrentTick, loaded.Save.CurrentTick);
    }

    [Fact]
    public void Store_CorruptLatest_FallsBackToPrevious_AndSaysSo()
    {
        var dir = MakeTempDir();
        var store = new SaveGameStore(dir);

        var world1 = SimulationFixtures.BuildWorldState();
        store.Save(world1.CaptureSave(MapId, MapContentHash, "0.1.0", "0.1.0")); // becomes "previous" after the next save.

        var world2 = SimulationFixtures.BuildWorldState();
        RunPhaseOne(world2);
        store.Save(world2.CaptureSave(MapId, MapContentHash, "0.1.0", "0.1.0")); // "latest".

        // Corrupt "latest" only.
        File.WriteAllText(Path.Combine(dir, "save-latest.json"), "{ not valid json at all");

        var result = store.LoadLatest(MapId, MapContentHash);

        var loaded = Assert.IsType<LoadResult.Loaded>(result);
        Assert.True(loaded.FellBackToPrevious);
        Assert.Equal(0, loaded.Save.CurrentTick); // world1's state (never ticked), not world2's.
    }

    [Fact]
    public void Store_BothSlotsCorrupt_ReturnsCorrupt_NeverAPartialLoad()
    {
        var dir = MakeTempDir();
        File.WriteAllText(Path.Combine(dir, "save-latest.json"), "not json");
        File.WriteAllText(Path.Combine(dir, "save-previous.json"), "also not json");
        var store = new SaveGameStore(dir);

        var result = store.LoadLatest(MapId, MapContentHash);
        Assert.IsType<LoadResult.Corrupt>(result);
    }

    [Fact]
    public void Store_NoSaveYet_ReturnsNotFound()
    {
        var store = new SaveGameStore(MakeTempDir());
        Assert.IsType<LoadResult.NotFound>(store.LoadLatest(MapId, MapContentHash));
    }

    private static void RunPhaseOne(WorldState world)
    {
        for (var i = 0; i < 8; i++)
        {
            world.SimulateTick();
        }

        var impact = PlanningEngine.EstimateRelocationAccessImpact(world, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        var draft = new BuildingRelocationDraft("save-reloc", world.Revision, 200_000, impact, SimulationFixtures.ResidentialBuildingId, 22, 3, SimulationFixtures.Node4);
        PlanningEngine.CommitRelocation(world, draft, LedgerAccountKind.Capex, new long[] { 200_000 });
    }

    private static void RunPhaseTwo(WorldState world)
    {
        for (var i = 0; i < 12; i++)
        {
            world.SimulateTick();
        }

        PlanningEngine.PayMilestone(world, "save-reloc", 0);
    }

    private static string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "thaivia-save-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }
}
