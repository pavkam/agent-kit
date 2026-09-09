// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

using AgentKit;

/// <summary>
/// Verifies local correlation, durability, and immutable value semantics for
/// the normative run-event values.
/// </summary>
public sealed class RunEventContractsTests
{
    [Fact]
    public void RunEvent_Constructor_WhenExternalVariantHasCompleteCorrelation_PreservesExtensibleEvent()
    {
        var occurredAt = DateTimeOffset.UnixEpoch.AddDays(1);
        var conversationId = ConversationId();

        var result = ExternalEvent(conversationId: conversationId, occurredAt: occurredAt);

        result.AgentId.ShouldBe(AgentId());
        result.SessionId.ShouldBe(SessionId());
        result.ConversationId.ShouldBe(conversationId);
        result.RunId.ShouldBe(RunId());
        result.TurnId.ShouldBe(TurnId());
        result.Sequence.ShouldBe(1);
        result.OccurredAt.ShouldBe(occurredAt);
        result.Durability.ShouldBe(RunEventDurability.Live);
    }

    [Fact]
    public void RunEvent_Constructor_WhenConversationAndTurnAreAbsent_PreservesOptionalCorrelation()
    {
        var result = new ExternalRunEvent(
            AgentId(),
            SessionId(),
            null,
            RunId(),
            null,
            long.MaxValue,
            DateTimeOffset.UnixEpoch,
            RunEventDurability.Durable);

        result.ConversationId.ShouldBeNull();
        result.TurnId.ShouldBeNull();
        result.Sequence.ShouldBe(long.MaxValue);
        result.Durability.ShouldBe(RunEventDurability.Durable);
    }

    [Fact]
    public void RunEvent_With_WhenExternalVariantCopies_PreservesConcreteTypeAndValue()
    {
        var original = ExternalEvent(conversationId: ConversationId());

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(ExternalRunEvent));
    }

    [Theory]
    [InlineData(0, "agentId")]
    [InlineData(1, "sessionId")]
    [InlineData(2, "conversationId")]
    [InlineData(3, "runId")]
    [InlineData(4, "turnId")]
    [InlineData(5, "sequence")]
    [InlineData(6, "durability")]
    [InlineData(7, "sequence")]
    public void RunEvent_Constructor_WhenBaseCorrelationIsInvalid_ThrowsArgumentOutOfRangeException(
        int invalidCase,
        string parameterName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => invalidCase switch
        {
            0 => new ExternalRunEvent(default, SessionId(), null, RunId(), null, 1, DateTimeOffset.UnixEpoch, RunEventDurability.Live),
            1 => new ExternalRunEvent(AgentId(), default, null, RunId(), null, 1, DateTimeOffset.UnixEpoch, RunEventDurability.Live),
            2 => new ExternalRunEvent(AgentId(), SessionId(), default(ConversationId), RunId(), null, 1, DateTimeOffset.UnixEpoch, RunEventDurability.Live),
            3 => new ExternalRunEvent(AgentId(), SessionId(), null, default, null, 1, DateTimeOffset.UnixEpoch, RunEventDurability.Live),
            4 => new ExternalRunEvent(AgentId(), SessionId(), null, RunId(), default(TurnId), 1, DateTimeOffset.UnixEpoch, RunEventDurability.Live),
            5 => new ExternalRunEvent(AgentId(), SessionId(), null, RunId(), null, 0, DateTimeOffset.UnixEpoch, RunEventDurability.Live),
            6 => new ExternalRunEvent(AgentId(), SessionId(), null, RunId(), null, 1, DateTimeOffset.UnixEpoch, (RunEventDurability) 99),
            7 => new ExternalRunEvent(AgentId(), SessionId(), null, RunId(), null, -1, DateTimeOffset.UnixEpoch, RunEventDurability.Live),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidCase)),
        });

        exception.ParamName.ShouldBe(parameterName);
    }

    [Fact]
    public void ContentDeltaEvent_Constructor_WhenTurnIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ContentDeltaEvent(
                AgentId(), SessionId(), null, RunId(), default(TurnId), 1, DateTimeOffset.UnixEpoch,
                ModelRequestId(), 0, new TextContentDelta("fragment")));

        exception.ParamName.ShouldBe("turnId");
    }

    [Fact]
    public void ContentDeltaEvent_Constructor_WhenTurnIdIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ContentDeltaEvent(
                AgentId(), SessionId(), null, RunId(), null, 1, DateTimeOffset.UnixEpoch,
                ModelRequestId(), 0, new TextContentDelta("fragment")));

        exception.ParamName.ShouldBe("turnId");
    }

    [Fact]
    public void ContentDeltaEvent_Constructor_WhenRequestIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ContentDeltaEvent(
                AgentId(), SessionId(), null, RunId(), TurnId(), 1, DateTimeOffset.UnixEpoch,
                default, 0, new TextContentDelta("fragment")));

        exception.ParamName.ShouldBe("requestId");
    }

    [Fact]
    public void ContentDeltaEvent_Constructor_WhenPartIndexIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ContentDeltaEvent(
                AgentId(), SessionId(), null, RunId(), TurnId(), 1, DateTimeOffset.UnixEpoch,
                ModelRequestId(), -1, new TextContentDelta("fragment")));

        exception.ParamName.ShouldBe("partIndex");
    }

    [Fact]
    public void ContentDeltaEvent_Constructor_WhenDeltaIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ContentDeltaEvent(
                AgentId(), SessionId(), null, RunId(), TurnId(), 1, DateTimeOffset.UnixEpoch,
                ModelRequestId(), 0, null!));

        exception.ParamName.ShouldBe("delta");
    }

    [Fact]
    public void ContentDeltaEvent_Constructor_WhenValidOptionalConversation_PreservesCorrelationTimestampAndEquality()
    {
        var occurredAt = DateTimeOffset.UnixEpoch.AddDays(1);
        var first = ContentDeltaEvent(conversationId: ConversationId(), occurredAt: occurredAt);
        var second = ContentDeltaEvent(conversationId: ConversationId(), occurredAt: occurredAt);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.Durability.ShouldBe(RunEventDurability.Live);
        first.RequestId.ShouldBe(ModelRequestId());
        first.PartIndex.ShouldBe(0);
        first.Delta.ShouldBe(new TextContentDelta("fragment"));
    }

    [Fact]
    public void ContentDeltaEvent_With_WhenBuiltInVariantCopies_PreservesConcreteTypeAndValue()
    {
        var original = ContentDeltaEvent(partIndex: int.MaxValue);

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(ContentDeltaEvent));
        copy.PartIndex.ShouldBe(int.MaxValue);
    }

    [Fact]
    public void MessageCommittedEvent_Constructor_WhenTurnIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new MessageCommittedEvent(
                AgentId(), SessionId(), null, RunId(), default(TurnId), 1, DateTimeOffset.UnixEpoch,
                MessageId(), new SessionVersion(1)));

        exception.ParamName.ShouldBe("turnId");
    }

    [Fact]
    public void MessageCommittedEvent_Constructor_WhenTurnIdIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new MessageCommittedEvent(
                AgentId(), SessionId(), null, RunId(), null, 1, DateTimeOffset.UnixEpoch,
                MessageId(), new SessionVersion(1)));

        exception.ParamName.ShouldBe("turnId");
    }

    [Fact]
    public void MessageCommittedEvent_Constructor_WhenMessageIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new MessageCommittedEvent(
                AgentId(), SessionId(), null, RunId(), TurnId(), 1, DateTimeOffset.UnixEpoch,
                default, new SessionVersion(1)));

        exception.ParamName.ShouldBe("messageId");
    }

    [Fact]
    public void MessageCommittedEvent_Constructor_WhenSessionVersionIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new MessageCommittedEvent(
                AgentId(), SessionId(), null, RunId(), TurnId(), 1, DateTimeOffset.UnixEpoch,
                MessageId(), default));

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

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(MessageCommittedEvent));
        copy.MessageId.ShouldBe(original.MessageId);
        copy.SessionVersion.ShouldBe(original.SessionVersion);
    }

    private static ExternalRunEvent ExternalEvent(
        ConversationId? conversationId = null,
        long sequence = 1,
        DateTimeOffset? occurredAt = null,
        RunEventDurability durability = RunEventDurability.Live) => new(
        AgentId(),
        SessionId(),
        conversationId,
        RunId(),
        TurnId(),
        sequence,
        occurredAt ?? DateTimeOffset.UnixEpoch,
        durability);

    private static ContentDeltaEvent ContentDeltaEvent(
        ConversationId? conversationId = null,
        int partIndex = 0,
        DateTimeOffset? occurredAt = null) => new(
        AgentId(),
        SessionId(),
        conversationId,
        RunId(),
        TurnId(),
        sequence: 1,
        occurredAt ?? DateTimeOffset.UnixEpoch,
        ModelRequestId(),
        partIndex,
        new TextContentDelta("fragment"));

    private static MessageCommittedEvent MessageCommittedEvent(
        ConversationId? conversationId = null,
        DateTimeOffset? occurredAt = null) => new(
        AgentId(),
        SessionId(),
        conversationId,
        RunId(),
        TurnId(),
        sequence: 1,
        occurredAt ?? DateTimeOffset.UnixEpoch,
        MessageId(),
        new SessionVersion(1));

    private static AgentId AgentId() => new(Guid.Parse("9ec779cd-99d0-4d9e-90cd-68ee7d4479f3"));

    private static SessionId SessionId() => new(Guid.Parse("e0e7d4cc-b38b-4d3f-a779-a3e23451a7cb"));

    private static ConversationId ConversationId() => new(Guid.Parse("9a7ae8cd-3448-40a5-bfd4-3e2d7e8cf5ba"));

    private static RunId RunId() => new(Guid.Parse("0bda7aac-4d15-40bb-90a3-17214fb5eb32"));

    private static TurnId TurnId() => new(Guid.Parse("1bd5e5aa-6ab4-4420-8c0b-af3d4fb1f941"));

    private static ModelRequestId ModelRequestId() => new(Guid.Parse("c60aa914-f23e-4c79-b1e5-0b786c6a1948"));

    private static MessageId MessageId() => new(Guid.Parse("16fd6df9-4e65-46e7-bc5f-a86c8e5ba45a"));

    private sealed record ExternalRunEvent(
        AgentId AgentId,
        SessionId SessionId,
        ConversationId? ConversationId,
        RunId RunId,
        TurnId? TurnId,
        long Sequence,
        DateTimeOffset OccurredAt,
        RunEventDurability Durability)
        : RunEvent(AgentId, SessionId, ConversationId, RunId, TurnId, Sequence, OccurredAt, Durability);
}
