// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using Microsoft.Extensions.Logging.Abstractions;

using static ToolRuntimeTestCalls;

/// <summary>Verifies <see cref="ToolRecordObservation"/> argument constraints and idempotent completion.</summary>
public sealed class ToolRecordObservationTests
{
    private static readonly ToolCallId _callId = new(Guid.Parse("66666666-6666-6666-6666-666666666661"));

    [Theory]
    [InlineData("recorded")]
    [InlineData("conflict")]
    [InlineData("invalid")]
    [InlineData("unavailable")]
    [InlineData("cancelled")]
    [InlineData("failed")]
    public void Complete_WhenOutcomeIsInTheClosedVocabulary_Succeeds(string outcome)
    {
        using var observation = Start("accepted");

        observation.Complete(outcome);
    }

    [Fact]
    public void Complete_WhenOutcomeIsOutsideTheVocabulary_ThrowsExactParameter()
    {
        using var observation = Start("terminal");

        Should.Throw<ArgumentException>(() => observation.Complete("success")).ParamName.ShouldBe("outcome");
        Should.Throw<ArgumentNullException>(() => observation.Complete(null!)).ParamName.ShouldBe("outcome");
    }

    [Fact]
    public void Complete_WhenCalledTwice_KeepsTheFirstOutcomeAndDisposeIsRepeatable()
    {
        var observation = Start("accepted");

        observation.Complete("recorded");
        observation.Complete("failed");
        observation.Dispose();
        observation.Dispose();
    }

    [Fact]
    public void Constructor_WhenStageIsUnknown_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => Start("projection")).ParamName.ShouldBe("stage");

    [Fact]
    public void Constructor_WhenReferenceArgumentIsNull_ThrowsExactParameter()
    {
        Should.Throw<ArgumentNullException>(() => Start(null!)).ParamName.ShouldBe("stage");
        Should.Throw<ArgumentNullException>(() => new ToolRecordObservation("accepted", AgentId, SessionId, RunId, TurnId, _callId, null!, NullLogger.Instance)).ParamName.ShouldBe("clock");
        Should.Throw<ArgumentNullException>(() => new ToolRecordObservation("accepted", AgentId, SessionId, RunId, TurnId, _callId, TimeProvider.System, null!)).ParamName.ShouldBe("logger");
    }

    private static ToolRecordObservation Start(string stage) =>
        new(stage, AgentId, SessionId, RunId, TurnId, _callId, TimeProvider.System, NullLogger.Instance);
}
