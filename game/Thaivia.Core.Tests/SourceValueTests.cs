using Thaivia.Core.Values;
using Xunit;

namespace Thaivia.Core.Tests;

public class SourceValueTests
{
    [Fact]
    public void Known_AsSourceFact_ReturnsTheValue()
    {
        var v = SourceValue<int>.Known(42);
        Assert.True(v.IsKnown);
        Assert.Equal(42, v.AsSourceFact());
    }

    [Fact]
    public void Unknown_AsSourceFact_Throws()
    {
        var v = SourceValue<int>.Unknown();
        Assert.Throws<InvalidOperationException>(() => v.AsSourceFact());
    }

    [Fact]
    public void Assumed_AsSourceFact_Throws()
    {
        // The single most safety-critical property of this type: an
        // assumed value can NEVER be read back through the accessor that
        // is supposed to mean "this came from the source".
        var v = SourceValue<double>.Assumed(3.0, "default_building_height_v1", AssumptionKind.Visual);
        var ex = Assert.Throws<InvalidOperationException>(() => v.AsSourceFact());
        Assert.Contains("Assumed", ex.Message);
    }

    [Fact]
    public void Unknown_TryGetDisplayValue_ReturnsFalse_NeverADefaultThatLooksReal()
    {
        var v = SourceValue<int>.Unknown();
        var ok = v.TryGetDisplayValue(out var value);
        Assert.False(ok);
        // Even though `value` is technically default(int) here (an `out`
        // parameter must be assigned), the contract is that callers MUST
        // check `ok` first -- there is no member that returns an int on
        // its own for an Unknown, unlike a GetValueOrDefault-style API.
        Assert.Equal(0, value);
    }

    [Fact]
    public void Unknown_StringDisplayValue_IsNeverEmptyStringMasqueradingAsKnown()
    {
        var v = SourceValue<string>.Unknown();
        var ok = v.TryGetDisplayValue(out var value);
        Assert.False(ok);
        Assert.Null(value);
    }

    [Fact]
    public void Assumed_TryGetDisplayValue_ReturnsTrueAndTheAssumedValue()
    {
        var v = SourceValue<int>.Assumed(3, "default_lane_count_v1", AssumptionKind.Visual);
        var ok = v.TryGetDisplayValue(out var value);
        Assert.True(ok);
        Assert.Equal(3, value);
    }

    [Fact]
    public void Match_DispatchesToTheCorrectBranch_ForAllThreeStates()
    {
        SourceValue<int> known = SourceValue<int>.Known(1);
        SourceValue<int> unknown = SourceValue<int>.Unknown();
        SourceValue<int> assumed = SourceValue<int>.Assumed(2, "rule", AssumptionKind.Simulation);

        Assert.Equal("known", known.Match(_ => "known", () => "unknown", (_, _, _) => "assumed"));
        Assert.Equal("unknown", unknown.Match(_ => "known", () => "unknown", (_, _, _) => "assumed"));
        Assert.Equal("assumed", assumed.Match(_ => "known", () => "unknown", (_, _, _) => "assumed"));
    }

    [Fact]
    public void Assumed_RequiresANonEmptyRule()
    {
        Assert.Throws<ArgumentException>(() => SourceValue<int>.Assumed(1, "", AssumptionKind.Visual));
        Assert.Throws<ArgumentException>(() => SourceValue<int>.Assumed(1, "   ", AssumptionKind.Visual));
    }

    [Fact]
    public void Assumed_ExposesItsRuleAndKind_ForInspectorUseWithoutPatternMatching()
    {
        var v = SourceValue<int>.Assumed(5, "default_gateway_demand_v1", AssumptionKind.Simulation);
        Assert.True(v.IsAssumed);
        Assert.Equal("default_gateway_demand_v1", v.AssumptionRule);
        Assert.Equal(AssumptionKind.Simulation, v.AssumptionKindValue);
    }

    [Fact]
    public void Known_And_Unknown_ExposeNoRuleOrKind()
    {
        Assert.Null(SourceValue<int>.Known(1).AssumptionRule);
        Assert.Null(SourceValue<int>.Known(1).AssumptionKindValue);
        Assert.Null(SourceValue<int>.Unknown().AssumptionRule);
        Assert.Null(SourceValue<int>.Unknown().AssumptionKindValue);
    }
}
