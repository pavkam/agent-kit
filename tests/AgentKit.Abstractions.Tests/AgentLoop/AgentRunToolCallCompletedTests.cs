// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunToolCallCompleted behavior and contracts.</summary>
public sealed class AgentRunToolCallCompletedTests
{
    [Fact]
    public void Constructor_WhenResultIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunToolCallCompleted(LoopTestData.TurnId, null!)).ParamName.ShouldBe("result");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var result = LoopTestData.ToolResult();
        var outcome = new AgentRunToolCallCompleted(LoopTestData.TurnId, result);
        outcome.TurnId.ShouldBe(LoopTestData.TurnId);
        outcome.Result.ShouldBe(result);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentRunToolCallCompleted(LoopTestData.TurnId, LoopTestData.ToolResult());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
