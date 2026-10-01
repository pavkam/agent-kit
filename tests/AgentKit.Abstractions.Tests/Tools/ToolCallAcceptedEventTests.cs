// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies <see cref="ToolCallAcceptedEvent"/>.</summary>
public sealed class ToolCallAcceptedEventTests
{
    private static readonly DateTimeOffset _at = DateTimeOffset.UnixEpoch.AddSeconds(5);

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var toolEvent = new ToolCallAcceptedEvent(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, _at, ToolId(), ToolVersion(), ExecutionPolicy());

        toolEvent.AgentId.ShouldBe(TestAgentId);
        toolEvent.SessionId.ShouldBe(TestSessionId);
        toolEvent.RunId.ShouldBe(TestRunId);
        toolEvent.TurnId.ShouldBe(TestTurnId);
        toolEvent.OperationId.ShouldBe(TestOperationId);
        toolEvent.CallId.ShouldBe(CallId);
        toolEvent.OccurredAt.ShouldBe(_at);
        toolEvent.ToolId.ShouldBe(ToolId());
        toolEvent.ToolVersion.ShouldBe(ToolVersion());
        toolEvent.ExecutionPolicy.ShouldBe(ExecutionPolicy());
    }

    [Fact]
    public void Constructor_WhenIdentityIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallAcceptedEvent(
            default, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, _at, ToolId(), ToolVersion(), ExecutionPolicy())).ParamName.ShouldBe("agentId");

    [Fact]
    public void Constructor_WhenToolIdIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallAcceptedEvent(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, _at, default, ToolVersion(), ExecutionPolicy())).ParamName.ShouldBe("toolId");

    [Fact]
    public void Constructor_WhenToolVersionIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallAcceptedEvent(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, _at, ToolId(), default, ExecutionPolicy())).ParamName.ShouldBe("toolVersion");

    [Fact]
    public void Constructor_WhenPolicyIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolCallAcceptedEvent(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, _at, ToolId(), ToolVersion(), null!)).ParamName.ShouldBe("executionPolicy");
}
