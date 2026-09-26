using System.Text.Json.Nodes;
using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Archetypes;
using Thaivia.Core.Simulation.Save;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// Task Zero (wave 9, ADR-0038), spec §13's content-versioning
/// requirement: a save must state which archetype catalog version it was
/// created under, and loading a save whose version differs from what this
/// build knows must be handled EXPLICITLY -- a tested migration (an older,
/// still-known version) or a clear, actionable refusal (a version newer
/// than this build has ever heard of) -- never a silent partial load, and
/// never a confusing raw <see cref="System.Enum.Parse"/> crash.
/// </summary>
public class ArchetypeCatalogSaveVersioningTests
{
    private const string MapId = "test-map";
    private const string MapContentHash = "sha256:deadbeef";

    private static string SaveJsonWithArchetypeCatalogVersion(int version)
    {
        var world = SimulationFixtures.BuildWorldState(masterSeed: 909);
        var save = world.CaptureSave(MapId, MapContentHash, "0.1.0", "0.1.0");
        var json = SaveSerializer.Serialize(save);

        var node = JsonNode.Parse(json)!.AsObject();
        node["archetype_catalog_version"] = version;
        return node.ToJsonString();
    }

    [Fact]
    public void RestoringASave_WithAnArchetypeCatalogVersionNewerThanThisBuildKnows_ThrowsExplicitly_NotAPartialLoad()
    {
        var futureVersion = ArchetypeCatalog.CurrentVersion + 1;
        var json = SaveJsonWithArchetypeCatalogVersion(futureVersion);
        var save = SaveSerializer.Deserialize(json);
        Assert.Equal(futureVersion, save.ArchetypeCatalogVersion);

        var ex = Assert.Throws<ArchetypeCatalogVersionUnknownException>(() =>
            WorldState.Restore(save, SimulationFixtures.BuildGeographyBase(), SimulationFixtures.BuildSimulationInitialization(), SimulationFixtures.BuildRoadGraph(),
                new Thaivia.Core.Simulation.Scenario.ScenarioConfig(909, 5_000_000)));

        Assert.Equal(futureVersion, ex.RequestedVersion);
        Assert.Equal(ArchetypeCatalog.CurrentVersion, ex.HighestKnownVersion);

        // "Not a partial load": there is no half-built WorldState to
        // inspect at all -- the exception propagates before Restore
        // returns anything, and the version is rejected before any
        // building/cohort/project state is even touched (see the
        // ArchetypeCatalog.ForVersion call site's position at the very
        // top of WorldState's restore constructor).
    }

    [Fact]
    public void RestoringASave_FromArchetypeCatalogVersion1_LoadsCleanly_TestedMigrationIsTheIdentity()
    {
        // A "v1" save (from back when BuildingArchetype only had 8
        // members) loading on today's 12-member build needs no
        // transformation at all: every building's archetype is stored by
        // NAME, and every v1 name still exists in the current enum
        // forever (catalog growth only ever appends). This is the
        // "tested migration" side of spec §13's requirement -- proven
        // here, not just asserted in a comment.
        var json = SaveJsonWithArchetypeCatalogVersion(1);
        var save = SaveSerializer.Deserialize(json);
        Assert.Equal(1, save.ArchetypeCatalogVersion);

        var restored = WorldState.Restore(save, SimulationFixtures.BuildGeographyBase(), SimulationFixtures.BuildSimulationInitialization(), SimulationFixtures.BuildRoadGraph(),
            new Thaivia.Core.Simulation.Scenario.ScenarioConfig(909, 5_000_000));

        Assert.Equal(1, restored.ArchetypeCatalogVersion);

        // Every building kept EXACTLY the archetype the save recorded --
        // restoring under an older pinned catalog version never
        // re-derives archetypes from AssignArchetype, it reads them
        // verbatim from the save.
        foreach (var saved in save.Buildings)
        {
            Assert.Equal(saved.Archetype, restored.BuildingStates[saved.SourceId].Archetype.ToString());
        }
    }

    [Fact]
    public void RestoringASave_FromTodaysCurrentArchetypeCatalogVersion_RoundTripsTheVersionItself()
    {
        var world = SimulationFixtures.BuildWorldState(masterSeed: 909);
        Assert.Equal(ArchetypeCatalog.CurrentVersion, world.ArchetypeCatalogVersion);

        var save = world.CaptureSave(MapId, MapContentHash, "0.1.0", "0.1.0");
        var roundTripped = SaveSerializer.Deserialize(SaveSerializer.Serialize(save));
        var restored = WorldState.Restore(roundTripped, SimulationFixtures.BuildGeographyBase(), SimulationFixtures.BuildSimulationInitialization(), SimulationFixtures.BuildRoadGraph(),
            new Thaivia.Core.Simulation.Scenario.ScenarioConfig(909, 5_000_000));

        Assert.Equal(ArchetypeCatalog.CurrentVersion, restored.ArchetypeCatalogVersion);
    }
}
