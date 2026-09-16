// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunContextPreparationFailed behavior and contracts.</summary>
public sealed class AgentRunContextPreparationFailedTests
{
    [Fact]
    public void Constructor_WhenFailureIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunContextPreparationFailed(null!)).ParamName.ShouldBe("failure");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var failure = LoopTestData.PreparationFailure();
        var outcome = new AgentRunContextPreparationFailed(failure);
        outcome.Failure.ShouldBe(failure);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentRunContextPreparationFailed(LoopTestData.PreparationFailure());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
