// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunFailed behavior and contracts.</summary>
public sealed class AgentRunFailedTests
{
    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunFailed(null!)).ParamName.ShouldBe("failure");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var failure = LoopTestData.Failure();
        var outcome = new AgentRunFailed(failure);
        outcome.Failure.ShouldBe(failure);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentRunFailed(LoopTestData.Failure());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
