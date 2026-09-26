using System.Collections.Generic;
using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Cohorts;
using Thaivia.Core.Simulation.Scenario;
using Thaivia.Core.Simulation.Utilities;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>Pure UtilityCoverage formula tests -- no WorldState involved.</summary>
public class UtilityCoverageFormulaTests
{
    [Fact]
    public void ReachScore_IsFullAtZeroDistance()
    {
        Assert.Equal(100, UtilityCoverage.ComputeReachScore(0.0));
    }

    [Fact]
    public void ReachScore_IsZeroAtOrBeyondReferenceDistance()
    {
        Assert.Equal(0, UtilityCoverage.ComputeReachScore(UtilityCoverage.ReferenceDistanceMeters));
        Assert.Equal(0, UtilityCoverage.ComputeReachScore(UtilityCoverage.ReferenceDistanceMeters * 10));
    }

    [Fact]
    public void ReachScore_IsZeroWhenUnreachable()
    {
        Assert.Equal(0, UtilityCoverage.ComputeReachScore(null));
    }

    [Fact]
    public void UtilizationFraction_ZeroCapacityWithDemand_IsPositiveInfinity_NeverTreatedAsFree()
    {
        Assert.True(double.IsPositiveInfinity(UtilityCoverage.UtilizationFraction(demandUnitsThisTick: 5, capacityUnitsPerTick: 0)));
        Assert.Equal(0.0, UtilityCoverage.UtilizationFraction(demandUnitsThisTick: 0, capacityUnitsPerTick: 0));
    }

    [Theory]
    [InlineData(5, 10, false)]
    [InlineData(10, 10, false)]
    [InlineData(11, 10, true)]
    public void IsExhausted_MatchesDemandVsCapacity(long demand, int capacity, bool expectedExhausted)
    {
        Assert.Equal(expectedExhausted, UtilityCoverage.IsExhausted(demand, capacity));
    }

    [Fact]
    public void ComputeScore_UnderCapacity_ReportsFullReachScore_NoPenalty()
    {
        var underCapacity = UtilityCoverage.ComputeScore(networkDistanceMeters: 0, demandUnitsThisTick: 5, capacityUnitsPerTick: 10);
        Assert.Equal(100, underCapacity);
    }

    [Fact]
    public void ComputeScore_OverCapacity_DegradesBelowTheReachOnlyScore()
    {
        var reachOnly = UtilityCoverage.ComputeReachScore(0.0);
        var overCapacity = UtilityCoverage.ComputeScore(networkDistanceMeters: 0, demandUnitsThisTick: 40, capacityUnitsPerTick: 10);
        Assert.True(overCapacity < reachOnly, $"expected over-capacity score ({overCapacity}) < reach-only score ({reachOnly})");
    }

    [Fact]
    public void ComputeScore_ZeroCapacityWithAnyDemand_IsZero_NeverUnlimited()
    {
        Assert.Equal(0, UtilityCoverage.ComputeScore(networkDistanceMeters: 0, demandUnitsThisTick: 1, capacityUnitsPerTick: 0));
    }

    [Fact]
    public void ComputeScore_UnreachableSource_IsZeroRegardlessOfCapacity()
    {
        Assert.Equal(0, UtilityCoverage.ComputeScore(networkDistanceMeters: null, demandUnitsThisTick: 0, capacityUnitsPerTick: 1000));
    }
}

/// <summary>WorldState-level integration: real GeographyBase + RoadGraph,
/// no mocked distance/demand numbers.</summary>
public class UtilityCoverageWorldStateTests
{
    private static PolygonFeature BuildBuilding(long sourceId, string use, double cx, double cz)
    {
        var outer = new List<Vec2>
        {
            new(cx - 1, cz - 1),
            new(cx + 1, cz - 1),
            new(cx + 1, cz + 1),
            new(cx - 1, cz + 1),
        };
        var ringGroup = new RingGroup(outer, new List<IReadOnlyList<Vec2>>());
        var tags = new SourceTags(new Dictionary<string, string> { ["building"] = use });
        return new PolygonFeature("building", "way", sourceId, tags, new List<AssumptionRecord>(), new List<AssumptionRecord>(),
            new List<RingGroup> { ringGroup }, new List<RingGroup> { ringGroup });
    }

    private static GeographyBase OneBuilding(long sourceId, string use, double cx, double cz) =>
        new(new List<PolygonFeature> { BuildBuilding(sourceId, use, cx, cz) },
            new List<PolygonFeature>(), new List<PolygonFeature>(), new List<LineFeature>(), new List<LineFeature>());

    private static SimulationInitialization SimInit() => SimulationFixtures.BuildSimulationInitialization();

    /// <summary>ADVERSARIAL FIXTURE (G6-06): reuses
    /// SimulationFixtures.BuildDetourRoadGraph, the SAME fixture
    /// AccessibilityGraphTests uses to prove network distance != straight
    /// line -- a 2m straight-line gap that is really a 502m network
    /// detour. A building sits exactly at the straight-line-close bank; a
    /// water source sits at the far bank. A Euclidean stand-in (2m) would
    /// score this near 100 (fully covered); the real network distance
    /// (502m, beyond UtilityCoverage.ReferenceDistanceMeters=400m) scores
    /// it 0 -- a materially different answer, proving the coverage
    /// computation actually walks the graph.</summary>
    [Fact]
    public void AdversarialFixture_NetworkReachDiffersMateriallyFromStraightLineDistance()
    {
        // 2003 hashes to BuildingArchetype.Office (see SimulationFixtures'
        // own control test) -- no cohort needed for a reach-only check.
        const long buildingId = 2003;
        var geography = OneBuilding(buildingId, "office", cx: 0, cz: 0); // exactly at DetourBankANode's coords.
        var roadGraph = SimulationFixtures.BuildDetourRoadGraph();
        var world = new WorldState(geography, SimInit(), roadGraph, new ScenarioConfig(1, 5_000_000));

        world.AddUtilitySource(new UtilitySource(UtilityKind.Water, SimulationFixtures.DetourBankBNode, capacityUnitsPerTick: 1000));

        var graph = world.BuildAccessibilityGraph();
        var networkDistance = graph.ShortestDistanceMeters(SimulationFixtures.DetourBankANode, SimulationFixtures.DetourBankBNode);
        Assert.NotNull(networkDistance);
        Assert.Equal(502.0, networkDistance!.Value); // sanity: same fixture number AccessibilityGraphTests documents.

        var straightLineDistance = 2.0; // the fixture's own straight-line gap, per its doc comment.
        var euclideanStandInScore = UtilityCoverage.ComputeReachScore(straightLineDistance);
        var realScore = world.ComputeUtilityCoverageScore(buildingId, UtilityKind.Water);

        Assert.True(euclideanStandInScore > 90, $"sanity: a Euclidean stand-in over 2m should score near-full, got {euclideanStandInScore}");
        Assert.Equal(0, realScore); // 502m > 400m reference -> zero, per the REAL network distance.
        Assert.True(realScore < euclideanStandInScore - 50, "the real (network) score must be materially lower than a straight-line stand-in would report");
    }

    /// <summary>Capacity exhaustion, proven the same way GatewayFlow/
    /// LinkQueueSimulator's tests do: identical geometry/demand, only the
    /// source's capacity differs, and the ample-capacity world scores
    /// strictly higher than the scarce-capacity world.</summary>
    [Fact]
    public void CapacityExhaustion_DegradesCoverageComparedToAmpleCapacity_SameGeometryAndDemand()
    {
        const long residentialBuildingId = 3013; // hashes to BuildingArchetype.Residential (see BudgetConstrainedTradeOffTests' control comment).
        var geography = OneBuilding(residentialBuildingId, "residential", cx: 12, cz: 0); // exactly at Node3.
        var roadGraph = SimulationFixtures.BuildRoadGraph();
        // Fixed household count (min == max == 5) so demand is an exact, deterministic number, not RNG-range-dependent.
        var scenario = new ScenarioConfig(1, 5_000_000, minHouseholdsPerResidentialBuilding: 5, maxHouseholdsPerResidentialBuilding: 5);

        var ampleWorld = new WorldState(geography, SimInit(), roadGraph, scenario);
        ampleWorld.AddUtilitySource(new UtilitySource(UtilityKind.Power, SimulationFixtures.Node5, capacityUnitsPerTick: 1000));

        var scarceWorld = new WorldState(geography, SimInit(), roadGraph, scenario);
        scarceWorld.AddUtilitySource(new UtilitySource(UtilityKind.Power, SimulationFixtures.Node5, capacityUnitsPerTick: 1));

        Assert.Single(ampleWorld.Cohorts);
        Assert.Equal(5, ampleWorld.Cohorts.Values.Single().HouseholdCount); // confirms real demand = 5, not mocked.

        var ampleScore = ampleWorld.ComputeUtilityCoverageScore(residentialBuildingId, UtilityKind.Power);
        var scarceScore = scarceWorld.ComputeUtilityCoverageScore(residentialBuildingId, UtilityKind.Power);

        Assert.True(scarceScore < ampleScore, $"expected scarce-capacity score ({scarceScore}) < ample-capacity score ({ampleScore})");
    }

    [Fact]
    public void NoRegisteredSourceOfKind_ScoresZero_NotAssumedFine()
    {
        const long buildingId = 2003;
        var geography = OneBuilding(buildingId, "office", cx: 0, cz: 3);
        var roadGraph = SimulationFixtures.BuildRoadGraph();
        var world = new WorldState(geography, SimInit(), roadGraph, new ScenarioConfig(1, 5_000_000));
        // No AddUtilitySource call at all for any kind.

        Assert.Equal(0, world.ComputeUtilityCoverageScore(buildingId, UtilityKind.Waste));
    }

    [Fact]
    public void AddUtilitySource_RejectsANodeIdNotInTheRoadGraph()
    {
        var geography = OneBuilding(2003, "office", cx: 0, cz: 3);
        var world = new WorldState(geography, SimInit(), SimulationFixtures.BuildRoadGraph(), new ScenarioConfig(1, 5_000_000));

        Assert.Throws<System.ArgumentException>(() =>
            world.AddUtilitySource(new UtilitySource(UtilityKind.Water, nodeId: 999_999, capacityUnitsPerTick: 10)));
    }

    [Fact]
    public void UtilitySource_RejectsNegativeCapacity()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new UtilitySource(UtilityKind.Water, 1, -1));
    }

    /// <summary>CohortNeedsCalculator's new overload actually threads a
    /// real utility score through, and every existing overload keeps the
    /// documented baseline (100) -- neither one silently changed the
    /// other's behavior.</summary>
    [Fact]
    public void CohortNeedsCalculator_NewOverload_CarriesTheGivenUtilitiesScore_OldOverloadsKeepTheBaseline()
    {
        var withRealScore = CohortNeedsCalculator.Compute(noiseIndex: 10, accessibilityScore: 80, nearbyJobsReachable: 2, safety: 70, utilitiesCoverage: 42);
        Assert.Equal(42, withRealScore.Utilities);

        var oldFourArg = CohortNeedsCalculator.Compute(noiseIndex: 10, accessibilityScore: 80, nearbyJobsReachable: 2, safety: 70);
        Assert.Equal(CohortNeedsCalculator.BaselineUtilitiesCoverage, oldFourArg.Utilities);

        var oldThreeArg = CohortNeedsCalculator.Compute(noiseIndex: 10, accessibilityScore: 80, nearbyJobsReachable: 2);
        Assert.Equal(CohortNeedsCalculator.BaselineUtilitiesCoverage, oldThreeArg.Utilities);
    }
}
