// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

/// <summary>Verifies <see cref="AgentScopedHookEventArgs"/> argument checks and read-only exposure.</summary>
public sealed class AgentScopedHookEventArgsTests
{
    [Fact]
    public void Constructor_WhenAgentIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new TestEventArgs(
            HookKernelTestData.Dispatch(), default, null));

        exception.ParamName.ShouldBe("agentId");
    }

    [Fact]
    public void Constructor_WhenSessionIdIsPresentAndDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new TestEventArgs(
            HookKernelTestData.Dispatch(), new AgentId(Guid.NewGuid()), default(SessionId)));

        exception.ParamName.ShouldBe("sessionId");
    }

    [Fact]
    public void Constructor_WhenDispatchNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TestEventArgs(
            null!, new AgentId(Guid.NewGuid()), null));

        exception.ParamName.ShouldBe("dispatch");
    }

    [Fact]
    public void Constructor_WhenValid_ExposesEveryProperty()
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var dispatch = HookKernelTestData.Dispatch();

        var args = new TestEventArgs(dispatch, agentId, sessionId);

        args.AgentId.ShouldBe(agentId);
        args.SessionId.ShouldBe(sessionId);
        args.Correlation.ShouldBe(dispatch.Correlation);
        args.Timestamp.ShouldBe(dispatch.Timestamp);
    }

    [Fact]
    public void Constructor_WhenSessionIdNull_AllowsNullSessionId()
    {
        var args = new TestEventArgs(HookKernelTestData.Dispatch(), new AgentId(Guid.NewGuid()), null);

        args.SessionId.ShouldBeNull();
    }

    private sealed class TestEventArgs(HookDispatchMetadata dispatch, AgentId agentId, SessionId? sessionId)
        : AgentScopedHookEventArgs(dispatch, agentId, sessionId);
}
