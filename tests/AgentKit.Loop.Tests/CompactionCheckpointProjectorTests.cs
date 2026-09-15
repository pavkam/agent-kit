// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

public sealed class CompactionCheckpointProjectorTests
{
    private readonly AgentId _agentId = new(Guid.NewGuid());
    private readonly SessionId _sessionId = new(Guid.NewGuid());
    private readonly BranchId _branchId = new(Guid.NewGuid());

    [Fact]
    public void Project_WhenCheckpointIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => CompactionCheckpointProjector.Project(null!, Cursor()));

        exception.ParamName.ShouldBe("checkpoint");
    }

    [Fact]
    public void Project_WhenCursorIsNull_ThrowsArgumentNullException()
    {
        var checkpoint = TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 3, coveredStart: 1, coveredEnd: 1, retainedSuffixStart: 2);

        var exception = Should.Throw<ArgumentNullException>(() => CompactionCheckpointProjector.Project(checkpoint, null!));

        exception.ParamName.ShouldBe("cursor");
    }

    [Fact]
    public void Project_WhenRecordIsNotActive_ThrowsArgumentException()
    {
        var rejected = TestFactory.SeedCompactionEntry(
            _agentId, _sessionId, _branchId, 3, coveredStart: 1, coveredEnd: 1, retainedSuffixStart: 2, status: CompactionRecordStatus.Rejected);

        var exception = Should.Throw<ArgumentException>(() => CompactionCheckpointProjector.Project(rejected, Cursor()));

        exception.ParamName.ShouldBe("checkpoint");
    }

    [Fact]
    public void Project_WhenRecordIsActive_AdoptsTheCursorCoordinatesAndKeepsSummaryPartsIntact()
    {
        var conversationId = new ConversationId(Guid.NewGuid());
        var checkpoint = TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 3, coveredStart: 1, coveredEnd: 1, retainedSuffixStart: 2, "kept verbatim");
        var cursor = Cursor(conversationId);

        var projected = CompactionCheckpointProjector.Project(checkpoint, cursor);

        projected.AgentId.ShouldBe(_agentId);
        projected.SessionId.ShouldBe(_sessionId);
        projected.ConversationId.ShouldBe(conversationId);
        projected.BranchId.ShouldBe(_branchId);
        projected.State.ShouldBe(MessageState.Complete);
        projected.CreatedAt.ShouldBe(checkpoint.RecordedAt);
        projected.Parts[0].ShouldBeOfType<TextPart>().Text.ShouldBe(CompactionCheckpointProjection.HeaderText);
        projected.Parts[1].ShouldBeSameAs(checkpoint.Record.Checkpoint!.Summary[0]);
        projected.Extensions.Values.Keys.ShouldBe(
            [
                CompactionCheckpointProjection.FormatKey,
                CompactionCheckpointProjection.CompactionIdKey,
                CompactionCheckpointProjection.ManifestIdKey,
                CompactionCheckpointProjection.SessionEntryIdKey,
                CompactionCheckpointProjection.SequenceKey,
                CompactionCheckpointProjection.CoveredStartKey,
                CompactionCheckpointProjection.CoveredEndKey,
                CompactionCheckpointProjection.RetainedSuffixStartKey,
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Project_WhenCalledTwiceForTheSameEntry_ProducesEqualMessages()
    {
        var checkpoint = TestFactory.SeedCompactionEntry(_agentId, _sessionId, _branchId, 3, coveredStart: 1, coveredEnd: 1, retainedSuffixStart: 2);
        var cursor = Cursor();

        var first = CompactionCheckpointProjector.Project(checkpoint, cursor);
        var second = CompactionCheckpointProjector.Project(checkpoint, cursor);

        second.ShouldBe(first);
    }

    [Fact]
    public void DeriveMessageId_WhenEntryIdsDiffer_ProducesDistinctVersionEightIdentities()
    {
        var first = new SessionEntryId(Guid.NewGuid());
        var second = new SessionEntryId(Guid.NewGuid());

        var firstId = CompactionCheckpointProjector.DeriveMessageId(first);
        var secondId = CompactionCheckpointProjector.DeriveMessageId(second);

        firstId.ShouldNotBe(secondId);
        CompactionCheckpointProjector.DeriveMessageId(first).ShouldBe(firstId);
        firstId.Value.ShouldNotBe(first.Value);
        firstId.Value.Version.ShouldBe(8);
        // Guid.Variant exposes the whole high nibble of octet 8; the RFC 4122 variant occupies its top two bits (10xx).
        firstId.Value.Variant.ShouldBeInRange(8, 11);
    }

    private MessageCursor Cursor(ConversationId? conversationId = null) =>
        new(_agentId, _sessionId, conversationId, _branchId, new SessionVersion(1), new SessionSequence(3));
}
