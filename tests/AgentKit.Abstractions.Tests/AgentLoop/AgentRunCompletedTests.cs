// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunCompleted behavior and contracts.</summary>
public sealed class AgentRunCompletedTests
{
    [Fact]
    public void Constructor_WhenFinalMessageIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunCompleted(null!)).ParamName.ShouldBe("finalMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var message = LoopTestData.AssistantMessage();
        var outcome = new AgentRunCompleted(message);
        outcome.FinalMessage.ShouldBe(message);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentRunCompleted(LoopTestData.AssistantMessage());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
