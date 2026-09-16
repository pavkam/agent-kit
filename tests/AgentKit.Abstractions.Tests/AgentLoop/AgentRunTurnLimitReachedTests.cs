// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunTurnLimitReached behavior and contracts.</summary>
public sealed class AgentRunTurnLimitReachedTests
{
    [Fact]
    public void Constructor_WhenMaxTurnsIsNotPositive_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentRunTurnLimitReached(0)).ParamName.ShouldBe("maxTurns");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var outcome = new AgentRunTurnLimitReached(10);
        outcome.MaxTurns.ShouldBe(10);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentRunTurnLimitReached(10);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
