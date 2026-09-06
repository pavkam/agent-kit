// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

using Microsoft.Extensions.Options;

public sealed class StructuralCompactionCutSelectorTests
{
    private readonly AgentId _agentId = new(Guid.NewGuid());
    private readonly SessionId _sessionId = new(Guid.NewGuid());
    private readonly BranchId _branchId = new(Guid.NewGuid());

    [Fact]
    public async Task SelectAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var selector = CreateSelector();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => selector.SelectAsync(null!, TestContext.Current.CancellationToken).AsTask());

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task SelectAsync_WhenEntriesFitWithinMinimumRetained_ReturnsNoSafeCut()
    {
        var selector = CreateSelector();
        var entries = ImmutableArray.Create<SessionEntry>(
            TestFactory.MessageEntry(Address(), _branchId, 1),
            TestFactory.MessageEntry(Address(), _branchId, 2));

        var result = await selector.SelectAsync(
            SelectionRequest(entries, minimumRetainedEntries: 2), TestContext.Current.CancellationToken);

        var noCut = result.ShouldBeOfType<NoSafeCompactionCut>();
        noCut.Rejection.Kind.ShouldBe(CompactionRejectionKind.NoSafeCut);
    }

    [Fact]
    public async Task SelectAsync_WhenSimpleLinearHistory_SelectsCutCoveringAllButMinimumRetained()
    {
        var selector = CreateSelector();
        var entries = ImmutableArray.Create<SessionEntry>(
            TestFactory.MessageEntry(Address(), _branchId, 1),
            TestFactory.MessageEntry(Address(), _branchId, 2),
            TestFactory.MessageEntry(Address(), _branchId, 3),
            TestFactory.MessageEntry(Address(), _branchId, 4));

        var result = await selector.SelectAsync(
            SelectionRequest(entries, minimumRetainedEntries: 1), TestContext.Current.CancellationToken);

        var selected = result.ShouldBeOfType<CompactionCutSelected>();
        selected.Cut.CoveredEntryIds.Length.ShouldBe(3);
        selected.Cut.RetainedSuffixStart.ShouldBe(entries[3].Sequence);
    }

    [Fact]
    public async Task SelectAsync_WhenCausalPairWouldBeSplit_SelectsEarlierSafeBoundary()
    {
        var selector = CreateSelector();
        var address = Address();
        var (call, result) = TestFactory.ToolCallPair(address, _branchId, callSequence: 3, resultSequence: 4);
        var entries = ImmutableArray.Create<SessionEntry>(
            TestFactory.MessageEntry(address, _branchId, 1),
            TestFactory.MessageEntry(address, _branchId, 2),
            call,
            result);

        var selection = await selector.SelectAsync(
            SelectionRequest(entries, minimumRetainedEntries: 1), TestContext.Current.CancellationToken);

        var selected = selection.ShouldBeOfType<CompactionCutSelected>();

        // Covering 3 (through the tool call) would split the call from its result at position 4,
        // so the safe boundary backs off to covering only the first 2 plain entries.
        selected.Cut.CoveredEntryIds.Length.ShouldBe(2);
    }

    [Fact]
    public async Task SelectAsync_WhenNoSafeBoundaryExists_ReturnsNoSafeCut()
    {
        var selector = CreateSelector();
        var address = Address();
        var (call, result) = TestFactory.ToolCallPair(address, _branchId, callSequence: 1, resultSequence: 2);
        var entries = ImmutableArray.Create<SessionEntry>(call, result);

        var selection = await selector.SelectAsync(
            SelectionRequest(entries, minimumRetainedEntries: 1), TestContext.Current.CancellationToken);

        var noCut = selection.ShouldBeOfType<NoSafeCompactionCut>();
        noCut.Rejection.Kind.ShouldBe(CompactionRejectionKind.NoSafeCut);
    }

    [Fact]
    public async Task SelectAsync_WhenSourceExceedsMaximum_ReturnsSourceLimitExceeded()
    {
        var selector = CreateSelector(maximumSourceEntries: 2);
        var entries = ImmutableArray.Create<SessionEntry>(
            TestFactory.MessageEntry(Address(), _branchId, 1),
            TestFactory.MessageEntry(Address(), _branchId, 2),
            TestFactory.MessageEntry(Address(), _branchId, 3));

        var result = await selector.SelectAsync(
            SelectionRequest(entries, minimumRetainedEntries: 1), TestContext.Current.CancellationToken);

        var noCut = result.ShouldBeOfType<NoSafeCompactionCut>();
        noCut.Rejection.Kind.ShouldBe(CompactionRejectionKind.SourceLimitExceeded);
    }

    private SessionAddress Address() => new(_agentId, _sessionId);

    private CompactionCutSelectionRequest SelectionRequest(
        ImmutableArray<SessionEntry> entries, int minimumRetainedEntries)
    {
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(
            context,
            _branchId,
            new SessionVersion(entries.Length),
            entries.IsEmpty ? new SessionSequence(0) : entries[^1].Sequence,
            minimumRetainedEntries);
        var source = new CompactionSourceSnapshot(
            context, _branchId, request.SourceVersion, request.SourceThrough, entries);

        return new CompactionCutSelectionRequest(request, source);
    }

    private static StructuralCompactionCutSelector CreateSelector(int maximumSourceEntries = 5_000) =>
        new(Options.Create(new CompactionOptions { MaximumSourceEntries = maximumSourceEntries }));
}
