// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

using AgentKit;

/// <summary>
/// Verifies validation and value semantics for immutable history cursors.
/// </summary>
public sealed class MessageCursorTests
{
    [Theory]
    [InlineData("agentId")]
    [InlineData("sessionId")]
    [InlineData("branchId")]
    public void Constructor_WhenRequiredIdentityIsDefault_ThrowsArgumentOutOfRangeWithParameter(string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => CreateWithDefaultIdentity(parameter));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Constructor_WhenPresentConversationIsDefault_ThrowsArgumentOutOfRangeWithParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new MessageCursor(
            AgentId(),
            SessionId(),
            default(ConversationId),
            BranchId(),
            default,
            default));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("conversationId");
    }

    [Fact]
    public void Constructor_WhenOptionalConversationAbsentAndWatermarksZero_PreservesExactValues()
    {
        var cursor = new MessageCursor(AgentId(), SessionId(), null, BranchId(), default, default);

        cursor.AgentId.ShouldBe(AgentId());
        cursor.SessionId.ShouldBe(SessionId());
        cursor.ConversationId.ShouldBeNull();
        cursor.BranchId.ShouldBe(BranchId());
        cursor.Version.ShouldBe(new SessionVersion(0));
        cursor.Sequence.ShouldBe(new SessionSequence(0));
    }

    [Fact]
    public void Constructor_WhenAllValuesValid_CapturesIndependentWatermarksAndStructuralEquality()
    {
        var conversationId = new ConversationId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var first = new MessageCursor(
            AgentId(),
            SessionId(),
            conversationId,
            BranchId(),
            new SessionVersion(42),
            new SessionSequence(24));
        var same = new MessageCursor(
            AgentId(),
            SessionId(),
            conversationId,
            BranchId(),
            new SessionVersion(42),
            new SessionSequence(24));

        first.ConversationId.ShouldBe(conversationId);
        first.Version.ShouldBe(new SessionVersion(42));
        first.Sequence.ShouldBe(new SessionSequence(24));
        first.ShouldBe(same);
        first.GetHashCode().ShouldBe(same.GetHashCode());
    }

    [Fact]
    public void With_WhenCursorIsCopied_PreservesTypeAndAllWatermarkEvidence()
    {
        var original = new MessageCursor(
            AgentId(),
            SessionId(),
            new ConversationId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            BranchId(),
            new SessionVersion(42),
            new SessionSequence(24));

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(MessageCursor));
    }

    private static MessageCursor CreateWithDefaultIdentity(string parameter) => new(
        parameter == "agentId" ? default : AgentId(),
        parameter == "sessionId" ? default : SessionId(),
        null,
        parameter == "branchId" ? default : BranchId(),
        default,
        default);

    private static AgentId AgentId() => new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    private static SessionId SessionId() => new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    private static BranchId BranchId() => new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
}
