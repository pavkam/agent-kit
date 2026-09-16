// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

/// <summary>Verifies <see cref="HistoryView"/> invariants and equality.</summary>
public sealed class HistoryViewTests
{
    [Fact]
    public void Constructor_WhenMessagesAreDefault_Throws()
    {
        var cursor = new MessageCursor(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), null, new BranchId(Guid.NewGuid()), new SessionVersion(0), new SessionSequence(0));
        Should.Throw<ArgumentException>(() => new HistoryView(cursor, default, [])).ParamName.ShouldBe("messages");
    }

    [Fact]
    public void Equals_WhenArraysAreDistinctButEmpty_IsDeep()
    {
        var cursor = new MessageCursor(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), null, new BranchId(Guid.NewGuid()), new SessionVersion(0), new SessionSequence(0));
        new HistoryView(cursor, [], []).ShouldBe(new HistoryView(cursor, [], []));
    }

    [Fact]
    public void Constructor_WhenChildBranchContainsParentMessage_PreservesAncestryProvenance()
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var parentBranch = new BranchId(Guid.NewGuid());
        var childBranch = new BranchId(Guid.NewGuid());
        var cursor = new MessageCursor(agentId, sessionId, null, childBranch, new SessionVersion(2), new SessionSequence(1));
        AgentMessage parentMessage = new UserMessage(
            new MessageId(Guid.NewGuid()),
            agentId,
            sessionId,
            null,
            parentBranch,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart("ancestor", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

        var view = new HistoryView(cursor, [parentMessage], []);

        view.Messages.ShouldHaveSingleItem().BranchId.ShouldBe(parentBranch);
    }

    [Fact]
    public void Equals_WhenMessagesAndRepairsMatch_HasEqualHashCode()
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var branchId = new BranchId(Guid.NewGuid());
        var cursor = new MessageCursor(agentId, sessionId, null, branchId, new SessionVersion(1), new SessionSequence(1));
        AgentMessage message = new UserMessage(
            new MessageId(Guid.NewGuid()),
            agentId,
            sessionId,
            null,
            branchId,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var repair = new HistoryRepair([message.Id], HistoryRepairKind.NormalizedContent, "reason", ExtensionData.Empty);
        var first = new HistoryView(cursor, [message], [repair]);
        var second = new HistoryView(cursor, [message], [repair]);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var cursor = new MessageCursor(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()), null, new BranchId(Guid.NewGuid()), new SessionVersion(0), new SessionSequence(0));
        var original = new HistoryView(cursor, [], []);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
