// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunToolCallStarted behavior and contracts.</summary>
public sealed class AgentRunToolCallStartedTests
{
    [Fact]
    public void Constructor_WhenCallIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunToolCallStarted(LoopTestData.TurnId, null!)).ParamName.ShouldBe("call");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var call = LoopTestData.ToolCall();
        var outcome = new AgentRunToolCallStarted(LoopTestData.TurnId, call);
        outcome.TurnId.ShouldBe(LoopTestData.TurnId);
        outcome.Call.ShouldBe(call);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentRunToolCallStarted(LoopTestData.TurnId, LoopTestData.ToolCall());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
