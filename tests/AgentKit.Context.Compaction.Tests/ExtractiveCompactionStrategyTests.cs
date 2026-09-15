// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

using Microsoft.Extensions.Options;

public sealed class ExtractiveCompactionStrategyTests
{
    private readonly AgentId _agentId = new(Guid.NewGuid());
    private readonly SessionId _sessionId = new(Guid.NewGuid());
    private readonly BranchId _branchId = new(Guid.NewGuid());

    [Fact]
    public void Constructor_WhenEstimatorNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ExtractiveCompactionStrategy(null!, Options.Create(new CompactionOptions())));

        exception.ParamName.ShouldBe("estimator");
    }

    [Fact]
    public void Constructor_WhenOptionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ExtractiveCompactionStrategy(new CharacterCompactionSizeEstimator(Options.Create(new CompactionOptions())), null!));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task ProduceAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var strategy = CreateStrategy();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => strategy.ProduceAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task ProduceAsync_WhenCoveredEntriesHaveText_ProducesCheckpointWithConcatenatedText()
    {
        var strategy = CreateStrategy();
        var address = Address();
        var first = TestFactory.MessageEntry(address, _branchId, 1, "alpha");
        var second = TestFactory.MessageEntry(address, _branchId, 2, "beta");
        var source = Source([first, second]);
        var cut = new CompactionCut(
            new CompactionSourceRange(first.Sequence, second.Sequence),
            new SessionSequence(3),
            [first.Id, second.Id]);

        var result = await strategy.ProduceAsync(
            new CompactionStrategyRequest(TestFactory.Request(source.Context, _branchId, new SessionVersion(2), second.Sequence), source, cut),
            TestContext.Current.CancellationToken);

        var produced = result.ShouldBeOfType<CompactionCheckpointProduced>();
        var text = ((TextPart) produced.Checkpoint.Summary[0]).Text;
        text.ShouldContain("alpha");
        text.ShouldContain("beta");
        produced.Producer.StrategyKey.ShouldBe(ExtractiveCompactionStrategy.StrategyKey);
        produced.Producer.Deterministic.ShouldBeTrue();
    }

    [Fact]
    public async Task ProduceAsync_WhenCutCoversNoEntries_ReturnsUnsupported()
    {
        var strategy = CreateStrategy();
        var address = Address();
        var entry = TestFactory.MessageEntry(address, _branchId, 1, "alpha");
        var source = Source([entry]);
        var cut = new CompactionCut(
            new CompactionSourceRange(entry.Sequence, entry.Sequence),
            new SessionSequence(2),
            []);

        var result = await strategy.ProduceAsync(
            new CompactionStrategyRequest(TestFactory.Request(source.Context, _branchId, new SessionVersion(1), entry.Sequence), source, cut),
            TestContext.Current.CancellationToken);

        var unsupported = result.ShouldBeOfType<CompactionStrategyUnsupported>();
        unsupported.Rejection.Kind.ShouldBe(CompactionRejectionKind.NoSafeCut);
    }

    [Fact]
    public async Task ProduceAsync_WhenExtractExceedsMaximumCharacters_TruncatesWithMarker()
    {
        var strategy = CreateStrategy(maximumCheckpointCharacters: 40);
        var address = Address();
        var entry = TestFactory.MessageEntry(address, _branchId, 1, new string('x', 500));
        var source = Source([entry]);
        var cut = new CompactionCut(
            new CompactionSourceRange(entry.Sequence, entry.Sequence), new SessionSequence(2), [entry.Id]);

        var result = await strategy.ProduceAsync(
            new CompactionStrategyRequest(TestFactory.Request(source.Context, _branchId, new SessionVersion(1), entry.Sequence), source, cut),
            TestContext.Current.CancellationToken);

        var produced = result.ShouldBeOfType<CompactionCheckpointProduced>();
        var text = ((TextPart) produced.Checkpoint.Summary[0]).Text;
        text.Length.ShouldBeLessThanOrEqualTo(40);
        text.ShouldContain("truncated");
    }

    [Theory]
    [InlineData(38)]
    [InlineData(39)]
    [InlineData(40)]
    public async Task ProduceAsync_WhenTruncationBoundaryFallsInsideASurrogatePair_BacksOffWithoutSplittingIt(int maximumCharacters)
    {
        // The marker is 19 chars. The text is 222 UTF-16 units with an emoji at [9,10] and another at [211,212].
        // 38 → head 9 / tail 10: the tail cut splits the second emoji. 39 → head 10 / tail 10: both cuts split.
        // 40 → head 10 / tail 11: the head cut splits the first emoji.
        var strategy = CreateStrategy(maximumCheckpointCharacters: maximumCharacters);
        var address = Address();
        var emoji = "\U0001F600";
        var text = new string('a', 9) + emoji + new string('b', 200) + emoji + new string('c', 9);
        var entry = TestFactory.MessageEntry(address, _branchId, 1, text);
        var source = Source([entry]);
        var cut = new CompactionCut(
            new CompactionSourceRange(entry.Sequence, entry.Sequence), new SessionSequence(2), [entry.Id]);

        var result = await strategy.ProduceAsync(
            new CompactionStrategyRequest(TestFactory.Request(source.Context, _branchId, new SessionVersion(1), entry.Sequence), source, cut),
            TestContext.Current.CancellationToken);

        var produced = result.ShouldBeOfType<CompactionCheckpointProduced>();
        var summary = ((TextPart) produced.Checkpoint.Summary[0]).Text;
        summary.Length.ShouldBeLessThanOrEqualTo(maximumCharacters);
        summary.ShouldContain(ExtractiveCompactionStrategy.TruncationMarker);
        IsWellFormedUtf16(summary).ShouldBeTrue();
    }

    [Fact]
    public void TruncationMarker_WhenRead_IsTheMarkerEmbeddedInTruncatedSummaries() =>
        ExtractiveCompactionStrategy.TruncationMarker.ShouldBe("\n...[truncated]...\n");

    private static bool IsWellFormedUtf16(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                {
                    return false;
                }

                i++;
            }
            else if (char.IsLowSurrogate(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    [Fact]
    public async Task ProduceAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var strategy = CreateStrategy();
        var address = Address();
        var entry = TestFactory.MessageEntry(address, _branchId, 1, "alpha");
        var source = Source([entry]);
        var cut = new CompactionCut(
            new CompactionSourceRange(entry.Sequence, entry.Sequence), new SessionSequence(2), [entry.Id]);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => strategy.ProduceAsync(
                new CompactionStrategyRequest(TestFactory.Request(source.Context, _branchId, new SessionVersion(1), entry.Sequence), source, cut),
                cts.Token));
    }

    [Fact]
    public async Task ProduceAsync_WhenCoveredEntriesHaveNoExtractableText_ProducesPlaceholderSummary()
    {
        var strategy = CreateStrategy();
        var address = Address();
        var (call, _) = TestFactory.ToolCallPair(address, _branchId, callSequence: 1, resultSequence: 2);
        var source = Source([call]);
        var cut = new CompactionCut(
            new CompactionSourceRange(call.Sequence, call.Sequence), new SessionSequence(2), [call.Id]);

        var result = await strategy.ProduceAsync(
            new CompactionStrategyRequest(TestFactory.Request(source.Context, _branchId, new SessionVersion(1), call.Sequence), source, cut),
            TestContext.Current.CancellationToken);

        var produced = result.ShouldBeOfType<CompactionCheckpointProduced>();
        var text = ((TextPart) produced.Checkpoint.Summary[0]).Text;
        text.ShouldBe("(no extractable text in covered entries)");
    }

    private SessionAddress Address() => new(_agentId, _sessionId);

    private CompactionSourceSnapshot Source(ImmutableArray<SessionEntry> entries)
    {
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        return new CompactionSourceSnapshot(
            context, _branchId, new SessionVersion(entries.Length), entries.IsEmpty ? new SessionSequence(0) : entries[^1].Sequence, entries);
    }

    private static ExtractiveCompactionStrategy CreateStrategy(
        double charactersPerToken = 4.0, int maximumCheckpointCharacters = 16_000)
    {
        var options = Options.Create(new CompactionOptions
        {
            CharactersPerToken = charactersPerToken,
            MaximumCheckpointCharacters = maximumCheckpointCharacters
        });
        return new ExtractiveCompactionStrategy(new CharacterCompactionSizeEstimator(options), options);
    }
}
