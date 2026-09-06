// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

using Microsoft.Extensions.Options;

public sealed class DefaultCompactorTests
{
    private readonly AgentId _agentId = new(Guid.NewGuid());
    private readonly SessionId _sessionId = new(Guid.NewGuid());
    private readonly BranchId _branchId = new(Guid.NewGuid());

    [Fact]
    public void Constructor_WhenCoordinatorNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultCompactor(
            null!,
            new StructuralCompactionCutSelector(Options.Create(new CompactionOptions())),
            CreateStrategy(),
            CreateValidator(),
            CreateEstimator(),
            IdGenerator(static v => new CompactionManifestId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            Options.Create(new CompactionOptions())));

        exception.ParamName.ShouldBe("coordinator");
    }

    [Fact]
    public async Task CompactAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var (compactor, _) = CreateCompactor();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => compactor.CompactAsync(null!, TestContext.Current.CancellationToken));

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
        var request = TestFactory.Request(
            context, _branchId, coordinator.Version, new SessionSequence(2), minimumRetainedEntries: 5);

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
        var entries = Enumerable.Range(1, 10)
            .Select(i => TestFactory.MessageEntry(address, _branchId, i, new string((char) ('a' + (i % 26)), 200)))
            .ToArray();
        coordinator.Seed(entries);

        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(
            context,
            _branchId,
            coordinator.Version,
            new SessionSequence(10),
            minimumRetainedEntries: 2,
            minimumReductionRatio: 0.1);

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
    public async Task CompactAsync_WhenBranchVersionAdvancedSinceComputed_ReturnsCompactionConflict()
    {
        var (compactor, coordinator) = CreateCompactor(maximumCheckpointCharacters: 30);
        var address = Address();
        var entries = Enumerable.Range(1, 5)
            .Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('h', 200)))
            .ToArray();
        coordinator.Seed(entries);

        var context = TestFactory.CompactionContext(_agentId, _sessionId);

        // Compute the request against a stale version — one behind the branch's actual, seeded version.
        var staleRequest = TestFactory.Request(
            context,
            _branchId,
            new SessionVersion(coordinator.Version.Value - 1),
            new SessionSequence(5),
            minimumRetainedEntries: 1,
            minimumReductionRatio: 0.1);

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
        var entries = new[] { TestFactory.MessageEntry(address, _branchId, 1, "short") };
        coordinator.Seed(entries);

        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(
            context,
            _branchId,
            coordinator.Version,
            new SessionSequence(1),
            minimumRetainedEntries: 0,
            minimumReductionRatio: 0.9);

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
        var entries = Enumerable.Range(1, 5)
            .Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('z', 200)))
            .ToArray();
        coordinator.Seed(entries);
        coordinator.AppendOverride = static request => new SessionAppendFailed("store unavailable");

        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(
            context, _branchId, coordinator.Version, new SessionSequence(5), minimumRetainedEntries: 1, minimumReductionRatio: 0.1);

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
        var tail = Enumerable.Range(3, 5)
            .Select(i => TestFactory.MessageEntry(address, _branchId, i, new string('w', 200)))
            .ToArray();
        var entries = new SessionEntry[] { call, toolResult }.Concat(tail).ToArray();
        coordinator.Seed(entries);

        var context = TestFactory.CompactionContext(_agentId, _sessionId);
        var request = TestFactory.Request(
            context,
            _branchId,
            coordinator.Version,
            new SessionSequence(entries.Length),
            minimumRetainedEntries: 1,
            minimumReductionRatio: 0.1);

        var result = await compactor.CompactAsync(request, TestContext.Current.CancellationToken);

        var succeeded = result.ShouldBeOfType<CompactionSucceeded>();
        succeeded.Record.Manifest.CoveredRange.EndInclusive.Value.ShouldBeGreaterThanOrEqualTo(toolResult.Sequence.Value);
    }

    private SessionAddress Address() => new(_agentId, _sessionId);

    private (DefaultCompactor Compactor, FakeSessionCoordinator Coordinator) CreateCompactor(
        int maximumCheckpointCharacters = 16_000, int maximumSourceEntries = 5_000, int sourceReadPageSize = 256)
    {
        var coordinator = new FakeSessionCoordinator(_branchId);
        var options = Options.Create(new CompactionOptions
        {
            MaximumCheckpointCharacters = maximumCheckpointCharacters,
            MaximumSourceEntries = maximumSourceEntries,
            SourceReadPageSize = sourceReadPageSize
        });

        var estimator = new CharacterCompactionSizeEstimator(options);
        var compactor = new DefaultCompactor(
            coordinator,
            new StructuralCompactionCutSelector(options),
            new ExtractiveCompactionStrategy(estimator, options),
            new DefaultCompactionValidator(estimator, options),
            estimator,
            IdGenerator(static v => new CompactionManifestId(v)),
            IdGenerator(static v => new SessionEntryId(v)),
            TimeProvider.System,
            options);

        return (compactor, coordinator);
    }

    private static CharacterCompactionSizeEstimator CreateEstimator() =>
        new(Options.Create(new CompactionOptions()));

    private static ExtractiveCompactionStrategy CreateStrategy() =>
        new(CreateEstimator(), Options.Create(new CompactionOptions()));

    private static DefaultCompactionValidator CreateValidator() =>
        new(CreateEstimator(), Options.Create(new CompactionOptions()));

    private static GuidIdentifierGenerator<TIdentifier> IdGenerator<TIdentifier>(Func<Guid, TIdentifier> factory)
        where TIdentifier : struct =>
        new(factory);
}
