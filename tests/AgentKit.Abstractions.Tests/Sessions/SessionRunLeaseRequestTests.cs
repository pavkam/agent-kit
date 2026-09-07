// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

public sealed class SessionRunLeaseRequestTests
{
    [Fact]
    public void Constructor_WhenAgentIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new SessionRunLeaseRequest(default, SessionId(), RunId()));

        exception.ParamName.ShouldBe("agentId");
    }

    [Fact]
    public void Constructor_WhenSessionIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new SessionRunLeaseRequest(AgentId(), default, RunId()));

        exception.ParamName.ShouldBe("sessionId");
    }

    [Fact]
    public void Constructor_WhenRunIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new SessionRunLeaseRequest(AgentId(), SessionId(), default));

        exception.ParamName.ShouldBe("runId");
    }

    [Fact]
    public void With_WhenIdentityIsDefault_ThrowsBeforeProducingInvalidRequest()
    {
        var request = new SessionRunLeaseRequest(AgentId(), SessionId(), RunId());

        var agentException = Should.Throw<ArgumentOutOfRangeException>(
            () => request with { AgentId = default });
        var sessionException = Should.Throw<ArgumentOutOfRangeException>(
            () => request with { SessionId = default });
        var runException = Should.Throw<ArgumentOutOfRangeException>(
            () => request with { RunId = default });

        agentException.ParamName.ShouldBe(nameof(SessionRunLeaseRequest.AgentId));
        sessionException.ParamName.ShouldBe(nameof(SessionRunLeaseRequest.SessionId));
        runException.ParamName.ShouldBe(nameof(SessionRunLeaseRequest.RunId));
    }

    private static AgentId AgentId() => new(Guid.NewGuid());

    private static SessionId SessionId() => new(Guid.NewGuid());

    private static RunId RunId() => new(Guid.NewGuid());
}
