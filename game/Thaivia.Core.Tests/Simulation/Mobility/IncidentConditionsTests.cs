using Thaivia.Core.Simulation.Mobility.Incidents;
using Xunit;

namespace Thaivia.Core.Tests.Simulation.Mobility;

public class IncidentConditionsTests
{
    [Fact]
    public void NightDisorderRisk_IsHigherAtNight_ThanAtNoon_ForTheSameNoiseAndCongestion()
    {
        var nightRisk = IncidentConditions.NightDisorderRisk(hourOfDay: 2, noiseIndex0To100: 60, congestionRatio0To1: 0.2);
        var noonRisk = IncidentConditions.NightDisorderRisk(hourOfDay: 12, noiseIndex0To100: 60, congestionRatio0To1: 0.2);
        Assert.True(nightRisk > noonRisk);
    }

    [Fact]
    public void NightDisorderRisk_IsHigherWhenNoisier_ForTheSameHourAndCongestion()
    {
        var quiet = IncidentConditions.NightDisorderRisk(hourOfDay: 2, noiseIndex0To100: 10, congestionRatio0To1: 0.2);
        var loud = IncidentConditions.NightDisorderRisk(hourOfDay: 2, noiseIndex0To100: 90, congestionRatio0To1: 0.2);
        Assert.True(loud > quiet);
    }

    [Fact]
    public void StreetRacingRisk_IsHigherOnEmptyRoads_ThanCongestedOnes_AtTheSameHour()
    {
        var emptyRoad = IncidentConditions.StreetRacingRisk(hourOfDay: 2, congestionRatio0To1: 0.0);
        var jammedRoad = IncidentConditions.StreetRacingRisk(hourOfDay: 2, congestionRatio0To1: 1.0);
        Assert.True(emptyRoad > jammedRoad);
    }

    [Fact]
    public void RiskFunctions_AlwaysStayWithinZeroToOne()
    {
        for (var hour = 0; hour < 24; hour++)
        {
            for (var noise = 0; noise <= 100; noise += 10)
            {
                var risk = IncidentConditions.NightDisorderRisk(hour, noise, congestionRatio0To1: 0.0);
                Assert.InRange(risk, 0.0, 1.0);
            }
        }
    }
}
