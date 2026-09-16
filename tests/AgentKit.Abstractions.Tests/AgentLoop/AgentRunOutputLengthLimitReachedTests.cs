// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunOutputLengthLimitReached behavior and contracts.</summary>
public sealed class AgentRunOutputLengthLimitReachedTests
{
    [Fact]
    public void Constructor_WhenModelRequestIdIsDefault_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentRunOutputLengthLimitReached(default, true, "truncated")).ParamName.ShouldBe("modelRequestId");

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new AgentRunOutputLengthLimitReached(LoopTestData.ModelRequestId, true, " ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var outcome = new AgentRunOutputLengthLimitReached(LoopTestData.ModelRequestId, true, "truncated");
        outcome.ModelRequestId.ShouldBe(LoopTestData.ModelRequestId);
        outcome.HasPartialOutput.ShouldBeTrue();
        outcome.SafeMessage.ShouldBe("truncated");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentRunOutputLengthLimitReached(LoopTestData.ModelRequestId, true, "truncated");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
