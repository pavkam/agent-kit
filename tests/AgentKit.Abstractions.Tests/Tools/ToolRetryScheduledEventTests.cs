// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies <see cref="ToolRetryScheduledEvent"/>.</summary>
public sealed class ToolRetryScheduledEventTests
{
    private static readonly DateTimeOffset _at = DateTimeOffset.UnixEpoch.AddSeconds(5);

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var toolEvent = new ToolRetryScheduledEvent(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, _at, ToolId(), ToolVersion(), 2, TimeSpan.FromSeconds(1));

        toolEvent.FailedAttempt.ShouldBe(2);
        toolEvent.Delay.ShouldBe(TimeSpan.FromSeconds(1));
        toolEvent.ToolId.ShouldBe(ToolId());
        toolEvent.ToolVersion.ShouldBe(ToolVersion());
    }

    [Theory]
    [InlineData(0, 0, "failedAttempt")]
    [InlineData(-1, 0, "failedAttempt")]
    [InlineData(1, -1, "delay")]
    public void Constructor_WhenArgumentIsOutOfRange_ThrowsExactParameter(int failedAttempt, int delaySeconds, string parameter) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolRetryScheduledEvent(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, _at, ToolId(), ToolVersion(), failedAttempt, TimeSpan.FromSeconds(delaySeconds))).ParamName.ShouldBe(parameter);

    [Fact]
    public void Constructor_WhenToolIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolRetryScheduledEvent(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, _at, default, ToolVersion(), 1, TimeSpan.Zero)).ParamName.ShouldBe("toolId");
}
