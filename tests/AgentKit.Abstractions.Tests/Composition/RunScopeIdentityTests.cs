// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies RunScopeIdentity behavior and contracts.</summary>
public sealed class RunScopeIdentityTests
{
    private static readonly AgentId Agent = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static readonly SessionId Session = new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    private static readonly ConversationId Conversation = new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static readonly RunId Run = new(Guid.Parse("40000000-0000-0000-0000-000000000001"));

    [Fact]
    public void Constructor_WhenAgentIdIsDefault_ThrowsArgumentOutOfRangeExceptionWithParamName() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RunScopeIdentity(default, Session, Conversation, Run)).ParamName.ShouldBe("agentId");

    [Fact]
    public void Constructor_WhenSessionIdIsDefault_ThrowsArgumentOutOfRangeExceptionWithParamName() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RunScopeIdentity(Agent, default, Conversation, Run)).ParamName.ShouldBe("sessionId");

    [Fact]
    public void Constructor_WhenConversationIdIsDefault_ThrowsArgumentOutOfRangeExceptionWithParamName() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RunScopeIdentity(Agent, Session, default(ConversationId), Run)).ParamName.ShouldBe("conversationId");

    [Fact]
    public void Constructor_WhenRunIdIsDefault_ThrowsArgumentOutOfRangeExceptionWithParamName() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RunScopeIdentity(Agent, Session, Conversation, default)).ParamName.ShouldBe("runId");

    [Fact]
    public void Constructor_WhenConversationIdIsNull_IsAccepted()
    {
        var identity = new RunScopeIdentity(Agent, Session, null, Run);

        identity.ConversationId.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var identity = new RunScopeIdentity(Agent, Session, Conversation, Run);

        identity.AgentId.ShouldBe(Agent);
        identity.SessionId.ShouldBe(Session);
        identity.ConversationId.ShouldBe(Conversation);
        identity.RunId.ShouldBe(Run);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new RunScopeIdentity(Agent, Session, Conversation, Run);
        var copy = original with { };

        copy.ShouldBe(original);
    }
}
