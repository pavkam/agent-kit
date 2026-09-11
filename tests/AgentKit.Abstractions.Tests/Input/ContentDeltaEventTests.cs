// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;

using AgentKit;

/// <summary>Verifies ContentDeltaEvent behavior and contracts.</summary>
public sealed class ContentDeltaEventTests
{
    [Fact]
    public void ContentDeltaEvent_Constructor_WhenTurnIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ContentDeltaEvent(AgentId(), SessionId(), null, RunId(), default(TurnId), 1, DateTimeOffset.UnixEpoch, ModelRequestId(), 0, new TextContentDelta("fragment")));
        exception.ParamName.ShouldBe("turnId");
    }

    [Fact]
    public void ContentDeltaEvent_Constructor_WhenTurnIdIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ContentDeltaEvent(AgentId(), SessionId(), null, RunId(), null, 1, DateTimeOffset.UnixEpoch, ModelRequestId(), 0, new TextContentDelta("fragment")));
        exception.ParamName.ShouldBe("turnId");
    }

    [Fact]
    public void ContentDeltaEvent_Constructor_WhenRequestIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ContentDeltaEvent(AgentId(), SessionId(), null, RunId(), TurnId(), 1, DateTimeOffset.UnixEpoch, default, 0, new TextContentDelta("fragment")));
        exception.ParamName.ShouldBe("requestId");
    }

    [Fact]
    public void ContentDeltaEvent_Constructor_WhenPartIndexIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ContentDeltaEvent(AgentId(), SessionId(), null, RunId(), TurnId(), 1, DateTimeOffset.UnixEpoch, ModelRequestId(), -1, new TextContentDelta("fragment")));
        exception.ParamName.ShouldBe("partIndex");
    }

    [Fact]
    public void ContentDeltaEvent_Constructor_WhenDeltaIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ContentDeltaEvent(AgentId(), SessionId(), null, RunId(), TurnId(), 1, DateTimeOffset.UnixEpoch, ModelRequestId(), 0, null!));
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
        var copy = original with
        {
        };
        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(ContentDeltaEvent));
        copy.PartIndex.ShouldBe(int.MaxValue);
    }

    private static ContentDeltaEvent ContentDeltaEvent(ConversationId? conversationId = null, int partIndex = 0, DateTimeOffset? occurredAt = null) => new(AgentId(), SessionId(), conversationId, RunId(), TurnId(), sequence: 1, occurredAt ?? DateTimeOffset.UnixEpoch, ModelRequestId(), partIndex, new TextContentDelta("fragment"));
    private static AgentId AgentId() => new(Guid.Parse("9ec779cd-99d0-4d9e-90cd-68ee7d4479f3"));
    private static SessionId SessionId() => new(Guid.Parse("e0e7d4cc-b38b-4d3f-a779-a3e23451a7cb"));
    private static ConversationId ConversationId() => new(Guid.Parse("9a7ae8cd-3448-40a5-bfd4-3e2d7e8cf5ba"));
    private static RunId RunId() => new(Guid.Parse("0bda7aac-4d15-40bb-90a3-17214fb5eb32"));
    private static TurnId TurnId() => new(Guid.Parse("1bd5e5aa-6ab4-4420-8c0b-af3d4fb1f941"));
    private static ModelRequestId ModelRequestId() => new(Guid.Parse("c60aa914-f23e-4c79-b1e5-0b786c6a1948"));
}
