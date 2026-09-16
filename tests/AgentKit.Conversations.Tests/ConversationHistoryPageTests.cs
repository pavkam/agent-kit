// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Verifies immutable conversation-history page construction and value semantics.</summary>
public sealed class ConversationHistoryPageTests
{
    [Fact]
    public void Constructor_WhenMessagesAreDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new ConversationHistoryPage(default, new SessionSequence(0), complete: true));

        exception.ParamName.ShouldBe("messages");
    }

    [Fact]
    public void Equals_WhenMessageOrderAndPaginationEvidenceMatch_ReturnsTrue()
    {
        var messages = ImmutableArray<AgentMessage>.Empty;
        var first = new ConversationHistoryPage(messages, new SessionSequence(4), complete: false);
        var second = new ConversationHistoryPage(messages, new SessionSequence(4), complete: false);

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public void Equals_WhenPagesShareTheSameNonEmptyMessages_ReturnsTrueWithMatchingHashCode()
    {
        var message = new UserMessage(
            new MessageId(Guid.NewGuid()),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            null,
            new BranchId(Guid.NewGuid()),
            null,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var messages = ImmutableArray.Create<AgentMessage>(message);
        var first = new ConversationHistoryPage(messages, new SessionSequence(1), complete: true);
        var second = new ConversationHistoryPage(messages, new SessionSequence(1), complete: true);

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }
}
