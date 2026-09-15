// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Options;

/// <summary>Verifies DefaultCompactor behavior and contracts.</summary>
public sealed class DefaultCompactorTests
{
    private readonly AgentId _agentId = new(Guid.NewGuid());
    private readonly SessionId _sessionId = new(Guid.NewGuid());
    private readonly BranchId _branchId = new(Guid.NewGuid());
    [Fact]
    public void Constructor_WhenCoordinatorNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultCompactor(null!, new StructuralCompactionCutSelector(Options.Create(new CompactionOptions())), CreateStrategy(), CreateValidator(), CreateEstimator(), IdGenerator(static v => new CompactionManifestId(v)), IdGenerator(static v => new SessionEntryId(v)), TimeProvider.System, Options.Create(new CompactionOptions())));
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
    public async Task CompactAsync_WhenBranchVersionAdvancedSinceComputed_ReturnsCompactionConflict()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        var entries = Enumerable.Range(1, 5).Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('h', 200))).ToArray();
        coordinator.Seed(entries);
        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        // Compute the request against a stale version — one behind the branch's actual, seeded version.
        var staleRequest = TestFactory.Request(context, _branchId, new SessionVersion(coordinator.Version.Value - 1), new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);
        var result = await compactor.CompactAsync(staleRequest, TestContext.Current.CancellationToken);
        var conflict = result.ShouldBeOfType<CompactionConflict>();
        conflict.ExpectedVersion.ShouldBe(staleRequest.SourceVersion);
        conflict.ActualVersion.ShouldBe(coordinator.Version);
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
    public async Task CompactAsync_WhenAppendFails_ReturnsCompactionFailed()
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
        failed.Failure.Retryable.ShouldBeTrue();
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

    private SessionAddress Address() => new(_agentId, _sessionId);
    private (DefaultCompactor Compactor, FakeSessionCoordinator Coordinator) CreateCompactor(int maximumCheckpointCharacters = 16_000, int maximumSourceEntries = 5_000, int sourceReadPageSize = 256)
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        var options = Options.Create(new CompactionOptions { MaximumCheckpointCharacters = maximumCheckpointCharacters, MaximumSourceEntries = maximumSourceEntries, SourceReadPageSize = sourceReadPageSize });
        var estimator = new CharacterCompactionSizeEstimator(options);
        var compactor = new DefaultCompactor(coordinator, new StructuralCompactionCutSelector(options), new ExtractiveCompactionStrategy(estimator, options), new DefaultCompactionValidator(estimator, options), estimator, IdGenerator(static v => new CompactionManifestId(v)), IdGenerator(static v => new SessionEntryId(v)), TimeProvider.System, options);
        return (compactor, coordinator);
    }

    private (DefaultCompactor Compactor, FakeSessionCoordinator Coordinator) CreateCompactorWithFakes(ICompactionCutSelector? cutSelector = null, ICompactionStrategy? strategy = null, ICompactionValidator? validator = null)
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        var options = Options.Create(new CompactionOptions());
        var estimator = new CharacterCompactionSizeEstimator(options);
        var compactor = new DefaultCompactor(coordinator, cutSelector ?? new StructuralCompactionCutSelector(options), strategy ?? new ExtractiveCompactionStrategy(estimator, options), validator ?? new DefaultCompactionValidator(estimator, options), estimator, IdGenerator(static v => new CompactionManifestId(v)), IdGenerator(static v => new SessionEntryId(v)), TimeProvider.System, options);
        return (compactor, coordinator);
    }

    private static CharacterCompactionSizeEstimator CreateEstimator() => new(Options.Create(new CompactionOptions()));
    private static ExtractiveCompactionStrategy CreateStrategy() => new(CreateEstimator(), Options.Create(new CompactionOptions()));
    private static DefaultCompactionValidator CreateValidator() => new(CreateEstimator(), Options.Create(new CompactionOptions()));
    private static GuidIdentifierGenerator<TIdentifier> IdGenerator<TIdentifier>(Func<Guid, TIdentifier> factory)
        where TIdentifier : struct => new(factory);
    [Fact]
    public async Task CompactAsync_WhenObserved_EmitsCorrelatedContentFreeActivity()
    {
        const string protectedContent = "never-export-compaction-source";
        var branchId = new BranchId(Guid.NewGuid());
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var coordinator = new FakeSessionCoordinator(branchId);
        coordinator.Seed([TestFactory.MessageEntry(new SessionAddress(agentId, sessionId), branchId, 1, protectedContent)]);
        var options = Options.Create(new CompactionOptions());
        var estimator = new CharacterCompactionSizeEstimator(options);
        var compactor = new DefaultCompactor(coordinator, new StructuralCompactionCutSelector(options), new ExtractiveCompactionStrategy(estimator, options), new DefaultCompactionValidator(estimator, options), estimator, new GuidIdentifierGenerator<CompactionManifestId>(static value => new CompactionManifestId(value)), new GuidIdentifierGenerator<SessionEntryId>(static value => new SessionEntryId(value)), TimeProvider.System, options);
        var request = TestFactory.Request(TestFactory.CompactionContext(agentId, sessionId), branchId, coordinator.Version, new SessionSequence(1), minimumRetainedEntries: 5);
        using var activities = new ActivityCollector(static source => source.Name == AgentKitDiagnostics.ActivitySourceName, activity => activity.OperationName == AgentKitActivityNames.ContextCompact && Equals(activity.GetTagItem(AgentKitTagNames.CompactionId), request.Context.CompactionId.ToString()));
        _ = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);
        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem(AgentKitTagNames.CompactionId).ShouldBe(request.Context.CompactionId.ToString());
        activity.Tags.Values.Select(static value => value?.ToString()).ShouldNotContain(protectedContent);
    }
}
