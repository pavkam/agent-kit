// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

public sealed class ContentTextExtractorTests
{
    [Fact]
    public void ExtractEntryText_WhenEntryNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => ContentTextExtractor.ExtractEntryText(null!));

        exception.ParamName.ShouldBe("entry");
    }

    [Fact]
    public void ExtractPartsText_WhenPartsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => ContentTextExtractor.ExtractPartsText(default));

        exception.ParamName.ShouldBe("parts");
    }

    [Fact]
    public void ExtractEntryText_WhenMessageSessionEntry_ExtractsMessageText()
    {
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var branchId = new BranchId(Guid.NewGuid());
        var entry = TestFactory.MessageEntry(address, branchId, 1, "hello");

        var text = ContentTextExtractor.ExtractEntryText(entry);

        text.ShouldBe("hello");
    }

    [Fact]
    public void ExtractEntryText_WhenCompactionSessionEntryWithCheckpoint_ExtractsCheckpointSummaryText()
    {
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var branchId = new BranchId(Guid.NewGuid());
        var context = TestFactory.CompactionContext();
        var manifest = new CompactionManifest(
            new CompactionManifestId(Guid.NewGuid()),
            context,
            branchId,
            new SessionVersion(1),
            new CompactionSourceRange(new SessionSequence(1), new SessionSequence(1)),
            new SessionSequence(2),
            new CompactionProducer(new CompactionStrategyKey("test"), true, ExtensionData.Empty),
            new ContextEpoch(0),
            new CompactionSizeEstimate(1, 1, 1),
            new CompactionSizeEstimate(1, 1, 1),
            DateTimeOffset.UnixEpoch,
            ExtensionData.Empty);
        var checkpoint = new CompactionCheckpoint(
            [new TextPart("summarized text", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var record = new CompactionRecord(
            context,
            new SessionVersion(1),
            new SessionVersion(2),
            CompactionRecordStatus.Active,
            manifest,
            checkpoint,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            ExtensionData.Empty);
        var entry = new CompactionSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            address,
            TestFactory.Correlation(),
            branchId,
            new SessionSequence(2),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            record);

        var text = ContentTextExtractor.ExtractEntryText(entry);

        text.ShouldBe("summarized text");
    }

    [Fact]
    public void ExtractEntryText_WhenCompactionSessionEntryWithoutCheckpoint_ReturnsEmpty()
    {
        var address = new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));
        var branchId = new BranchId(Guid.NewGuid());
        var context = TestFactory.CompactionContext();
        var manifest = new CompactionManifest(
            new CompactionManifestId(Guid.NewGuid()),
            context,
            branchId,
            new SessionVersion(1),
            new CompactionSourceRange(new SessionSequence(1), new SessionSequence(1)),
            new SessionSequence(2),
            new CompactionProducer(new CompactionStrategyKey("test"), true, ExtensionData.Empty),
            new ContextEpoch(0),
            new CompactionSizeEstimate(1, 1, 1),
            new CompactionSizeEstimate(1, 1, 1),
            DateTimeOffset.UnixEpoch,
            ExtensionData.Empty);
        var record = new CompactionRecord(
            context,
            new SessionVersion(1),
            null,
            CompactionRecordStatus.Rejected,
            manifest,
            null,
            null,
            new CompactionRejection(CompactionRejectionKind.NoSafeCut, "no safe cut in this test fixture", ExtensionData.Empty),
            DateTimeOffset.UnixEpoch,
            ExtensionData.Empty);
        var entry = new CompactionSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            address,
            TestFactory.Correlation(),
            branchId,
            new SessionSequence(2),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            record);

        var text = ContentTextExtractor.ExtractEntryText(entry);

        text.ShouldBe(string.Empty);
    }

    [Fact]
    public void ExtractPartsText_WhenMultipleTextPartsPresent_JoinsThemWithNewlines()
    {
        var parts = ImmutableArray.Create<ContentPart>(
            new TextPart("first line", TextSemantics.Plain, ExtensionData.Empty),
            new TextPart("second line", TextSemantics.Plain, ExtensionData.Empty));

        var text = ContentTextExtractor.ExtractPartsText(parts);

        text.ShouldBe("first line\nsecond line");
    }

    [Fact]
    public void ExtractPartsText_WhenToolResultPartPresent_RecursesIntoResultContent()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var tool = new ToolReference(new ToolAlias("tool"), null, null);
        var outcome = new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty);
        var resultPart = new ToolResultPart(callId, tool, outcome, [new TextPart("nested result text", TextSemantics.Plain, ExtensionData.Empty)], new ToolResultProjectionInfo(ToolResultProjectionPolicyReference.Default, [], 0, 0), ExtensionData.Empty);

        var text = ContentTextExtractor.ExtractPartsText([resultPart]);

        text.ShouldBe("nested result text");
    }
}
