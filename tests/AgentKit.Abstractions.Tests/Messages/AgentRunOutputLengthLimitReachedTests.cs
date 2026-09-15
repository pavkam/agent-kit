// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

public sealed class AgentRunOutputLengthLimitReachedTests
{
    private static readonly ModelRequestId _requestId = new(Guid.Parse("20000000-0000-0000-0000-000000000002"));

    [Fact]
    public void Constructor_WhenModelRequestIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => _ = new AgentRunOutputLengthLimitReached(default, hasPartialOutput: true, "truncated"));

        exception.ParamName.ShouldBe("modelRequestId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsArgumentException(string? safeMessage)
    {
        var exception = Should.Throw<ArgumentException>(
            () => _ = new AgentRunOutputLengthLimitReached(_requestId, hasPartialOutput: false, safeMessage!));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesThem()
    {
        var outcome = new AgentRunOutputLengthLimitReached(_requestId, hasPartialOutput: true, "truncated");

        outcome.ModelRequestId.ShouldBe(_requestId);
        outcome.HasPartialOutput.ShouldBeTrue();
        outcome.SafeMessage.ShouldBe("truncated");
    }

    [Fact]
    public void Equals_WhenFieldsMatch_IsStructural()
    {
        var left = new AgentRunOutputLengthLimitReached(_requestId, hasPartialOutput: false, "truncated");
        var right = new AgentRunOutputLengthLimitReached(_requestId, hasPartialOutput: false, "truncated");

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        _ = left.ShouldBeAssignableTo<AgentRunOutcome>();
    }

    [Fact]
    public void Constructor_WhenUsedAsHaltOutcome_IsAcceptedAsNonSuccess()
    {
        var outcome = new AgentRunOutputLengthLimitReached(_requestId, hasPartialOutput: false, "truncated");

        var halt = new HaltRun(outcome);

        halt.Outcome.ShouldBe(outcome);
    }
}
