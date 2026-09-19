// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentLoopResult behavior and contracts.</summary>
public sealed class AgentLoopResultTests
{
    [Fact]
    public void Constructor_WhenOutcomeIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentLoopResult(LoopTestData.AgentId, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, null!, [], null)).ParamName.ShouldBe("outcome");

    [Fact]
    public void Constructor_WhenNewMessagesIsDefault_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new AgentLoopResult(LoopTestData.AgentId, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, new RunIdle(), default, null)).ParamName.ShouldBe("newMessages");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var outcome = new RunIdle();
        var result = Result(outcome: outcome);
        result.AgentId.ShouldBe(LoopTestData.AgentId);
        result.SessionId.ShouldBe(LoopTestData.SessionId);
        result.BranchId.ShouldBe(LoopTestData.BranchId);
        result.RunId.ShouldBe(LoopTestData.RunId);
        result.Outcome.ShouldBe(outcome);
        result.NewMessages.ShouldBeEmpty();
        result.FinalVersion.ShouldBeNull();
        result.Output.ShouldBeNull();
    }

    [Fact]
    public void Equality_WhenNewMessagesArePresent_HashesEveryMessage()
    {
        var message = LoopTestData.AssistantMessage();
        var first = Result(newMessages: [message], finalVersion: new SessionVersion(1));
        var second = Result(newMessages: [message], finalVersion: new SessionVersion(1));
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equality_WhenOtherIsNull_IsNotEqual() => Result().Equals(null).ShouldBeFalse();

    [Fact]
    public void Constructor_WhenOutputIsSupplied_RoundTripsProperty()
    {
        var output = LoopTestData.ValidatedOutput();
        var result = Result(outcome: new RunSucceeded(), output: output);
        result.Output.ShouldBe(output);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Result();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static AgentLoopResult Result(
        AgentRunOutcome? outcome = null, ImmutableArray<AgentMessage> newMessages = default, SessionVersion? finalVersion = null, ValidatedOutput? output = null) =>
        new(LoopTestData.AgentId, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId,
            outcome ?? new RunIdle(), newMessages.IsDefault ? [] : newMessages, finalVersion, output);
}
