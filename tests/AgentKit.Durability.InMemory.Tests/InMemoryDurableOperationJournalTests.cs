// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory.Tests;

public sealed class InMemoryDurableOperationJournalTests
{
    private static readonly FencingToken TokenOne = new(1);
    private static readonly FencingToken TokenTwo = new(2);

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_RejectsExactArgument() =>
        Should.Throw<ArgumentNullException>(() => new InMemoryDurableOperationJournal(null!)).ParamName.ShouldBe("timeProvider");

    [Fact]
    public async Task RecordStartAsync_WhenStartIsNull_RejectsExactArgument()
    {
        var journal = Journal();

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            _ = await journal.RecordStartAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("start");
    }

    [Fact]
    public async Task RecordStartAsync_WhenNoPriorRecordExists_CommitsAcceptance()
    {
        var journal = Journal();

        var result = await journal.RecordStartAsync(
            DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);

        var recorded = result.ShouldBeOfType<DurableRecorded>();
        recorded.FencingToken.ShouldBe(TokenOne);
    }

    [Fact]
    public async Task RecordStartAsync_ThenLoadEvidence_ReportsAcceptedAndStartDefinitelyAbsent()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);

        var evidence = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);

        var loaded = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence;
        loaded.State.ShouldBe(DurableOperationState.Accepted);
        loaded.StartDefinitelyAbsent.ShouldBeTrue();
        loaded.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        loaded.TerminalResultRecorded.ShouldBeFalse();
    }

    [Fact]
    public async Task RecordStartAsync_WhenRepeatedUnderANewerTokenWhileStillAccepted_UpdatesOwnership()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);

        var result = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenTwo), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecorded>().FencingToken.ShouldBe(TokenTwo);
    }

    [Fact]
    public async Task RecordStartAsync_WhenPresentedTokenIsOlderThanCurrent_ReturnsFenced()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenTwo), TestContext.Current.CancellationToken);

        var result = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);

        var fenced = result.ShouldBeOfType<DurableRecordFenced>();
        fenced.PresentedToken.ShouldBe(TokenOne);
        fenced.CurrentToken.ShouldBe(TokenTwo);
    }

    [Fact]
    public async Task RecordStartAsync_WhenExecutionContextDiffersFromTheAcceptedRecord_ReturnsFailedWithoutMutating()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);
        var foreignContext = DurableJournalTestData.Context(DurableJournalTestData.ForeignAuthorization());

        var result = await journal.RecordStartAsync(
            DurableJournalTestData.Start(TokenTwo, context: foreignContext), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<DurableRecordFailed>();
        failed.Committed.ShouldBe(false);
        var evidence = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);
        evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.LastWriterToken.ShouldBe(TokenOne);
    }

    [Fact]
    public async Task RecordStartAsync_WhenTheOperationAlreadyProgressedPastAcceptance_ReturnsFailed()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);
        _ = await journal.RecordCheckpointAsync(DurableJournalTestData.Checkpoint(TokenOne), TestContext.Current.CancellationToken);

        var result = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenTwo), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(true);
    }

    [Fact]
    public async Task RecordStartAsync_WhenCancelledBeforeCommit_CommitsNoRecord()
    {
        var journal = Journal();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), cancellation.Token));

        var evidence = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);
        _ = evidence.ShouldBeOfType<RecoveryEvidenceNotFound>();
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenCheckpointIsNull_RejectsExactArgument()
    {
        var journal = Journal();

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            _ = await journal.RecordCheckpointAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("checkpoint");
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenNoAcceptedRecordExists_ReturnsFailed()
    {
        var journal = Journal();

        var result = await journal.RecordCheckpointAsync(
            DurableJournalTestData.Checkpoint(TokenOne), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenAccepted_MovesToEffectPendingWithUnknownCertainty()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);

        var result = await journal.RecordCheckpointAsync(
            DurableJournalTestData.Checkpoint(TokenOne), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
        var evidence = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);
        var loaded = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence;
        loaded.State.ShouldBe(DurableOperationState.EffectPending);
        loaded.StartDefinitelyAbsent.ShouldBeFalse();
        loaded.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
        _ = loaded.LatestCheckpoint.ShouldNotBeNull();
        loaded.LatestCheckpoint!.Id.ShouldBe(DurableJournalTestData.CheckpointId);
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenPresentedTokenIsOlderThanCurrent_ReturnsFenced()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenTwo), TestContext.Current.CancellationToken);

        var result = await journal.RecordCheckpointAsync(
            DurableJournalTestData.Checkpoint(TokenOne), TestContext.Current.CancellationToken);

        var fenced = result.ShouldBeOfType<DurableRecordFenced>();
        fenced.PresentedToken.ShouldBe(TokenOne);
        fenced.CurrentToken.ShouldBe(TokenTwo);
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenExecutionContextDiffers_ReturnsFailed()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);
        var foreignContext = DurableJournalTestData.Context(DurableJournalTestData.ForeignAuthorization());

        var result = await journal.RecordCheckpointAsync(
            DurableJournalTestData.Checkpoint(TokenOne, context: foreignContext), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenATerminalRecordAlreadyExists_ReturnsFailedWithoutMutating()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);
        _ = await journal.RecordTerminalAsync(DurableJournalTestData.Result(TokenOne), TestContext.Current.CancellationToken);

        var result = await journal.RecordCheckpointAsync(
            DurableJournalTestData.Checkpoint(TokenOne), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(true);
        var evidence = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);
        evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.LatestCheckpoint.ShouldBeNull();
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenCancelledBeforeCommit_DoesNotChangeState()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            _ = await journal.RecordCheckpointAsync(DurableJournalTestData.Checkpoint(TokenOne), cancellation.Token));

        var evidence = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);
        evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.State.ShouldBe(DurableOperationState.Accepted);
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenResultIsNull_RejectsExactArgument()
    {
        var journal = Journal();

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            _ = await journal.RecordTerminalAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("result");
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenNoAcceptedRecordExists_ReturnsFailed()
    {
        var journal = Journal();

        var result = await journal.RecordTerminalAsync(
            DurableJournalTestData.Result(TokenOne), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenAccepted_CommitsTheTerminalRecordAndItsCertainty()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);

        var result = await journal.RecordTerminalAsync(
            DurableJournalTestData.Result(TokenOne, certainty: SideEffectCertainty.PartiallyPerformed),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
        var evidence = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);
        var loaded = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence;
        loaded.State.ShouldBe(DurableOperationState.Completed);
        loaded.SideEffectCertainty.ShouldBe(SideEffectCertainty.PartiallyPerformed);
        loaded.TerminalResultRecorded.ShouldBeTrue();
        loaded.StartDefinitelyAbsent.ShouldBeFalse();
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenRepeatedWithAnEquivalentResult_IsIdempotent()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);
        _ = await journal.RecordTerminalAsync(DurableJournalTestData.Result(TokenOne), TestContext.Current.CancellationToken);

        var result = await journal.RecordTerminalAsync(
            DurableJournalTestData.Result(TokenOne), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenRepeatedWithADifferentResult_ReturnsFailed()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);
        _ = await journal.RecordTerminalAsync(DurableJournalTestData.Result(TokenOne, marker: 3), TestContext.Current.CancellationToken);

        var result = await journal.RecordTerminalAsync(
            DurableJournalTestData.Result(TokenOne, marker: 9), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(true);
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenPresentedTokenIsOlderThanCurrent_ReturnsFenced()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenTwo), TestContext.Current.CancellationToken);

        var result = await journal.RecordTerminalAsync(
            DurableJournalTestData.Result(TokenOne), TestContext.Current.CancellationToken);

        var fenced = result.ShouldBeOfType<DurableRecordFenced>();
        fenced.PresentedToken.ShouldBe(TokenOne);
        fenced.CurrentToken.ShouldBe(TokenTwo);
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenExecutionContextDiffers_ReturnsFailed()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);
        var foreignContext = DurableJournalTestData.Context(DurableJournalTestData.ForeignAuthorization());

        var result = await journal.RecordTerminalAsync(
            DurableJournalTestData.Result(TokenOne, context: foreignContext), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenCancelledBeforeCommit_DoesNotRecordATerminalResult()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            _ = await journal.RecordTerminalAsync(DurableJournalTestData.Result(TokenOne), cancellation.Token));

        var evidence = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);
        evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.TerminalResultRecorded.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadEvidenceAsync_WhenAddressIsNull_RejectsExactArgument()
    {
        var journal = Journal();

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            _ = await journal.LoadEvidenceAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("address");
    }

    [Fact]
    public async Task LoadEvidenceAsync_WhenNoRecordExists_ReturnsNotFoundWithTheRequestedAddress()
    {
        var journal = Journal();

        var result = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RecoveryEvidenceNotFound>().Address.ShouldBe(DurableJournalTestData.Address());
    }

    [Fact]
    public async Task LoadEvidenceAsync_WhenCancelledBeforeRead_Throws()
    {
        var journal = Journal();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            _ = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), cancellation.Token));
    }

    [Fact]
    public async Task LoadEvidenceAsync_NeverReportsAnExternalReferenceOrWaitingState()
    {
        var journal = Journal();
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);
        _ = await journal.RecordCheckpointAsync(DurableJournalTestData.Checkpoint(TokenOne), TestContext.Current.CancellationToken);

        var evidence = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);

        var loaded = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence;
        loaded.ExternalReference.ShouldBeNull();
        loaded.ExternalIdempotencyKey.ShouldBeNull();
        loaded.State.ShouldNotBe(DurableOperationState.Waiting);
    }

    [Fact]
    public async Task Addresses_WithDifferentOperationIdentities_AreTrackedIndependently()
    {
        var journal = Journal();
        var otherOperationId = new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000002"));
        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);

        var otherResult = await journal.RecordStartAsync(
            DurableJournalTestData.Start(TokenOne, operationId: otherOperationId), TestContext.Current.CancellationToken);

        _ = otherResult.ShouldBeOfType<DurableRecorded>();
        var firstEvidence = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);
        var otherEvidence = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(otherOperationId), TestContext.Current.CancellationToken);
        _ = firstEvidence.ShouldBeOfType<RecoveryEvidenceLoaded>();
        _ = otherEvidence.ShouldBeOfType<RecoveryEvidenceLoaded>();
    }

    [Fact]
    public async Task FullLifecycle_StartCheckpointTerminal_ReflectsEachTransitionInEvidence()
    {
        var journal = Journal();

        _ = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);
        var afterStart = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);
        afterStart.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.State.ShouldBe(DurableOperationState.Accepted);

        _ = await journal.RecordCheckpointAsync(DurableJournalTestData.Checkpoint(TokenOne), TestContext.Current.CancellationToken);
        var afterCheckpoint = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);
        afterCheckpoint.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.State.ShouldBe(DurableOperationState.EffectPending);

        _ = await journal.RecordTerminalAsync(DurableJournalTestData.Result(TokenOne), TestContext.Current.CancellationToken);
        var afterTerminal = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);
        var final = afterTerminal.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence;
        final.State.ShouldBe(DurableOperationState.Completed);
        final.TerminalResultRecorded.ShouldBeTrue();
        _ = final.LatestCheckpoint.ShouldNotBeNull();
    }

    [Fact]
    public async Task WriteMethods_WhenTheClockAndLoggerFail_StillReturnTheirCommittedOutcome()
    {
        var journal = new InMemoryDurableOperationJournal(new ThrowingTimeProvider(), new ThrowingLogger<InMemoryDurableOperationJournal>());

        var started = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);
        var checkpointed = await journal.RecordCheckpointAsync(DurableJournalTestData.Checkpoint(TokenOne), TestContext.Current.CancellationToken);
        var completed = await journal.RecordTerminalAsync(DurableJournalTestData.Result(TokenOne), TestContext.Current.CancellationToken);
        var evidence = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);

        _ = started.ShouldBeOfType<DurableRecorded>();
        _ = checkpointed.ShouldBeOfType<DurableRecorded>();
        _ = completed.ShouldBeOfType<DurableRecorded>();
        _ = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>();
    }

    [Fact]
    public async Task WriteMethods_WhenElapsedTimeMeasurementFails_StillReturnTheirCommittedOutcome()
    {
        // GetTimestamp succeeds (unlike ThrowingTimeProvider above), so TryGetElapsedTime's own
        // independent failure boundary around GetElapsedTime is exercised instead of short-circuiting
        // before ever calling it.
        var journal = new InMemoryDurableOperationJournal(new ThrowingElapsedTimeProvider(), new ThrowingLogger<InMemoryDurableOperationJournal>());

        var started = await journal.RecordStartAsync(DurableJournalTestData.Start(TokenOne), TestContext.Current.CancellationToken);
        var checkpointed = await journal.RecordCheckpointAsync(DurableJournalTestData.Checkpoint(TokenOne), TestContext.Current.CancellationToken);
        var completed = await journal.RecordTerminalAsync(DurableJournalTestData.Result(TokenOne), TestContext.Current.CancellationToken);
        var evidence = await journal.LoadEvidenceAsync(DurableJournalTestData.Address(), TestContext.Current.CancellationToken);

        _ = started.ShouldBeOfType<DurableRecorded>();
        _ = checkpointed.ShouldBeOfType<DurableRecorded>();
        _ = completed.ShouldBeOfType<DurableRecorded>();
        _ = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>();
    }

    private static InMemoryDurableOperationJournal Journal() => new(TimeProvider.System);

    private sealed class ThrowingTimeProvider: TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DurableJournalTestData.Now;

        public override long GetTimestamp() => throw new InvalidTimeZoneException("clock failure");
    }

    private sealed class ThrowingElapsedTimeProvider: TimeProvider
    {
        private int _calls;

        public override DateTimeOffset GetUtcNow() => DurableJournalTestData.Now;

        // The first call captures the starting timestamp (TryGetTimestamp, unprotected against this
        // failure mode); the second call is GetElapsedTime's own internal "now" fetch, which fails
        // independently and exercises TryGetElapsedTime's own catch boundary.
        public override long GetTimestamp() => ++_calls == 1 ? 1 : throw new InvalidTimeZoneException("elapsed-time failure");
    }

    private sealed class ThrowingLogger<TCategory>: ILogger<TCategory>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new InvalidTimeZoneException("logger failure");
    }
}
