// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunModelResponseEvent behavior and contracts.</summary>
public sealed class AgentRunModelResponseEventTests
{
    [Fact]
    public void Constructor_WhenResponseEventIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunModelResponseEvent(LoopTestData.TurnId, null!)).ParamName.ShouldBe("responseEvent");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var responseEvent = new ModelResponseStarted(LoopTestData.ModelRequestId, 0);
        var outcome = new AgentRunModelResponseEvent(LoopTestData.TurnId, responseEvent);
        outcome.TurnId.ShouldBe(LoopTestData.TurnId);
        outcome.ResponseEvent.ShouldBe(responseEvent);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentRunModelResponseEvent(LoopTestData.TurnId, new ModelResponseStarted(LoopTestData.ModelRequestId, 0));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
