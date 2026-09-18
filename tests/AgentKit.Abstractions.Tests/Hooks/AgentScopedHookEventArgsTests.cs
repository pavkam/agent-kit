// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

/// <summary>Verifies <see cref="AgentScopedHookEventArgs"/> argument checks and read-only exposure.</summary>
public sealed class AgentScopedHookEventArgsTests
{
    private static InRunOperationCorrelation Correlation() =>
        new(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null);

    [Fact]
    public void Constructor_WhenAgentIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new TestEventArgs(
            default, null, Correlation(), DateTimeOffset.UnixEpoch, new HookInvocationId(Guid.NewGuid())));

        exception.ParamName.ShouldBe("agentId");
    }

    [Fact]
    public void Constructor_WhenSessionIdIsPresentAndDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new TestEventArgs(
            new AgentId(Guid.NewGuid()), default(SessionId), Correlation(), DateTimeOffset.UnixEpoch,
            new HookInvocationId(Guid.NewGuid())));

        exception.ParamName.ShouldBe("sessionId");
    }

    [Fact]
    public void Constructor_WhenCorrelationNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TestEventArgs(
            new AgentId(Guid.NewGuid()), null, null!, DateTimeOffset.UnixEpoch, new HookInvocationId(Guid.NewGuid())));

        exception.ParamName.ShouldBe("correlation");
    }

    [Fact]
    public void Constructor_WhenValid_ExposesEveryProperty()
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var correlation = Correlation();
        var timestamp = DateTimeOffset.UnixEpoch;
        var invocationId = new HookInvocationId(Guid.NewGuid());

        var args = new TestEventArgs(agentId, sessionId, correlation, timestamp, invocationId);

        args.AgentId.ShouldBe(agentId);
        args.SessionId.ShouldBe(sessionId);
        args.Correlation.ShouldBe(correlation);
        args.Timestamp.ShouldBe(timestamp);
        args.InvocationId.ShouldBe(invocationId);
    }

    [Fact]
    public void Constructor_WhenSessionIdNull_AllowsNullSessionId()
    {
        var args = new TestEventArgs(
            new AgentId(Guid.NewGuid()), null, Correlation(), DateTimeOffset.UnixEpoch,
            new HookInvocationId(Guid.NewGuid()));

        args.SessionId.ShouldBeNull();
    }

    private sealed class TestEventArgs: AgentScopedHookEventArgs
    {
        public TestEventArgs(
            AgentId agentId,
            SessionId? sessionId,
            OperationCorrelation correlation,
            DateTimeOffset timestamp,
            HookInvocationId invocationId)
            : base(agentId, sessionId, correlation, timestamp, invocationId)
        {
        }
    }
}
