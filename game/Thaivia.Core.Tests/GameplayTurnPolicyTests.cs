using Thaivia.Core.Graph;
using Xunit;

namespace Thaivia.Core.Tests;

/// <summary>Proves the disclosed rule from
/// docs/decisions/0016-turn-decision-unsupported-gameplay-routing-rule.md:
/// Unsupported is treated exactly like Denied for gameplay routing (fail
/// safe, not fail open).</summary>
public class GameplayTurnPolicyTests
{
    [Fact]
    public void Allowed_IsAllowedForRouting() =>
        Assert.True(GameplayTurnPolicy.IsAllowedForGameplayRouting(TurnDecision.Allowed));

    [Fact]
    public void Denied_IsNotAllowedForRouting() =>
        Assert.False(GameplayTurnPolicy.IsAllowedForGameplayRouting(TurnDecision.Denied));

    [Fact]
    public void Unsupported_IsTreatedExactlyLikeDenied_NotSilentlyAllowed() =>
        Assert.False(GameplayTurnPolicy.IsAllowedForGameplayRouting(TurnDecision.Unsupported));
}
