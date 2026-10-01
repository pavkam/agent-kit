// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies <see cref="ToolCallTerminalEvent"/>.</summary>
public sealed class ToolCallTerminalEventTests
{
    private static readonly DateTimeOffset _at = DateTimeOffset.UnixEpoch.AddSeconds(5);

    [Fact]
    public void Constructor_WhenToolIsResolved_RoundTripsProperties()
    {
        var toolEvent = new ToolCallTerminalEvent(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, _at, ToolId(), ToolVersion(),
            ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, retryable: false, accepted: true, recorded: true);

        toolEvent.ToolId.ShouldBe(ToolId());
        toolEvent.ToolVersion.ShouldBe(ToolVersion());
        toolEvent.Status.ShouldBe(ToolTerminalStatus.Succeeded);
        toolEvent.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyPerformed);
        toolEvent.Retryable.ShouldBeFalse();
        toolEvent.Accepted.ShouldBeTrue();
        toolEvent.Recorded.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenToolIsUnresolved_AllowsAbsentIdentity()
    {
        var toolEvent = new ToolCallTerminalEvent(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, _at, null, null,
            ToolTerminalStatus.UnknownTool, SideEffectCertainty.DefinitelyNotPerformed, retryable: false, accepted: false, recorded: false);

        toolEvent.ToolId.ShouldBeNull();
        toolEvent.ToolVersion.ShouldBeNull();
        toolEvent.Accepted.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenOnlyOneOfIdAndVersionIsPresent_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolCallTerminalEvent(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, _at, ToolId(), null,
            ToolTerminalStatus.UnknownTool, SideEffectCertainty.DefinitelyNotPerformed, false, false, false)).ParamName.ShouldBe("toolVersion");

    [Fact]
    public void Constructor_WhenCertaintyIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolCallTerminalEvent(
            TestAgentId, TestSessionId, TestRunId, TestTurnId, TestOperationId, CallId, _at, null, null,
            ToolTerminalStatus.UnknownTool, (SideEffectCertainty) 99, false, false, false)).ParamName.ShouldBe("sideEffectCertainty");
}
