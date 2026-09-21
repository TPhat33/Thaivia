using System.Linq;
using Thaivia.Core.Simulation.Archetypes;
using Thaivia.Core.Simulation.Noise;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

public class ActivityClockCatalogTests
{
    [Theory]
    [InlineData(BuildingArchetype.Residential)]
    [InlineData(BuildingArchetype.LateNightFoodStreet)]
    [InlineData(BuildingArchetype.SmallFactory)]
    [InlineData(BuildingArchetype.Temple)]
    [InlineData(BuildingArchetype.Market)]
    [InlineData(BuildingArchetype.Office)]
    [InlineData(BuildingArchetype.School)]
    [InlineData(BuildingArchetype.Retail)]
    public void EveryHour_ProducesValuesWithinZeroToOne(BuildingArchetype archetype)
    {
        for (var hour = 0; hour < 24; hour++)
        {
            var activity = ActivityClockCatalog.At(archetype, hour);
            Assert.InRange(activity.NoiseContribution, 0.0, 1.0);
            Assert.InRange(activity.TripGeneration, 0.0, 1.0);
            Assert.InRange(activity.ServiceLoad, 0.0, 1.0);
        }
    }

    [Fact]
    public void LateNightFoodStreet_IsLouderAtNightThanEarlyMorning()
    {
        var night = ActivityClockCatalog.At(BuildingArchetype.LateNightFoodStreet, 22);
        var earlyMorning = ActivityClockCatalog.At(BuildingArchetype.LateNightFoodStreet, 6);
        Assert.True(night.NoiseContribution > earlyMorning.NoiseContribution,
            $"expected night ({night.NoiseContribution}) > early morning ({earlyMorning.NoiseContribution})");
    }

    /// <summary>Ethical-constraint regression: Temple must never be a
    /// constantly elevated/negative archetype -- its noise contribution
    /// across MOST hours of the day must stay low (well under its one
    /// scheduled peak window), never uniformly high like a factory or a
    /// food street.</summary>
    [Fact]
    public void Temple_IsCalmAcrossMostOfTheDay_NeverPermanentlyElevated()
    {
        var hourly = Enumerable.Range(0, 24).Select(h => ActivityClockCatalog.At(BuildingArchetype.Temple, h).NoiseContribution).ToArray();
        var hoursAboveModerate = hourly.Count(v => v > 0.3);

        // At most a handful of hours around its one peak window may
        // exceed a moderate noise contribution -- the archetype must not
        // read as "loud all day", which is the shape a fixed negative
        // score would have.
        Assert.True(hoursAboveModerate <= 4, $"Temple exceeded moderate noise in {hoursAboveModerate} hours; expected a narrow peak window, not a constant elevated level.");
        Assert.True(hourly.Min() < 0.15, "Temple's quietest hour should be genuinely calm.");
    }

    [Fact]
    public void InvalidHour_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => ActivityClockCatalog.At(BuildingArchetype.Office, 24));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => ActivityClockCatalog.At(BuildingArchetype.Office, -1));
    }
}

public class NoiseIndexTests
{
    [Fact]
    public void CloserSource_ProducesHigherNoiseThanFartherSource_SameArchetypeSameHour()
    {
        var near = new[] { new NoiseIndex.NoiseSource(BuildingArchetype.LateNightFoodStreet, 5, 0) };
        var far = new[] { new NoiseIndex.NoiseSource(BuildingArchetype.LateNightFoodStreet, 500, 0) };

        var nearScore = NoiseIndex.ComputeAt(0, 0, hourOfDay: 22, near);
        var farScore = NoiseIndex.ComputeAt(0, 0, hourOfDay: 22, far);

        Assert.True(nearScore > farScore, $"expected near ({nearScore}) > far ({farScore})");
    }

    [Fact]
    public void SameSource_IsLouderAtItsPeakHourThanItsQuietHour()
    {
        var source = new[] { new NoiseIndex.NoiseSource(BuildingArchetype.LateNightFoodStreet, 10, 0) };
        var peak = NoiseIndex.ComputeAt(0, 0, hourOfDay: 22, source);
        var quiet = NoiseIndex.ComputeAt(0, 0, hourOfDay: 6, source);
        Assert.True(peak > quiet, $"expected peak ({peak}) > quiet ({quiet})");
    }

    [Fact]
    public void NoSources_IsZero()
    {
        Assert.Equal(0, NoiseIndex.ComputeAt(0, 0, 12, System.Array.Empty<NoiseIndex.NoiseSource>()));
    }

    [Fact]
    public void Result_IsAlwaysWithinZeroToOneHundred()
    {
        // Many loud sources stacked at the same point -- must clamp, not overflow past 100.
        var manySources = Enumerable.Range(0, 50).Select(_ => new NoiseIndex.NoiseSource(BuildingArchetype.LateNightFoodStreet, 0, 0));
        var score = NoiseIndex.ComputeAt(0, 0, 22, manySources);
        Assert.InRange(score, 0, 100);
    }
}
