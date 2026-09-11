// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

using AgentKit;

/// <summary>Verifies RunEvent behavior and contracts.</summary>
public sealed class RunEventTests
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
        var result = new ExternalRunEvent(AgentId(), SessionId(), null, RunId(), null, long.MaxValue, DateTimeOffset.UnixEpoch, RunEventDurability.Durable);
        result.ConversationId.ShouldBeNull();
        result.TurnId.ShouldBeNull();
        result.Sequence.ShouldBe(long.MaxValue);
        result.Durability.ShouldBe(RunEventDurability.Durable);
    }

    [Fact]
    public void RunEvent_With_WhenExternalVariantCopies_PreservesConcreteTypeAndValue()
    {
        var original = ExternalEvent(conversationId: ConversationId());
        var copy = original with
        {
        };
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
    public void RunEvent_Constructor_WhenBaseCorrelationIsInvalid_ThrowsArgumentOutOfRangeException(int invalidCase, string parameterName)
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

    private static ExternalRunEvent ExternalEvent(ConversationId? conversationId = null, long sequence = 1, DateTimeOffset? occurredAt = null, RunEventDurability durability = RunEventDurability.Live) => new(AgentId(), SessionId(), conversationId, RunId(), TurnId(), sequence, occurredAt ?? DateTimeOffset.UnixEpoch, durability);
    private static AgentId AgentId() => new(Guid.Parse("9ec779cd-99d0-4d9e-90cd-68ee7d4479f3"));
    private static SessionId SessionId() => new(Guid.Parse("e0e7d4cc-b38b-4d3f-a779-a3e23451a7cb"));
    private static ConversationId ConversationId() => new(Guid.Parse("9a7ae8cd-3448-40a5-bfd4-3e2d7e8cf5ba"));
    private static RunId RunId() => new(Guid.Parse("0bda7aac-4d15-40bb-90a3-17214fb5eb32"));
    private static TurnId TurnId() => new(Guid.Parse("1bd5e5aa-6ab4-4420-8c0b-af3d4fb1f941"));
    private sealed record ExternalRunEvent(AgentId AgentId, SessionId SessionId, ConversationId? ConversationId, RunId RunId, TurnId? TurnId, long Sequence, DateTimeOffset OccurredAt, RunEventDurability Durability): RunEvent(AgentId, SessionId, ConversationId, RunId, TurnId, Sequence, OccurredAt, Durability);
}
