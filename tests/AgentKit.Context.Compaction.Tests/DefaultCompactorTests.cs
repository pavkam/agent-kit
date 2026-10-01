// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies DefaultCompactor behavior and contracts.</summary>
public sealed class DefaultCompactorTests: IDisposable
{
    private readonly List<ServiceProvider> _providers = [];
    private readonly AgentId _agentId = new(Guid.NewGuid());
    private readonly SessionId _sessionId = new(Guid.NewGuid());
    private readonly BranchId _branchId = new(Guid.NewGuid());
    [Fact]
    public void Constructor_WhenCoordinatorNull_ThrowsArgumentNullException()
    {
        var provider = BuildProvider(new FakeSessionCoordinator(_branchId));
        var key = AgentContextCompactionComponentDefaults.CompactorKey;

        var exception = Should.Throw<ArgumentNullException>(() => new DefaultCompactor(
            null!,
            provider.GetRequiredKeyedService<ICompactionCutSelector>(CompactionServiceKeys.CutSelector(key)),
            provider.GetRequiredKeyedService<ICompactionStrategyResolver>(CompactionServiceKeys.StrategyResolver(key)),
            provider.GetRequiredKeyedService<ICompactionValidator>(CompactionServiceKeys.Validator(key)),
            provider.GetRequiredKeyedService<ICompactionActivationCoordinator>(CompactionServiceKeys.ActivationCoordinator(key)),
            provider.GetRequiredKeyedService<ICompactionEventDispatcher>(CompactionServiceKeys.EventDispatcher(key)),
            provider.GetRequiredService<ICompactionSizeEstimator>(),
            provider.GetRequiredService<IIdentifierGenerator<CompactionManifestId>>(),
            provider.GetRequiredService<IIdentifierGenerator<SessionEntryId>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IOptions<CompactionOptions>>(),
            provider.GetRequiredKeyedService<ContextCompactionOptionsSnapshot>(key.Value)));

        exception.ParamName.ShouldBe("coordinator");
    }

    [Fact]
    public async Task CompactAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var (compactor, _) = CreateCompactor();
        var exception = await Should.ThrowAsync<ArgumentNullException>(() => compactor.CompactAsync(null!, TestContext.Current.CancellationToken));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task CompactAsync_WhenHistoryTooShortForMinimumRetained_ReturnsCompactionRejected()
    {
        var (compactor, coordinator) = CreateCompactor();
        var address = Address();
        var entries = new[]
        {
            TestFactory.MessageEntry(address, _branchId, 1, "one"),
            TestFactory.MessageEntry(address, _branchId, 2, "two")
        };
        coordinator.Seed(entries);
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(context, _branchId, coordinator.Version, new SessionSequence(2), minimumRetainedEntries: 5);
        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);
        var rejected = result.ShouldBeOfType<CompactionRejected>();
        rejected.Rejection.Kind.ShouldBe(CompactionRejectionKind.NoSafeCut);
        coordinator.ReceivedAppends.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompactAsync_WhenSuccessful_AppendsCompactionEntryAndReturnsSucceeded()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        var entries = Enumerable.Range(1, 10).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string((char) ('a' + (i % 26)), 200))).ToArray();
        coordinator.Seed(entries);
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(context, _branchId, coordinator.Version, new SessionSequence(10), minimumRetainedEntries: 2, minimumReductionRatio: 0.1);
        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);
        var succeeded = result.ShouldBeOfType<CompactionSucceeded>();
        succeeded.Record.Status.ShouldBe(CompactionRecordStatus.Active);
        succeeded.Record.ActivatedSessionVersion.ShouldBe(new SessionVersion(11));
        _ = coordinator.ReceivedAppends.ShouldHaveSingleItem();
        var appended = coordinator.ReceivedAppends[0];
        appended.Entries.Length.ShouldBe(1);
        var committedEntry = appended.Entries[0].ShouldBeOfType<CompactionSessionEntry>();
        committedEntry.Sequence.ShouldBe(new SessionSequence(11));
        _ = committedEntry.Record.Checkpoint.ShouldNotBeNull();
    }

    [Fact]
    public async Task CompactAsync_WhenCutCoversAnEarlierActiveRecord_NamesItInSupersedes()
    {
        // docs/architecture/context-compaction.md: "A newer active record names the prior CompactionId
        // in Supersedes; it does not mutate the older record." A second compaction whose cut covers the
        // first compaction's entry must carry that first record's CompactionId as Supersedes.
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 10).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string((char) ('a' + (i % 26)), 200))));
        var firstContext = TestFactory.CompactionContext(_agentId, _sessionId);
        var firstRequest = TestFactory.Request(firstContext, _branchId, coordinator.Version, new SessionSequence(10), minimumRetainedEntries: 2, minimumReductionRatio: 0.1);
        var firstResult = (await compactor.CompactAsync(firstRequest, TestContext.Current.CancellationToken)).ShouldBeOfType<CompactionSucceeded>();

        coordinator.Seed(Enumerable.Range(12, 10).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string((char) ('a' + (i % 26)), 200))));
        var secondContext = TestFactory.CompactionContext(_agentId, _sessionId);
        var secondRequest = TestFactory.Request(secondContext, _branchId, coordinator.Version, coordinator.TipSequence, minimumRetainedEntries: 2, minimumReductionRatio: 0.1);
        var secondResult = (await compactor.CompactAsync(secondRequest, TestContext.Current.CancellationToken)).ShouldBeOfType<CompactionSucceeded>();

        secondResult.Record.Supersedes.ShouldBe(firstResult.Record.Context.CompactionId);
    }

    [Fact]
    public async Task CompactAsync_WhenSuccessful_AppendsAnEntryTheFirstPartyCodecCatalogCanRoundTrip()
    {
        // The loop, stores, and every codec use schema version "1"; an entry authored with a different embedded version
        // is persisted (the compaction codec does not cross-check) and then rejected on every later read of that session.
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 10).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string((char) ('a' + (i % 26)), 200))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(10), minimumRetainedEntries: 2, minimumReductionRatio: 0.1);
        _ = (await compactor.CompactAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<CompactionSucceeded>();
        var committed = coordinator.ReceivedAppends.Single().Entries.Single().ShouldBeOfType<CompactionSessionEntry>();
        var catalog = new Session.SessionEntryCodecCatalog([new Session.CompactionSessionEntryCodec()], TimeProvider.System);

        var encoded = catalog.Encode(committed).ShouldBeOfType<SessionEntryEncoded>();
        var decoded = catalog.Decode(encoded.Wire);

        _ = decoded.ShouldBeOfType<SessionEntryDecoded>();
    }

    [Fact]
    public async Task CompactAsync_WhenBranchVersionIsNotEqualToEntryCount_UsesTheLastCommittedSequenceNotVersionPlusOne()
    {
        // Real stores advance Version once per append but Sequence once per entry; after any multi-entry append
        // (run start, batch tool results) Version + 1 is a stale sequence and the store rejects the compaction entry.
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 10).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('h', 200))));
        coordinator.SetVersion(new SessionVersion(4)); // 10 entries committed across 4 appends.
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(10), minimumRetainedEntries: 2, minimumReductionRatio: 0.1);

        _ = (await compactor.CompactAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<CompactionSucceeded>();

        coordinator.ReceivedAppends.Single().Entries.Single().Sequence.ShouldBe(new SessionSequence(11));
    }

    [Fact]
    public async Task CompactAsync_WhenSourceThroughPrecedesBranchTip_CoversOnlyEligibleEntriesAndAppendsAfterTip()
    {
        // The branch has 10 entries but only sequences 1..6 are eligible. Coverage must stop at 6, and the new entry
        // must still be allocated after the real branch tip (11), not after the eligible bound (7).
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30, sourceReadPageSize: 4);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 10).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('q', 200))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(6), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var succeeded = result.ShouldBeOfType<CompactionSucceeded>();
        succeeded.Record.Manifest.CoveredRange.EndInclusive.Value.ShouldBeLessThanOrEqualTo(6);
        succeeded.Record.Manifest.RetainedSuffixStart.Value.ShouldBeLessThanOrEqualTo(6);
        var committed = coordinator.ReceivedAppends.Single().Entries.Single().ShouldBeOfType<CompactionSessionEntry>();
        committed.Sequence.ShouldBe(new SessionSequence(11));
        coordinator.Entries.Count.ShouldBe(11);
    }

    [Fact]
    public async Task CompactAsync_WhenSourceThroughExceedsBranchTip_ReturnsNonRetryableFailedWithoutAppend()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('q', 200))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(9), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<CompactionFailed>();
        failed.Failure.Kind.ShouldBe(CompactionFailureKind.SourceUnavailable);
        failed.Failure.Retryable.ShouldBeFalse();
        coordinator.ReceivedAppends.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompactAsync_WhenIneligibleTailDependsOnCoveredEntry_ReturnsNoSafeCutWithoutAppend()
    {
        // A tool call at 6 is eligible but its result at 7 is beyond SourceThrough; covering the call would split the pair.
        // The head is assistant-only so no user-turn boundary lets the selector avoid the call on its own.
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        var head = Enumerable.Range(1, 5).Select(i => TestFactory.AssistantEntry(address, _branchId, i, new string('q', 200)));
        var (call, toolResult) = TestFactory.ToolCallPair(address, _branchId, callSequence: 6, resultSequence: 7);
        coordinator.Seed([.. head, call, toolResult]);
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(6), minimumRetainedEntries: 0, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<CompactionRejected>();
        rejected.Rejection.Kind.ShouldBe(CompactionRejectionKind.NoSafeCut);
        coordinator.ReceivedAppends.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompactAsync_WhenSourceVersionDoesNotMatchBranch_ReturnsConflictBeforeStrategy()
    {
        var address = Address();
        var strategyInvocations = 0;
        var strategy = new FakeCompactionStrategy
        {
            OnProduce = _ => throw new InvalidOperationException($"strategy must not run; invocation {++strategyInvocations}")
        };
        var (compactor, coordinator) = CreateCompactorWithFakes(strategy: strategy);
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('h', 200))));
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        // Compute the request against a stale version — one behind the branch's actual, seeded version.
        var staleRequest = TestFactory.Request(context, _branchId, new SessionVersion(coordinator.Version.Value - 1), new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(staleRequest, TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<CompactionConflict>();
        conflict.ExpectedVersion.ShouldBe(staleRequest.SourceVersion);
        conflict.ActualVersion.ShouldBe(coordinator.Version);
        conflict.Manifest.ShouldBeNull();
        strategyInvocations.ShouldBe(0);
        coordinator.ReceivedAppends.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompactAsync_WhenBranchAdvancesBetweenReadAndAppend_ReturnsConflictWithManifest()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('h', 200))));
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(context, _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);
        var newerVersion = new SessionVersion(coordinator.Version.Value + 1);
        coordinator.AppendOverride = append => new SessionAppendConflict(append.ExpectedVersion, newerVersion);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<CompactionConflict>();
        conflict.ExpectedVersion.ShouldBe(request.SourceVersion);
        conflict.ActualVersion.ShouldBe(newerVersion);
        _ = conflict.Manifest.ShouldNotBeNull();
    }

    [Fact]
    public async Task CompactAsync_WhenReadingMultiplePages_PinsContinuationReadsToTheFirstPageSnapshot()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30, sourceReadPageSize: 3);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 10).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('p', 200))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(10), minimumRetainedEntries: 2, minimumReductionRatio: 0.1);

        _ = (await compactor.CompactAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<CompactionSucceeded>();

        coordinator.ReceivedReads.Count.ShouldBe(4);
        coordinator.ReceivedReads[0].Snapshot.ShouldBeNull();
        var issued = new SessionReadSnapshot(address, _branchId, request.SourceVersion, new SessionSequence(10));
        foreach (var continuation in coordinator.ReceivedReads.Skip(1))
        {
            continuation.Snapshot.ShouldBe(issued);
        }
    }

    [Fact]
    public async Task CompactAsync_WhenAppendOccursBetweenPages_ReadsThePinnedPrefixAndReturnsConflict()
    {
        // A pinned continuation excludes the concurrent entry, so the snapshot stays consistent; activation then fails
        // the version check rather than overwriting or silently including the newer entry.
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30, sourceReadPageSize: 4);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 10).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('p', 200))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(10), minimumRetainedEntries: 2, minimumReductionRatio: 0.1);
        var interleaved = false;
        coordinator.OnRead = read =>
        {
            if (read.Snapshot is not null && !interleaved)
            {
                interleaved = true;
                coordinator.Seed([TestFactory.MessageEntry(address, _branchId, 11, "concurrent")]);
            }
        };

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var conflict = result.ShouldBeOfType<CompactionConflict>();
        conflict.ExpectedVersion.ShouldBe(request.SourceVersion);
        conflict.ActualVersion.ShouldBe(coordinator.Version);
        conflict.Manifest.ShouldNotBeNull().CoveredRange.EndInclusive.Value.ShouldBeLessThanOrEqualTo(8);
        coordinator.Entries.Count.ShouldBe(11);
    }

    [Fact]
    public async Task CompactAsync_WhenBranchChangesBetweenPages_ReturnsTypedFailure()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30, sourceReadPageSize: 2);
        var address = Address();
        var entries = Enumerable.Range(1, 4).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('p', 200))).ToArray();
        var firstSnapshot = new SessionReadSnapshot(address, _branchId, new SessionVersion(4), new SessionSequence(4));
        var driftedSnapshot = new SessionReadSnapshot(address, _branchId, new SessionVersion(5), new SessionSequence(5));
        coordinator.ReadOverride = read => read.Snapshot is null
            ? new SessionPage([entries[0], entries[1]], entries[1].Sequence, hasMore: true, firstSnapshot)
            : new SessionPage([entries[2], entries[3]], entries[3].Sequence, hasMore: false, driftedSnapshot);
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, new SessionVersion(4), new SessionSequence(4), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<CompactionFailed>();
        failed.Failure.Kind.ShouldBe(CompactionFailureKind.SourceUnavailable);
        failed.Failure.Retryable.ShouldBeTrue();
        coordinator.ReceivedAppends.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompactAsync_WhenReductionInsufficient_ReturnsCompactionNotReducing()
    {
        var (compactor, coordinator) = CreateCompactor();
        var address = Address();
        var entries = new[]
        {
            TestFactory.MessageEntry(address, _branchId, 1, "short")
        };
        coordinator.Seed(entries);
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(context, _branchId, coordinator.Version, new SessionSequence(1), minimumRetainedEntries: 0, minimumReductionRatio: 0.9);
        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<CompactionNotReducing>();
        coordinator.ReceivedAppends.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompactAsync_WhenSourceReadFails_ReturnsCompactionFailed()
    {
        var (compactor, coordinator) = CreateCompactor();
        coordinator.ReadOverride = static request => new SessionReadFailed("boom");
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(context, _branchId, new SessionVersion(0), new SessionSequence(1));
        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<CompactionFailed>();
        failed.Failure.Kind.ShouldBe(CompactionFailureKind.SourceUnavailable);
    }

    [Fact]
    public async Task CompactAsync_WhenSourceReadNotFound_ReturnsNonRetryableCompactionFailed()
    {
        var (compactor, coordinator) = CreateCompactor();
        coordinator.ReadOverride = static request => new SessionReadNotFound(request.Context.ToAddress());
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, new SessionVersion(0), new SessionSequence(1));

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<CompactionFailed>();
        failed.Failure.Kind.ShouldBe(CompactionFailureKind.SourceUnavailable);
        failed.Failure.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task CompactAsync_WhenSourceReadFails_ReturnsRetryableCompactionFailed()
    {
        var (compactor, coordinator) = CreateCompactor();
        coordinator.ReadOverride = static request => new SessionReadFailed("transient");
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, new SessionVersion(0), new SessionSequence(1));

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<CompactionFailed>().Failure.Retryable.ShouldBeTrue();
    }

    [Fact]
    public async Task CompactAsync_WhenStrategyMisreportsItsOwnSize_ManifestCarriesTheCompactorsIndependentEstimate()
    {
        var address = Address();
        var checkpoint = new CompactionCheckpoint([new TextPart(new string('s', 400), TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var strategy = new FakeCompactionStrategy
        {
            OnProduce = _ => new CompactionCheckpointProduced(
                checkpoint,
                new CompactionProducer(new CompactionStrategyKey("test.misreporting"), deterministic: true, ExtensionData.Empty),
                new CompactionSizeEstimate(1, 1, 1))
        };
        var validator = new FakeCompactionValidator { OnValidate = request => new CompactionValidated(new ValidatedCompaction(request.Candidate, new CompactionValidationStamp(new CompactionValidatorVersion("1"), new ContentHash("sha256:candidate"), DateTimeOffset.UnixEpoch), [])) };
        var (compactor, coordinator) = CreateCompactorWithFakes(strategy: strategy, validator: validator);
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('m', 2000))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);
        var expectedAfter = CreateEstimator().EstimateCheckpoint(checkpoint);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var succeeded = result.ShouldBeOfType<CompactionSucceeded>();
        succeeded.Record.Manifest.After.ShouldBe(expectedAfter);
        succeeded.Record.Manifest.After.Tokens.ShouldNotBe(1);
    }

    [Fact]
    public async Task CompactAsync_WhenNotReducing_ReportsTheCompactorsIndependentEstimates()
    {
        var address = Address();
        var checkpoint = new CompactionCheckpoint([new TextPart(new string('s', 400), TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var strategy = new FakeCompactionStrategy
        {
            OnProduce = _ => new CompactionCheckpointProduced(
                checkpoint,
                new CompactionProducer(new CompactionStrategyKey("test.misreporting"), deterministic: true, ExtensionData.Empty),
                new CompactionSizeEstimate(1, 1, 1))
        };
        var (compactor, coordinator) = CreateCompactorWithFakes(strategy: strategy);
        coordinator.Seed(Enumerable.Range(1, 2).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('m', 100))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(2), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var notReducing = result.ShouldBeOfType<CompactionNotReducing>();
        notReducing.After.ShouldBe(CreateEstimator().EstimateCheckpoint(checkpoint));
        notReducing.Before.EntryCount.ShouldBe(1);
    }

    [Fact]
    public async Task CompactAsync_WhenCutCoversASubset_ManifestBeforeEstimateCountsOnlyCoveredEntries()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 10).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('b', 200))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(10), minimumRetainedEntries: 3, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var succeeded = result.ShouldBeOfType<CompactionSucceeded>();
        var coveredCount = succeeded.Record.Manifest.CoveredRange.EndInclusive.Value - succeeded.Record.Manifest.CoveredRange.StartInclusive.Value + 1;
        succeeded.Record.Manifest.Before.EntryCount.ShouldBe((int) coveredCount);
        succeeded.Record.Manifest.Before.Bytes.ShouldBe(coveredCount * 200);
    }

    [Fact]
    public async Task CompactAsync_WhenAppendFailsAndNoRecordWasCommitted_ReturnsNonRetryableCompactionFailed()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        var entries = Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('z', 200))).ToArray();
        coordinator.Seed(entries);
        coordinator.AppendOverride = static request => new SessionAppendFailed("store unavailable");
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(context, _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);
        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<CompactionFailed>();
        failed.Failure.Kind.ShouldBe(CompactionFailureKind.ActivationFailure);
        failed.Failure.Retryable.ShouldBeFalse();
        failed.Failure.SafeMessage.ShouldContain("store unavailable");
        coordinator.ReceivedReads.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CompactAsync_WhenRetriedWithSameCompactionId_ReportsExistingActiveRecord()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 10).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('r', 200))));
        var context = TestFactory.CompactionContext(_agentId, _sessionId, new CompactionId(Guid.NewGuid()));
        var request = TestFactory.Request(context, _branchId, coordinator.Version, new SessionSequence(10), minimumRetainedEntries: 2, minimumReductionRatio: 0.1);
        var first = (await compactor.CompactAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<CompactionSucceeded>();

        // The caller lost the first response and replays the identical request under the same CompactionId.
        var retry = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var succeeded = retry.ShouldBeOfType<CompactionSucceeded>();
        succeeded.Record.ShouldBe(first.Record);
        _ = coordinator.ReceivedAppends.ShouldHaveSingleItem();
        coordinator.Entries.Count.ShouldBe(11);
    }

    [Fact]
    public async Task CompactAsync_WhenAppendCommittedButResponseWasLost_ReconcilesToTheCommittedRecord()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('z', 200))));
        coordinator.AppendOverride = append =>
        {
            coordinator.Seed(append.Entries);
            return new SessionAppendFailed("response lost");
        };
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var succeeded = result.ShouldBeOfType<CompactionSucceeded>();
        var committed = coordinator.Entries[^1].ShouldBeOfType<CompactionSessionEntry>();
        succeeded.Record.ShouldBe(committed.Record);
        succeeded.Record.Context.CompactionId.ShouldBe(request.Context.CompactionId);
    }

    [Fact]
    public async Task CompactAsync_WhenAppendConflictsWithADuplicateAttemptOfTheSameCompactionId_ReportsExistingActiveRecord()
    {
        // Two workers race the same logical checkpoint: the other worker's identical CompactionId wins the version
        // check, so this attempt's conflict is really "already active".
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('z', 200))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);
        coordinator.AppendOverride = append =>
        {
            var expected = append.ExpectedVersion;
            coordinator.Seed(append.Entries);
            return new SessionAppendConflict(expected, coordinator.Version);
        };

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var succeeded = result.ShouldBeOfType<CompactionSucceeded>();
        succeeded.Record.ShouldBe(coordinator.Entries[^1].ShouldBeOfType<CompactionSessionEntry>().Record);
    }

    [Fact]
    public async Task CompactAsync_WhenDeadlineAlreadyPassed_ReturnsDeadlineExceededWithoutReadingOrAppending()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30, timeProvider: new FakeTimeProvider(DateTimeOffset.UnixEpoch.AddMinutes(10)));
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('d', 200))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<CompactionRejected>();
        rejected.Rejection.Kind.ShouldBe(CompactionRejectionKind.DeadlineExceeded);
        coordinator.ReceivedReads.ShouldBeEmpty();
        coordinator.ReceivedAppends.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompactAsync_WhenClockIsExactlyAtDeadline_ReturnsDeadlineExceeded()
    {
        var address = Address();
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, new SessionVersion(5), new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30, timeProvider: new FakeTimeProvider(request.Deadline));
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('d', 200))));

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<CompactionRejected>().Rejection.Kind.ShouldBe(CompactionRejectionKind.DeadlineExceeded);
    }

    [Fact]
    public async Task CompactAsync_WhenClockIsJustBeforeDeadline_Proceeds()
    {
        var address = Address();
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, new SessionVersion(5), new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30, timeProvider: new FakeTimeProvider(request.Deadline.AddTicks(-1)));
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('d', 200))));

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<CompactionSucceeded>();
    }

    [Fact]
    public async Task CompactAsync_WhenStoreReportsUnexpectedNewVersion_ReturnsActivationFailureAndLogsWarning()
    {
        // The record is built before the append with ActivatedSessionVersion = SourceVersion + 1. If the store reports
        // anything else, the persisted record's version claim is false and must not be reported as a clean success.
        var logger = new RecordingLogger<DefaultCompactor>();
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30, logger: logger);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('v', 200))));
        coordinator.AppendOverride = append => new SessionAppended(new SessionVersion(append.ExpectedVersion.Value + 2), append.Entries);
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<CompactionFailed>();
        failed.Failure.Kind.ShouldBe(CompactionFailureKind.ActivationFailure);
        failed.Failure.Retryable.ShouldBeFalse();
        var warning = logger.Snapshot().Where(static entry => entry.Level == LogLevel.Warning).ShouldHaveSingleItem();
        warning.EventId.Id.ShouldBe(9004);
        warning.State["ExpectedVersion"].ShouldBe(new SessionVersion(request.SourceVersion.Value + 1));
        warning.State["ReportedVersion"].ShouldBe(new SessionVersion(request.SourceVersion.Value + 2));
        warning.Message.ShouldNotContain("vvvv");
    }

    [Fact]
    public async Task CompactAsync_WhenStoreReportsExpectedNewVersion_ReturnsSucceededWithMatchingActivatedVersion()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('v', 200))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var succeeded = result.ShouldBeOfType<CompactionSucceeded>();
        succeeded.Record.ActivatedSessionVersion.ShouldBe(coordinator.Version);
    }

    [Fact]
    public async Task CompactAsync_WhenCancelledBeforeActivation_ReturnsCancelledNotAttemptedAndLogsCancellation()
    {
        var address = Address();
        using var cts = new CancellationTokenSource();
        var strategy = new FakeCompactionStrategy
        {
            OnProduce = _ =>
            {
                cts.Cancel();
                cts.Token.ThrowIfCancellationRequested();
                return null!;
            }
        };
        var logger = new RecordingLogger<DefaultCompactor>();
        var (compactor, coordinator) = CreateCompactorWithFakes(strategy: strategy, logger: logger);
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('c', 200))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, cts.Token);

        var cancelled = result.ShouldBeOfType<CompactionCancelled>();
        cancelled.CommitState.ShouldBe(CompactionCommitState.NotAttempted);
        cancelled.CommittedRecord.ShouldBeNull();
        coordinator.ReceivedAppends.ShouldBeEmpty();
        var entry = logger.Snapshot().Where(static e => e.EventId.Id == 9002).ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Debug);
        entry.State["CommitState"].ShouldBe(CompactionCommitState.NotAttempted);
    }

    [Fact]
    public async Task CompactAsync_WhenCancelledAfterAppendCommitted_ReturnsCancelledWithCommittedState()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('c', 200))));
        using var cts = new CancellationTokenSource();
        coordinator.AppendOverride = append =>
        {
            // The store commits, then the caller's token fires before the response is observed.
            coordinator.Seed(append.Entries);
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        };
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, cts.Token);

        var cancelled = result.ShouldBeOfType<CompactionCancelled>();
        cancelled.CommitState.ShouldBe(CompactionCommitState.Committed);
        var committed = coordinator.Entries[^1].ShouldBeOfType<CompactionSessionEntry>();
        cancelled.CommittedRecord.ShouldBe(committed.Record);
        // Reconciliation must not itself be cancelled by the caller's token.
        coordinator.ReceivedReads[^1].Snapshot.ShouldBeNull();
        coordinator.ReceivedReads.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CompactAsync_WhenCancelledDuringAppendThatDidNotCommit_ReturnsCancelledNotCommitted()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('c', 200))));
        using var cts = new CancellationTokenSource();
        coordinator.AppendOverride = _ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        };
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, cts.Token);

        var cancelled = result.ShouldBeOfType<CompactionCancelled>();
        cancelled.CommitState.ShouldBe(CompactionCommitState.NotCommitted);
        cancelled.CommittedRecord.ShouldBeNull();
        coordinator.Entries.Count.ShouldBe(5);
    }

    [Fact]
    public async Task CompactAsync_WhenCancelledDuringAppendAndReconciliationUnavailable_ReturnsCancelledUnknown()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('c', 200))));
        using var cts = new CancellationTokenSource();
        var appended = false;
        coordinator.AppendOverride = _ =>
        {
            appended = true;
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        };
        coordinator.OnRead = _ => coordinator.ReadOverride = appended ? static _ => new SessionReadFailed("store unavailable") : null;
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, cts.Token);

        var cancelled = result.ShouldBeOfType<CompactionCancelled>();
        cancelled.CommitState.ShouldBe(CompactionCommitState.Unknown);
        cancelled.CommittedRecord.ShouldBeNull();
    }

    [Fact]
    public async Task CompactAsync_WhenCancelled_EmitsCancelledOutcomeOnActivity()
    {
        var address = Address();
        using var cts = new CancellationTokenSource();
        var strategy = new FakeCompactionStrategy
        {
            OnProduce = _ =>
            {
                cts.Cancel();
                cts.Token.ThrowIfCancellationRequested();
                return null!;
            }
        };
        var (compactor, coordinator) = CreateCompactorWithFakes(strategy: strategy);
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('c', 200))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);
        using var activities = new ActivityCollector(static source => source.Name == AgentKitDiagnostics.ActivitySourceName, activity => activity.OperationName == AgentKitActivityNames.ContextCompact && Equals(activity.GetTagItem(AgentKitTagNames.CompactionId), request.Context.CompactionId.ToString()));

        _ = (await compactor.CompactAsync(request, cts.Token)).ShouldBeOfType<CompactionCancelled>();

        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("cancelled");
    }

    [Fact]
    public async Task CompactAsync_WhenAppendFailsAndReconciliationReadFails_ReturnsNonRetryableCompactionFailed()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('z', 200))));
        var appended = false;
        coordinator.AppendOverride = _ =>
        {
            appended = true;
            return new SessionAppendFailed("store unavailable");
        };
        // Source reads succeed; only the reconciliation read after the failed append is unavailable.
        coordinator.OnRead = _ => coordinator.ReadOverride = appended ? static _ => new SessionReadFailed("store unavailable") : null;
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<CompactionFailed>();
        failed.Failure.Kind.ShouldBe(CompactionFailureKind.ActivationFailure);
        failed.Failure.Retryable.ShouldBeFalse();
        failed.Failure.SafeMessage.ShouldContain("could not be reconciled");
    }

    [Fact]
    public async Task CompactAsync_WhenCutSplitsToolCall_NeverActivatesAcrossTheBoundary()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        var (call, toolResult) = TestFactory.ToolCallPair(address, _branchId, callSequence: 1, resultSequence: 2);
        var tail = Enumerable.Range(3, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('w', 200))).ToArray();
        var entries = new SessionEntry[]
        {
            call,
            toolResult
        }.Concat(tail).ToArray();
        coordinator.Seed(entries);
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(context, _branchId, coordinator.Version, new SessionSequence(entries.Length), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);
        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);
        var succeeded = result.ShouldBeOfType<CompactionSucceeded>();
        succeeded.Record.Manifest.CoveredRange.EndInclusive.Value.ShouldBeGreaterThanOrEqualTo(toolResult.Sequence.Value);
    }

    [Fact]
    public async Task CompactAsync_WhenCutSelectionFails_ReturnsCompactionFailed()
    {
        var address = Address();
        var entries = new[]
        {
            TestFactory.MessageEntry(address, _branchId, 1, "one")
        };
        var cutSelector = new FakeCompactionCutSelector
        {
            OnSelect = static _ => new CompactionCutSelectionFailed(new CompactionFailure(CompactionFailureKind.Unknown, "boom", retryable: false, ExtensionData.Empty))
        };
        var (compactor, coordinator) = CreateCompactorWithFakes(cutSelector: cutSelector);
        coordinator.Seed(entries);
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(context, _branchId, coordinator.Version, new SessionSequence(1));
        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<CompactionFailed>();
        failed.Failure.SafeMessage.ShouldBe("boom");
    }

    [Fact]
    public async Task CompactAsync_WhenStrategyThrowsUnexpectedly_PropagatesTheExceptionAndLogsFailure()
    {
        var address = Address();
        var entries = new[]
        {
            TestFactory.MessageEntry(address, _branchId, 1, "one"),
        };
        var strategy = new FakeCompactionStrategy
        {
            OnProduce = static _ => throw new InvalidOperationException("unexpected strategy failure"),
        };
        var logger = new RecordingLogger<DefaultCompactor>();
        var (compactor, coordinator) = CreateCompactorWithFakes(strategy: strategy, logger: logger);
        coordinator.Seed(entries);
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(context, _branchId, coordinator.Version, new SessionSequence(1), minimumRetainedEntries: 0);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await compactor.CompactAsync(request, TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("unexpected strategy failure");
        var entry = logger.Snapshot().Where(static e => e.EventId.Id == 9003).ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Error);
        entry.State["ErrorType"].ShouldBe(typeof(InvalidOperationException).FullName);
        entry.Message.ShouldNotContain("unexpected strategy failure");
    }

    [Fact]
    public async Task CompactAsync_WhenStrategyDeclinesToProduce_ReturnsCompactionRejected()
    {
        var address = Address();
        var entries = new[]
        {
            TestFactory.MessageEntry(address, _branchId, 1, "one")
        };
        var strategy = new FakeCompactionStrategy
        {
            OnProduce = static _ => new CompactionStrategyUnsupported(new CompactionRejection(CompactionRejectionKind.NoSafeCut, "unsupported", ExtensionData.Empty))
        };
        var (compactor, coordinator) = CreateCompactorWithFakes(strategy: strategy);
        coordinator.Seed(entries);
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(context, _branchId, coordinator.Version, new SessionSequence(1), minimumRetainedEntries: 0);
        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);
        var rejected = result.ShouldBeOfType<CompactionRejected>();
        rejected.Rejection.SafeMessage.ShouldBe("unsupported");
    }

    [Fact]
    public async Task CompactAsync_WhenStrategyFails_ReturnsCompactionFailed()
    {
        var address = Address();
        var entries = new[]
        {
            TestFactory.MessageEntry(address, _branchId, 1, "one")
        };
        var strategy = new FakeCompactionStrategy
        {
            OnProduce = static _ => new CompactionStrategyFailed(new CompactionFailure(CompactionFailureKind.StrategyFailure, "strategy boom", retryable: true, ExtensionData.Empty))
        };
        var (compactor, coordinator) = CreateCompactorWithFakes(strategy: strategy);
        coordinator.Seed(entries);
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(context, _branchId, coordinator.Version, new SessionSequence(1), minimumRetainedEntries: 0);
        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<CompactionFailed>();
        failed.Failure.SafeMessage.ShouldBe("strategy boom");
    }

    [Fact]
    public async Task CompactAsync_WhenValidationFails_ReturnsCompactionFailed()
    {
        var address = Address();
        var entries = new[]
        {
            TestFactory.MessageEntry(address, _branchId, 1, "one")
        };
        var validator = new FakeCompactionValidator
        {
            OnValidate = static _ => new CompactionValidationFailed(new CompactionFailure(CompactionFailureKind.ValidationFailure, "validation boom", retryable: false, ExtensionData.Empty))
        };
        var (compactor, coordinator) = CreateCompactorWithFakes(validator: validator);
        coordinator.Seed(entries);
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(context, _branchId, coordinator.Version, new SessionSequence(1), minimumRetainedEntries: 0);
        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<CompactionFailed>();
        failed.Failure.SafeMessage.ShouldBe("validation boom");
    }

    [Fact]
    public async Task CompactAsync_WhenValidationRejectedWithMultipleIssues_ReturnsCompactionRejectedWithCombinedMessage()
    {
        var address = Address();
        var entries = new[]
        {
            TestFactory.MessageEntry(address, _branchId, 1, "one")
        };
        var validator = new FakeCompactionValidator
        {
            OnValidate = static _ => new CompactionValidationRejected([new CompactionValidationIssue(CompactionValidationIssueKind.InvalidStructure, "issue one", []), new CompactionValidationIssue(CompactionValidationIssueKind.UnboundedContent, "issue two", [])])
        };
        var (compactor, coordinator) = CreateCompactorWithFakes(validator: validator);
        coordinator.Seed(entries);
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(context, _branchId, coordinator.Version, new SessionSequence(1), minimumRetainedEntries: 0);
        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);
        var rejected = result.ShouldBeOfType<CompactionRejected>();
        rejected.Rejection.Kind.ShouldBe(CompactionRejectionKind.PolicyViolation);
        rejected.Rejection.SafeMessage.ShouldContain("issue one");
        rejected.Rejection.SafeMessage.ShouldContain("issue two");
    }

    [Fact]
    public async Task CompactAsync_WhenSessionOrBranchNotFoundDuringActivation_ReturnsCompactionFailed()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        var entries = Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('z', 200))).ToArray();
        coordinator.Seed(entries);
        coordinator.AppendOverride = request => new SessionAppendNotFound(request.Context.ToAddress());
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(context, _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);
        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);
        var failed = result.ShouldBeOfType<CompactionFailed>();
        failed.Failure.Kind.ShouldBe(CompactionFailureKind.ActivationFailure);
        failed.Failure.Retryable.ShouldBeFalse();
    }

    [Fact]
    public async Task CompactAsync_WhenModelBackedStrategyProducesSummary_ActivatesModelSummaryAsCheckpoint()
    {
        const string summary = "User asked about alpha; assistant confirmed beta. Open question: gamma.";
        var model = new ScriptedLlmModel(new ModelAlias("summarizer"))
        {
            ExecuteOverride = (llmRequest, _) => Task.FromResult<ModelAttemptResult>(
                TestFactory.CompletedTextAttempt(llmRequest.Context.ModelRequestId, summary)),
        };
        var (compactor, coordinator) = CreateCompactorWithModelStrategy(model, maximumCheckpointCharacters: 200);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 10).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string((char) ('a' + (i % 26)), 200))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(10), minimumRetainedEntries: 2, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var succeeded = result.ShouldBeOfType<CompactionSucceeded>();
        succeeded.Record.Status.ShouldBe(CompactionRecordStatus.Active);
        ((TextPart) succeeded.Record.Checkpoint!.Summary.ShouldHaveSingleItem()).Text.ShouldBe(summary);
        succeeded.Record.Manifest.Producer.StrategyKey.ShouldBe(ModelCompactionStrategy.StrategyKey);
        succeeded.Record.Manifest.Producer.Deterministic.ShouldBeFalse();
        succeeded.Record.Manifest.Producer.Extensions.Values.ShouldContainKey(ModelCompactionProvenanceKeys.ModelId);
        _ = model.ReceivedRequests.ShouldHaveSingleItem();
        var committed = coordinator.ReceivedAppends.Single().Entries.Single().ShouldBeOfType<CompactionSessionEntry>();
        committed.Sequence.ShouldBe(new SessionSequence(11));

        // The producer provenance rides in ExtensionData; the first-party codec must round-trip it.
        var catalog = new Session.SessionEntryCodecCatalog([new Session.CompactionSessionEntryCodec()], TimeProvider.System);
        var encoded = catalog.Encode(committed).ShouldBeOfType<SessionEntryEncoded>();
        var decoded = catalog.Decode(encoded.Wire).ShouldBeOfType<SessionEntryDecoded>();
        decoded.Decoded.Entry.ShouldBeOfType<CompactionSessionEntry>().Record.Manifest.Producer.ShouldBe(committed.Record.Manifest.Producer);
    }

    [Fact]
    public async Task CompactAsync_WhenModelBackedStrategyFails_ReturnsCompactionFailedWithoutAppend()
    {
        var model = new ScriptedLlmModel(new ModelAlias("summarizer"), TestFactory.FailedAttempt(ProviderFailureKind.Unavailable));
        var (compactor, coordinator) = CreateCompactorWithModelStrategy(model, maximumCheckpointCharacters: 200);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 10).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('m', 200))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(10), minimumRetainedEntries: 2, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<CompactionFailed>();
        failed.Failure.Kind.ShouldBe(CompactionFailureKind.StrategyFailure);
        failed.Failure.Retryable.ShouldBeTrue();
        coordinator.ReceivedAppends.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompactAsync_WhenCallerCancelsDuringModelBackedSummary_ReturnsCancelledNotAttempted()
    {
        using var cancellation = new CancellationTokenSource();
        var model = new ScriptedLlmModel(new ModelAlias("summarizer"))
        {
            ExecuteOverride = async (_, token) =>
            {
                await cancellation.CancelAsync();
                token.ThrowIfCancellationRequested();
                throw new InvalidOperationException("unreachable");
            },
        };
        var (compactor, coordinator) = CreateCompactorWithModelStrategy(model, maximumCheckpointCharacters: 200);
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 10).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('c', 200))));
        var request = TestFactory.Request(TestFactory.CompactionContext(_agentId, _sessionId), _branchId, coordinator.Version, new SessionSequence(10), minimumRetainedEntries: 2, minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, cancellation.Token);

        result.ShouldBeOfType<CompactionCancelled>().CommitState.ShouldBe(CompactionCommitState.NotAttempted);
        coordinator.ReceivedAppends.ShouldBeEmpty();
    }

    private (DefaultCompactor Compactor, FakeSessionCoordinator Coordinator) CreateCompactorWithModelStrategy(ScriptedLlmModel model, int maximumCheckpointCharacters)
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        var descriptor = TestFactory.SummaryModel(model.Alias.Value);
        var compactor = BuildFromRegistration(
            coordinator,
            services =>
            {
                _ = services.AddModelBackedContextCompaction(options =>
                {
                    options.MaximumCheckpointCharacters = maximumCheckpointCharacters;
                    options.SummaryModelPolicy = new ModelSelectionPolicy([model.Alias]);
                });
                _ = services.AddSingleton<IModelCatalog>(new StaticModelCatalog(new ModelCatalogSnapshot(new ModelCatalogVersion(1), [descriptor])));
                _ = services.AddSingleton<IModelSelector>(ScriptedModelSelector.Selecting(descriptor));
                _ = services.AddSingleton<ILlmModelResolver>(new AliasLlmModelResolver(model));
            });
        return (compactor, coordinator);
    }

    /// <summary>Builds the default compactor through the public registration, with the fake coordinator and clock registered first so <c>TryAdd</c> keeps them.</summary>
    private DefaultCompactor BuildFromRegistration(
        FakeSessionCoordinator coordinator,
        Action<IServiceCollection> register,
        Action<IServiceCollection>? overrides = null,
        TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISessionCoordinator>(coordinator);
        _ = services.AddSingleton(timeProvider ?? Clock());
        register(services);
        overrides?.Invoke(services);
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return provider.GetRequiredService<ICompactor>().ShouldBeOfType<DefaultCompactor>();
    }

    private ServiceProvider BuildProvider(FakeSessionCoordinator coordinator)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISessionCoordinator>(coordinator);
        _ = services.AddContextCompaction();
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return provider;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }
    }

    private SessionAddress Address() => new(_agentId, _sessionId);
    /// <summary>Requests from <see cref="TestFactory.Request"/> are stamped at the Unix epoch with a five-minute deadline; the clock starts inside that window.</summary>
    private static FakeTimeProvider Clock() => new(DateTimeOffset.UnixEpoch.AddMinutes(1));

    [Fact]
    public async Task CompactAsync_WhenTheProfileEnablesActivation_JournalsTheAppendWithACompactionActivatedCheckpoint()
    {
        var profile = new DurabilityProfileKey("test-durability");
        var (compactor, coordinator, durable) = CreateDurableCompactor(profile, CompactionDurableOperations.Activation);
        var request = SeedDurableRequest(coordinator, profile);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var succeeded = result.ShouldBeOfType<CompactionSucceeded>();
        var activation = durable.Executions.ShouldHaveSingleItem();
        activation.Name.ShouldBe(CompactionDurableOperations.Activation);
        activation.Address.RunId.ShouldBe(((InRunOperationCorrelation) request.Context.Correlation).RunId);
        var manifest = DurableBoundaryPayload.Decode<DurableCompactionActivationManifest>(activation.Input);
        manifest.CompactionId.ShouldBe(request.Context.CompactionId.Value);
        manifest.ActivatedVersion.ShouldBe(succeeded.Record.ActivatedSessionVersion!.Value.Value);
        durable.Writers.ShouldHaveSingleItem().Checkpoints.ShouldBe([DurableCheckpointKind.CompactionActivated]);
        _ = coordinator.ReceivedAppends.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CompactAsync_WhenTheRequestSelectsNoProfile_AppendsWithoutJournaling()
    {
        var (compactor, coordinator, durable) = CreateDurableCompactor(
            new DurabilityProfileKey("test-durability"), CompactionDurableOperations.Activation);
        var request = SeedDurableRequest(coordinator, profile: null);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<CompactionSucceeded>();
        durable.Executions.ShouldBeEmpty();
        _ = coordinator.ReceivedAppends.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CompactAsync_WhenTheProfileDoesNotEnableActivation_AppendsWithoutJournaling()
    {
        var profile = new DurabilityProfileKey("test-durability");
        var (compactor, coordinator, durable) = CreateDurableCompactor(profile);
        var request = SeedDurableRequest(coordinator, profile);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<CompactionSucceeded>();
        durable.Executions.ShouldBeEmpty();
        _ = coordinator.ReceivedAppends.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CompactAsync_WhenTheSelectedProfileIsNotRegistered_AppendsWithoutJournaling()
    {
        var (compactor, coordinator, durable) = CreateDurableCompactor(
            new DurabilityProfileKey("another-profile"), CompactionDurableOperations.Activation);
        var request = SeedDurableRequest(coordinator, new DurabilityProfileKey("test-durability"));

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<CompactionSucceeded>();
        durable.Executions.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompactAsync_WhenTheActivationCannotBeJournaled_PropagatesTheFaultAndNeverAppendsTheRecord()
    {
        var profile = new DurabilityProfileKey("test-durability");
        var (compactor, coordinator, durable) = CreateDurableCompactor(profile, CompactionDurableOperations.Activation);
        durable.Failure = new InvalidOperationException("journal offline");
        var request = SeedDurableRequest(coordinator, profile);

        var failure = await Should.ThrowAsync<InvalidOperationException>(
            () => compactor.CompactAsync(request, TestContext.Current.CancellationToken));

        failure.Message.ShouldBe("journal offline");
        _ = durable.Executions.ShouldHaveSingleItem();
        coordinator.ReceivedAppends.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompactAsync_WhenTheActivationCheckpointIsRefused_NeverAppendsTheRecord()
    {
        var profile = new DurabilityProfileKey("test-durability");
        var (compactor, coordinator, durable) = CreateDurableCompactor(profile, CompactionDurableOperations.Activation);
        durable.WriteRefusal = new DurableRecordFenced(new FencingToken(1), new FencingToken(2));
        var request = SeedDurableRequest(coordinator, profile);

        var failure = await Should.ThrowAsync<InvalidOperationException>(
            () => compactor.CompactAsync(request, TestContext.Current.CancellationToken));

        failure.Message.ShouldContain("no longer owns");
        coordinator.ReceivedAppends.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompactAsync_WhenPolicyOrdersADecliningStrategyFirst_FallsBackToTheNextStrategyAndSucceeds()
    {
        var (compactor, coordinator) = CreateCompactorWithRegistrations(services =>
            services.AddCompactionStrategy<DecliningCompactionStrategy>(DefaultKey, StrategyRegistration("declining")));
        var request = SeedPolicyRequest(coordinator, "declining", CompactionStrategyKeys.Extractive.Value);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<CompactionSucceeded>().Record.Manifest.Producer.StrategyKey.ShouldBe(CompactionStrategyKeys.Extractive);
    }

    [Fact]
    public async Task CompactAsync_WhenEveryOrderedStrategyDeclines_ReturnsTheLastRejectionWithoutAppending()
    {
        var (compactor, coordinator) = CreateCompactorWithRegistrations(services =>
        {
            _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(DefaultKey, StrategyRegistration("first"));
            _ = services.AddCompactionStrategy<OtherDecliningCompactionStrategy>(DefaultKey, StrategyRegistration("second"));
        });
        var request = SeedPolicyRequest(coordinator, "first", "second");

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<CompactionRejected>();
        rejected.Rejection.Kind.ShouldBe(CompactionRejectionKind.PolicyViolation);
        coordinator.ReceivedAppends.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompactAsync_WhenAnOrderedStrategyFails_DoesNotFallBackToTheNextStrategy()
    {
        var failure = new CompactionFailure(CompactionFailureKind.StrategyFailure, "strategy blew up", retryable: false, ExtensionData.Empty);
        var failing = new FakeCompactionStrategy { OnProduce = _ => new CompactionStrategyFailed(failure) };
        var (compactor, coordinator) = CreateCompactorWithRegistrations(
            services => _ = services.AddKeyedSingleton<ICompactionStrategy>(
                CompactionServiceKeys.Strategy(DefaultKey, new CompactionStrategyKey("failing")), failing));
        var request = SeedPolicyRequest(coordinator, "failing", CompactionStrategyKeys.Extractive.Value);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<CompactionFailed>().Failure.SafeMessage.ShouldBe("strategy blew up");
        coordinator.ReceivedAppends.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompactAsync_WhenAnOrderedStrategyIsNotRegistered_ReturnsAStrategyFailure()
    {
        var (compactor, coordinator) = CreateCompactorWithRegistrations(static _ => { });
        var request = SeedPolicyRequest(coordinator, "never-registered");

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<CompactionFailed>();
        failed.Failure.Kind.ShouldBe(CompactionFailureKind.StrategyFailure);
        failed.Failure.SafeMessage.ShouldContain("never-registered");
    }

    [Fact]
    public async Task CompactAsync_WhenAnOrderedStrategyIsCancelled_ReturnsCancelledWithNothingAttempted()
    {
        var cancelled = new FakeCompactionStrategy
        {
            OnProduce = _ => new CompactionStrategyCancelled(new CompactionCancellation(
                CompactionCancellationReason.CallerCancelled, CompactionCommitState.NotAttempted, "cancelled by strategy")),
        };
        var (compactor, coordinator) = CreateCompactorWithRegistrations(
            services => _ = services.AddKeyedSingleton<ICompactionStrategy>(
                CompactionServiceKeys.Strategy(DefaultKey, new CompactionStrategyKey("cancelled")), cancelled));
        var request = SeedPolicyRequest(coordinator, "cancelled");

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<CompactionCancelled>().CommitState.ShouldBe(CompactionCommitState.NotAttempted);
        coordinator.ReceivedAppends.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompactAsync_WhenPolicyWasCompiledForAnotherCompactor_RejectsBeforeReadingTheSession()
    {
        var (compactor, coordinator) = CreateCompactorWithRegistrations(static _ => { });
        var request = SeedPolicyRequest(coordinator, CompactionStrategyKeys.Extractive.Value) with
        {
            Policy = CompactionPolicyFixtures.Create(
                compactorKey: new ComponentKey<ICompactor>("another-compactor"),
                strategyOrder: [CompactionStrategyKeys.Extractive]),
        };

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<CompactionRejected>().Rejection.Kind.ShouldBe(CompactionRejectionKind.PolicyViolation);
        coordinator.ReceivedReads.ShouldBeEmpty();
    }

    [Fact]
    public async Task CompactAsync_WhenTheRequestCarriesNoPolicy_AppliesTheCompactorsDefaultStrategyOrder()
    {
        var (compactor, coordinator) = CreateCompactorWithRegistrations(services =>
        {
            _ = services.AddAgentContextCompaction(
                DefaultKey,
                options => options.DefaultStrategyOrder = [new CompactionStrategyKey("declining"), CompactionStrategyKeys.Extractive]);
            _ = services.AddCompactionStrategy<DecliningCompactionStrategy>(DefaultKey, StrategyRegistration("declining"));
        });
        var request = SeedPolicyRequest(coordinator);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<CompactionSucceeded>().Record.Manifest.Producer.StrategyKey.ShouldBe(CompactionStrategyKeys.Extractive);
    }

    private static ComponentKey<ICompactor> DefaultKey => AgentContextCompactionComponentDefaults.CompactorKey;

    private static CompactionStrategyRegistration StrategyRegistration(string key) =>
        new(
            new CompactionStrategyDescriptor(
                new CompactionStrategyKey(key), new CompactionStrategyVersion("1"), CompactionStrategyCapabilities.None, deterministic: true, summaryGeneratorKey: null),
            order: 0,
            before: [],
            after: [],
            ServiceLifetime.Singleton);

    private (DefaultCompactor Compactor, FakeSessionCoordinator Coordinator) CreateCompactorWithRegistrations(Action<IServiceCollection> register)
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        var compactor = BuildFromRegistration(
            coordinator,
            services =>
            {
                _ = services.AddContextCompaction(o => o.MaximumCheckpointCharacters = 30);
                register(services);
            });
        return (compactor, coordinator);
    }

    /// <summary>Seeds a reducible branch and returns a request whose policy orders <paramref name="strategyOrder"/>, or no policy when none are named.</summary>
    private CompactionRequest SeedPolicyRequest(FakeSessionCoordinator coordinator, params string[] strategyOrder)
    {
        var request = SeedDurableRequest(coordinator, profile: null);
        return strategyOrder.Length == 0
            ? request
            : request with
            {
                Policy = CompactionPolicyFixtures.Create(
                    compactorKey: DefaultKey,
                    strategyOrder: [.. strategyOrder.Select(static key => new CompactionStrategyKey(key))]),
            };
    }

    private CompactionRequest SeedDurableRequest(FakeSessionCoordinator coordinator, DurabilityProfileKey? profile)
    {
        var address = Address();
        coordinator.Seed(Enumerable.Range(1, 10).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string((char) ('a' + (i % 26)), 200))));
        var request = TestFactory.Request(
            TestFactory.CompactionContext(_agentId, _sessionId),
            _branchId,
            coordinator.Version,
            new SessionSequence(10),
            minimumRetainedEntries: 2,
            minimumReductionRatio: 0.1);
        return profile is { } selected ? request with { DurabilityProfile = selected } : request;
    }

    private (DefaultCompactor Compactor, FakeSessionCoordinator Coordinator, RecordingBoundaryCoordinator Durable) CreateDurableCompactor(
        DurabilityProfileKey registeredProfile,
        params DurableOperationName[] enabledOperations)
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        var registry = new DurableBoundaryRegistry();
        var durable = new RecordingBoundaryCoordinator(registry);
        var compactor = BuildFromRegistration(
            coordinator,
            services => services.AddContextCompaction(o => o.MaximumCheckpointCharacters = 30),
            services =>
            {
                // The default keyed activation coordinator reads the durability coordinator, catalog, and registry from DI.
                _ = services.AddSingleton(registry);
                _ = services.AddSingleton<IDurableExecutionCoordinator>(durable);
                _ = services.AddSingleton<IDurabilityProfileCatalog>(new FixedDurabilityProfileCatalog(registeredProfile, enabledOperations));
            });
        return (compactor, coordinator, durable);
    }

    private (DefaultCompactor Compactor, FakeSessionCoordinator Coordinator) CreateCompactor(int maximumCheckpointCharacters = 16_000, int maximumSourceEntries = 5_000, int sourceReadPageSize = 256, ILogger<DefaultCompactor>? logger = null, TimeProvider? timeProvider = null)
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        var compactor = BuildFromRegistration(
            coordinator,
            services => services.AddContextCompaction(o =>
            {
                o.MaximumCheckpointCharacters = maximumCheckpointCharacters;
                o.MaximumSourceEntries = maximumSourceEntries;
                o.SourceReadPageSize = sourceReadPageSize;
            }),
            logger is null ? null : services => services.AddSingleton(logger),
            timeProvider);
        return (compactor, coordinator);
    }

    private (DefaultCompactor Compactor, FakeSessionCoordinator Coordinator) CreateCompactorWithFakes(ICompactionCutSelector? cutSelector = null, ICompactionStrategy? strategy = null, ICompactionValidator? validator = null, ILogger<DefaultCompactor>? logger = null)
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        var compactor = BuildFromRegistration(
            coordinator,
            services => services.AddContextCompaction(),
            services =>
            {
                // A fake takes the exact slot the first-party default would occupy, so the compactor reaches it through
                // its keyed resolver, selector, and validator rather than a test-only constructor.
                if (cutSelector is not null)
                {
                    _ = services.AddSingleton(cutSelector);
                }

                if (strategy is not null)
                {
                    _ = services.AddKeyedSingleton(
                        CompactionServiceKeys.Strategy(AgentContextCompactionComponentDefaults.CompactorKey, CompactionStrategyKeys.Extractive),
                        strategy);
                }

                if (validator is not null)
                {
                    _ = services.AddSingleton(validator);
                }

                if (logger is not null)
                {
                    _ = services.AddSingleton(logger);
                }
            });
        return (compactor, coordinator);
    }

    private static CharacterCompactionSizeEstimator CreateEstimator() => new(Options.Create(new CompactionOptions()));
    [Fact]
    public async Task CompactAsync_WhenObserved_EmitsCorrelatedContentFreeActivity()
    {
        const string protectedContent = "never-export-compaction-source";
        var branchId = new BranchId(Guid.NewGuid());
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var coordinator = new FakeSessionCoordinator(branchId);
        coordinator.Seed([TestFactory.MessageEntry(new SessionAddress(agentId, sessionId), branchId, 1, protectedContent)]);
        var compactor = BuildFromRegistration(coordinator, services => services.AddContextCompaction());
        var request = TestFactory.Request(TestFactory.CompactionContext(agentId, sessionId), branchId, coordinator.Version, new SessionSequence(1), minimumRetainedEntries: 5);
        using var activities = new ActivityCollector(static source => source.Name == AgentKitDiagnostics.ActivitySourceName, activity => activity.OperationName == AgentKitActivityNames.ContextCompact && Equals(activity.GetTagItem(AgentKitTagNames.CompactionId), request.Context.CompactionId.ToString()));
        _ = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);
        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.CompactionId).ShouldBe(request.Context.CompactionId.ToString());
        activity.Tags.Values.Select(static value => value?.ToString()).ShouldNotContain(protectedContent);
    }
}
