// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

using AgentKit;

/// <summary>Verifies MessageCommittedEvent behavior and contracts.</summary>
public sealed class MessageCommittedEventTests
{
    [Fact]
    public void MessageCommittedEvent_Constructor_WhenTurnIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new MessageCommittedEvent(AgentId(), SessionId(), null, RunId(), default(TurnId), 1, DateTimeOffset.UnixEpoch, MessageId(), new SessionVersion(1)));
        exception.ParamName.ShouldBe("turnId");
    }

    [Fact]
    public void MessageCommittedEvent_Constructor_WhenTurnIdIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new MessageCommittedEvent(AgentId(), SessionId(), null, RunId(), null, 1, DateTimeOffset.UnixEpoch, MessageId(), new SessionVersion(1)));
        exception.ParamName.ShouldBe("turnId");
    }

    [Fact]
    public void MessageCommittedEvent_Constructor_WhenMessageIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new MessageCommittedEvent(AgentId(), SessionId(), null, RunId(), TurnId(), 1, DateTimeOffset.UnixEpoch, default, new SessionVersion(1)));
        exception.ParamName.ShouldBe("messageId");
    }

    [Fact]
    public void MessageCommittedEvent_Constructor_WhenSessionVersionIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new MessageCommittedEvent(AgentId(), SessionId(), null, RunId(), TurnId(), 1, DateTimeOffset.UnixEpoch, MessageId(), default));
        exception.ParamName.ShouldBe("sessionVersion");
    }

    [Fact]
    public void MessageCommittedEvent_Constructor_WhenValidOptionalConversation_PreservesCorrelationTimestampAndEquality()
    {
        var occurredAt = DateTimeOffset.UnixEpoch.AddDays(1);
        var first = MessageCommittedEvent(conversationId: ConversationId(), occurredAt: occurredAt);
        var second = MessageCommittedEvent(conversationId: ConversationId(), occurredAt: occurredAt);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.Durability.ShouldBe(RunEventDurability.Durable);
        first.MessageId.ShouldBe(MessageId());
        first.SessionVersion.ShouldBe(new SessionVersion(1));
    }

    [Fact]
    public void MessageCommittedEvent_With_WhenBuiltInVariantCopies_PreservesConcreteTypeAndValue()
    {
        var original = MessageCommittedEvent();
        var copy = original with
        {
        };
        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(MessageCommittedEvent));
        copy.MessageId.ShouldBe(original.MessageId);
        copy.SessionVersion.ShouldBe(original.SessionVersion);
    }

    private static MessageCommittedEvent MessageCommittedEvent(ConversationId? conversationId = null, DateTimeOffset? occurredAt = null) => new(AgentId(), SessionId(), conversationId, RunId(), TurnId(), sequence: 1, occurredAt ?? DateTimeOffset.UnixEpoch, MessageId(), new SessionVersion(1));
    private static AgentId AgentId() => new(Guid.Parse("9ec779cd-99d0-4d9e-90cd-68ee7d4479f3"));
    private static SessionId SessionId() => new(Guid.Parse("e0e7d4cc-b38b-4d3f-a779-a3e23451a7cb"));
    private static ConversationId ConversationId() => new(Guid.Parse("9a7ae8cd-3448-40a5-bfd4-3e2d7e8cf5ba"));
    private static RunId RunId() => new(Guid.Parse("0bda7aac-4d15-40bb-90a3-17214fb5eb32"));
    private static TurnId TurnId() => new(Guid.Parse("1bd5e5aa-6ab4-4420-8c0b-af3d4fb1f941"));
    private static MessageId MessageId() => new(Guid.Parse("16fd6df9-4e65-46e7-bc5f-a86c8e5ba45a"));
}
