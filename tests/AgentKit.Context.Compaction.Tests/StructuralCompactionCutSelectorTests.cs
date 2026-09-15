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

        // Covering 3 (through the tool call) would split the call from its result at position 4. Covering 2 is
        // causally safe but would start the retained suffix at the assistant tool call; the selector prefers the
        // safe boundary that starts the suffix at the second user turn.
        selected.Cut.CoveredEntryIds.Length.ShouldBe(1);
        selected.Cut.RetainedSuffixStart.ShouldBe(entries[1].Sequence);
    }

    [Fact]
    public async Task SelectAsync_WhenCausalPairWouldBeSplitAndNoUserTurnFollows_SelectsLargestSafeBoundary()
    {
        var selector = CreateSelector();
        var address = Address();
        var (call, result) = TestFactory.ToolCallPair(address, _branchId, callSequence: 3, resultSequence: 4);
        var entries = ImmutableArray.Create<SessionEntry>(
            TestFactory.MessageEntry(address, _branchId, 1),
            TestFactory.AssistantEntry(address, _branchId, 2),
            call,
            result);

        var selection = await selector.SelectAsync(
            SelectionRequest(entries, minimumRetainedEntries: 1), TestContext.Current.CancellationToken);

        // No boundary starts the suffix at a user turn, so the largest causally safe boundary (covering 2) wins.
        var selected = selection.ShouldBeOfType<CompactionCutSelected>();
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
    public async Task SelectAsync_WhenHistoryAlternatesUserAndAssistant_CutsBeforeAUserTurn()
    {
        var selector = CreateSelector();
        var address = Address();
        var entries = ImmutableArray.Create<SessionEntry>(
            TestFactory.MessageEntry(address, _branchId, 1, "u1"),
            TestFactory.AssistantEntry(address, _branchId, 2, "a2"),
            TestFactory.MessageEntry(address, _branchId, 3, "u3"),
            TestFactory.AssistantEntry(address, _branchId, 4, "a4"),
            TestFactory.MessageEntry(address, _branchId, 5, "u5"),
            TestFactory.AssistantEntry(address, _branchId, 6, "a6"));

        var result = await selector.SelectAsync(
            SelectionRequest(entries, minimumRetainedEntries: 1), TestContext.Current.CancellationToken);

        // The largest causally safe boundary (covering 5) would begin the retained suffix mid-turn at the assistant
        // reply; the selector backs off to the largest boundary that starts the suffix at a user turn.
        var selected = result.ShouldBeOfType<CompactionCutSelected>();
        selected.Cut.CoveredEntryIds.Length.ShouldBe(4);
        selected.Cut.RetainedSuffixStart.ShouldBe(entries[4].Sequence);
        selected.Cut.CoveredRange.EndInclusive.ShouldBe(entries[3].Sequence);
    }

    [Fact]
    public async Task SelectAsync_WhenNoUserTurnBoundarySatisfiesMinimumRetained_FallsBackToCausalBoundary()
    {
        var selector = CreateSelector();
        var address = Address();
        // Only the first entry is a user turn, so no boundary before a user turn can leave one entry retained.
        var entries = ImmutableArray.Create<SessionEntry>(
            TestFactory.MessageEntry(address, _branchId, 1, "u1"),
            TestFactory.AssistantEntry(address, _branchId, 2, "a2"),
            TestFactory.AssistantEntry(address, _branchId, 3, "a3"),
            TestFactory.AssistantEntry(address, _branchId, 4, "a4"));

        var result = await selector.SelectAsync(
            SelectionRequest(entries, minimumRetainedEntries: 1), TestContext.Current.CancellationToken);

        var selected = result.ShouldBeOfType<CompactionCutSelected>();
        selected.Cut.CoveredEntryIds.Length.ShouldBe(3);
        selected.Cut.RetainedSuffixStart.ShouldBe(entries[3].Sequence);
    }

    [Fact]
    public async Task SelectAsync_WhenUserTurnBoundaryWouldSplitCausalPair_PrefersEarlierSafeUserTurnBoundary()
    {
        var selector = CreateSelector();
        var address = Address();
        var (call, toolResult) = TestFactory.ToolCallPair(address, _branchId, callSequence: 4, resultSequence: 6);
        var entries = ImmutableArray.Create<SessionEntry>(
            TestFactory.MessageEntry(address, _branchId, 1, "u1"),
            TestFactory.AssistantEntry(address, _branchId, 2, "a2"),
            TestFactory.MessageEntry(address, _branchId, 3, "u3"),
            call,
            TestFactory.MessageEntry(address, _branchId, 5, "u5-steering"),
            toolResult,
            TestFactory.MessageEntry(address, _branchId, 7, "u7"));

        var result = await selector.SelectAsync(
            SelectionRequest(entries, minimumRetainedEntries: 2), TestContext.Current.CancellationToken);

        // Covering 4 (through the call) would start the suffix at the steering user turn but split the call from
        // its result; the largest safe user-turn boundary is covering 2 (suffix starts at u3).
        var selected = result.ShouldBeOfType<CompactionCutSelected>();
        selected.Cut.CoveredEntryIds.Length.ShouldBe(2);
        selected.Cut.RetainedSuffixStart.ShouldBe(entries[2].Sequence);
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
