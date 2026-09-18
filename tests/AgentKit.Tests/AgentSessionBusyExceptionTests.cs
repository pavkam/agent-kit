// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Verifies <see cref="AgentSessionBusyException"/> argument checks and message content.</summary>
public sealed class AgentSessionBusyExceptionTests
{
    [Fact]
    public void Constructor_WhenIdentitiesAreValid_ExposesThemAndASafeMessage()
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());

        var exception = new AgentSessionBusyException(agentId, sessionId, runId);

        exception.AgentId.ShouldBe(agentId);
        exception.SessionId.ShouldBe(sessionId);
        exception.ActiveRunId.ShouldBe(runId);
        exception.Message.ShouldContain(sessionId.ToString());
        exception.Message.ShouldContain("busy");
    }

    [Fact]
    public void Constructor_WhenAgentIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentSessionBusyException(default, new SessionId(Guid.NewGuid()), new RunId(Guid.NewGuid()))).ParamName.ShouldBe("agentId");

    [Fact]
    public void Constructor_WhenSessionIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentSessionBusyException(new AgentId(Guid.NewGuid()), default, new RunId(Guid.NewGuid()))).ParamName.ShouldBe("sessionId");

    [Fact]
    public void Constructor_WhenRunIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentSessionBusyException(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), default)).ParamName.ShouldBe("activeRunId");
}
