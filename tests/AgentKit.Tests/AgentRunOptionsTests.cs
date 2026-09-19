// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Verifies AgentRunOptions behavior and contracts.</summary>
public sealed class AgentRunOptionsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaxTurnsIsZeroOrNegative_ThrowsExactArgumentOutOfRangeException(int maxTurns)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AgentRunOptions(maxTurns));

        exception.ParamName.ShouldBe("maxTurns");
    }

    [Fact]
    public void Constructor_WhenAttemptTimeoutIsZeroOrNegative_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AgentRunOptions(attemptTimeout: TimeSpan.Zero));

        exception.ParamName.ShouldBe("attemptTimeout");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreOmitted_DefaultsToNull()
    {
        var options = new AgentRunOptions();

        options.MaxTurns.ShouldBeNull();
        options.AttemptTimeout.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var options = new AgentRunOptions(3, TimeSpan.FromMinutes(1));

        options.MaxTurns.ShouldBe(3);
        options.AttemptTimeout.ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = CompositionTestData.RunOptions(maxTurns: 2);
        var copy = original with { };

        copy.ShouldBe(original);
    }
}
