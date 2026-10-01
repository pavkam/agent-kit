// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory.Tests;

public sealed class InMemoryDurableOperationJournalTests
{
    private static readonly FencingToken TokenOne = new(1);
    private static readonly FencingToken TokenTwo = new(2);

    [Fact]
    public void Constructor_WhenTheJournalKeyIsDefault_RejectsExactArgument() =>
        Should.Throw<ArgumentNullException>(() => new InMemoryDurableOperationJournal(
            default,
            new TestAuditRecordIdGenerator(),
            new TestDurableSecurityHarness(),
            new TestDurableSecurityHarness(),
            TimeProvider.System)).ParamName.ShouldBe("key");

    [Fact]
    public void Constructor_WhenAuditRecordIdsIsNull_RejectsExactArgument() =>
        Should.Throw<ArgumentNullException>(() => new InMemoryDurableOperationJournal(
            DurableJournalTestData.JournalKey,
            null!,
            new TestDurableSecurityHarness(),
            new TestDurableSecurityHarness(),
            TimeProvider.System)).ParamName.ShouldBe("auditRecordIds");

    [Fact]
    public void Constructor_WhenAuditDispatcherIsNull_RejectsExactArgument() =>
        Should.Throw<ArgumentNullException>(() => new InMemoryDurableOperationJournal(
            DurableJournalTestData.JournalKey,
            new TestAuditRecordIdGenerator(),
            null!,
            new TestDurableSecurityHarness(),
            TimeProvider.System)).ParamName.ShouldBe("auditDispatcher");

    [Fact]
    public void Constructor_WhenGrantStoreIsNull_RejectsExactArgument() =>
        Should.Throw<ArgumentNullException>(() => new InMemoryDurableOperationJournal(
            DurableJournalTestData.JournalKey,
            new TestAuditRecordIdGenerator(),
            new TestDurableSecurityHarness(),
            null!,
            TimeProvider.System)).ParamName.ShouldBe("grants");

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_RejectsExactArgument() =>
        Should.Throw<ArgumentNullException>(() => new InMemoryDurableOperationJournal(
            DurableJournalTestData.JournalKey,
            new TestAuditRecordIdGenerator(),
            new TestDurableSecurityHarness(),
            new TestDurableSecurityHarness(),
            null!)).ParamName.ShouldBe("timeProvider");

    [Fact]
    public void Key_WhenConstructed_ExposesTheSelectedRegistrationKey() =>
        new DurableJournalFixture().Journal.Key.ShouldBe(DurableJournalTestData.JournalKey);

    [Fact]
    public async Task RecordStartAsync_WhenStartIsNull_RejectsExactArgument()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            _ = await journal.RecordStartAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("start");
    }

    [Fact]
    public async Task RecordStartAsync_WhenNoPriorRecordExists_CommitsAcceptance()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;

        var result = await journal.RecordStartAsync(
            fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        var recorded = result.ShouldBeOfType<DurableRecorded>();
        recorded.FencingToken.ShouldBe(TokenOne);
    }

    [Fact]
    public async Task RecordStartAsync_ThenLoadEvidence_ReportsAcceptedAndStartDefinitelyAbsent()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);

        var loaded = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence;
        loaded.State.ShouldBe(DurableOperationState.Accepted);
        loaded.StartDefinitelyAbsent.ShouldBeTrue();
        loaded.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        loaded.TerminalResultRecorded.ShouldBeFalse();
    }

    [Fact]
    public async Task RecordStartAsync_WhenRepeatedUnderANewerTokenWhileStillAccepted_UpdatesOwnership()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        var result = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenTwo), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecorded>().FencingToken.ShouldBe(TokenTwo);
    }

    [Fact]
    public async Task RecordStartAsync_WhenPresentedTokenIsOlderThanCurrent_ReturnsFenced()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenTwo), TestContext.Current.CancellationToken);

        var result = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        var fenced = result.ShouldBeOfType<DurableRecordFenced>();
        fenced.PresentedToken.ShouldBe(TokenOne);
        fenced.CurrentToken.ShouldBe(TokenTwo);
    }

    [Fact]
    public async Task RecordStartAsync_WhenExecutionContextDiffersFromTheAcceptedRecord_ReturnsFailedWithoutMutating()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        var foreignContext = DurableJournalTestData.Context(DurableJournalTestData.ForeignAuthorization());

        var result = await journal.RecordStartAsync(
            fixture.AuthorizedStart(TokenTwo, context: foreignContext), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<DurableRecordFailed>();
        failed.Committed.ShouldBe(false);
        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.LastWriterToken.ShouldBe(TokenOne);
    }

    [Fact]
    public async Task RecordStartAsync_WhenTheOperationAlreadyProgressedPastAcceptance_ReturnsFailed()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        _ = await journal.RecordCheckpointAsync(fixture.AuthorizedCheckpoint(TokenOne), TestContext.Current.CancellationToken);

        var result = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenTwo), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(true);
    }

    [Fact]
    public async Task RecordStartAsync_WhenCancelledBeforeCommit_CommitsNoRecord()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), cancellation.Token));

        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        _ = evidence.ShouldBeOfType<RecoveryEvidenceNotFound>();
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenCheckpointIsNull_RejectsExactArgument()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            _ = await journal.RecordCheckpointAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("checkpoint");
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenNoAcceptedRecordExists_ReturnsFailed()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;

        var result = await journal.RecordCheckpointAsync(
            fixture.AuthorizedCheckpoint(TokenOne), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenAccepted_MovesToEffectPendingWithUnknownCertainty()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        var result = await journal.RecordCheckpointAsync(
            fixture.AuthorizedCheckpoint(TokenOne), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
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
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenTwo), TestContext.Current.CancellationToken);

        var result = await journal.RecordCheckpointAsync(
            fixture.AuthorizedCheckpoint(TokenOne), TestContext.Current.CancellationToken);

        var fenced = result.ShouldBeOfType<DurableRecordFenced>();
        fenced.PresentedToken.ShouldBe(TokenOne);
        fenced.CurrentToken.ShouldBe(TokenTwo);
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenExecutionContextDiffers_ReturnsFailed()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        var foreignContext = DurableJournalTestData.Context(DurableJournalTestData.ForeignAuthorization());

        var result = await journal.RecordCheckpointAsync(
            fixture.AuthorizedCheckpoint(TokenOne, context: foreignContext), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenATerminalRecordAlreadyExists_ReturnsFailedWithoutMutating()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        _ = await journal.RecordTerminalAsync(fixture.AuthorizedResult(TokenOne), TestContext.Current.CancellationToken);

        var result = await journal.RecordCheckpointAsync(
            fixture.AuthorizedCheckpoint(TokenOne), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(true);
        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.LatestCheckpoint.ShouldBeNull();
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenCancelledBeforeCommit_DoesNotChangeState()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            _ = await journal.RecordCheckpointAsync(fixture.AuthorizedCheckpoint(TokenOne), cancellation.Token));

        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.State.ShouldBe(DurableOperationState.Accepted);
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenResultIsNull_RejectsExactArgument()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            _ = await journal.RecordTerminalAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("result");
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenNoAcceptedRecordExists_ReturnsFailed()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;

        var result = await journal.RecordTerminalAsync(
            fixture.AuthorizedResult(TokenOne), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenAccepted_CommitsTheTerminalRecordAndItsCertainty()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        var result = await journal.RecordTerminalAsync(
            fixture.AuthorizedResult(TokenOne, certainty: SideEffectCertainty.PartiallyPerformed),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        var loaded = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence;
        loaded.State.ShouldBe(DurableOperationState.Completed);
        loaded.SideEffectCertainty.ShouldBe(SideEffectCertainty.PartiallyPerformed);
        loaded.TerminalResultRecorded.ShouldBeTrue();
        loaded.StartDefinitelyAbsent.ShouldBeFalse();
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenRepeatedWithAnEquivalentResult_IsIdempotent()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        _ = await journal.RecordTerminalAsync(fixture.AuthorizedResult(TokenOne), TestContext.Current.CancellationToken);

        var result = await journal.RecordTerminalAsync(
            fixture.AuthorizedResult(TokenOne), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenRepeatedWithADifferentResult_ReturnsFailed()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        _ = await journal.RecordTerminalAsync(fixture.AuthorizedResult(TokenOne, marker: 3), TestContext.Current.CancellationToken);

        var result = await journal.RecordTerminalAsync(
            fixture.AuthorizedResult(TokenOne, marker: 9), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(true);
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenPresentedTokenIsOlderThanCurrent_ReturnsFenced()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenTwo), TestContext.Current.CancellationToken);

        var result = await journal.RecordTerminalAsync(
            fixture.AuthorizedResult(TokenOne), TestContext.Current.CancellationToken);

        var fenced = result.ShouldBeOfType<DurableRecordFenced>();
        fenced.PresentedToken.ShouldBe(TokenOne);
        fenced.CurrentToken.ShouldBe(TokenTwo);
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenExecutionContextDiffers_ReturnsFailed()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        var foreignContext = DurableJournalTestData.Context(DurableJournalTestData.ForeignAuthorization());

        var result = await journal.RecordTerminalAsync(
            fixture.AuthorizedResult(TokenOne, context: foreignContext), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenCancelledBeforeCommit_DoesNotRecordATerminalResult()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            _ = await journal.RecordTerminalAsync(fixture.AuthorizedResult(TokenOne), cancellation.Token));

        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.TerminalResultRecorded.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadEvidenceAsync_WhenAddressIsNull_RejectsExactArgument()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            _ = await journal.LoadEvidenceAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("address");
    }

    [Fact]
    public async Task LoadEvidenceAsync_WhenNoRecordExists_ReturnsNotFoundWithTheRequestedAddress()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;

        var result = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<RecoveryEvidenceNotFound>().Address.ShouldBe(DurableJournalTestData.Address());
    }

    [Fact]
    public async Task LoadEvidenceAsync_WhenCancelledBeforeRead_Throws()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            _ = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), cancellation.Token));
    }

    [Fact]
    public async Task LoadEvidenceAsync_NeverReportsAnExternalReferenceOrWaitingState()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        _ = await journal.RecordCheckpointAsync(fixture.AuthorizedCheckpoint(TokenOne), TestContext.Current.CancellationToken);

        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);

        var loaded = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence;
        loaded.ExternalReference.ShouldBeNull();
        loaded.ExternalIdempotencyKey.ShouldBeNull();
        loaded.State.ShouldNotBe(DurableOperationState.Waiting);
    }

    [Fact]
    public async Task Addresses_WithDifferentOperationIdentities_AreTrackedIndependently()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        var otherOperationId = new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000002"));
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        var otherResult = await journal.RecordStartAsync(
            fixture.AuthorizedStart(TokenOne, operationId: otherOperationId), TestContext.Current.CancellationToken);

        _ = otherResult.ShouldBeOfType<DurableRecorded>();
        var firstEvidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        var otherEvidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(otherOperationId), TestContext.Current.CancellationToken);
        _ = firstEvidence.ShouldBeOfType<RecoveryEvidenceLoaded>();
        _ = otherEvidence.ShouldBeOfType<RecoveryEvidenceLoaded>();
    }

    [Fact]
    public async Task FullLifecycle_StartCheckpointTerminal_ReflectsEachTransitionInEvidence()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;

        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        var afterStart = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        afterStart.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.State.ShouldBe(DurableOperationState.Accepted);

        _ = await journal.RecordCheckpointAsync(fixture.AuthorizedCheckpoint(TokenOne), TestContext.Current.CancellationToken);
        var afterCheckpoint = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        afterCheckpoint.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.State.ShouldBe(DurableOperationState.EffectPending);

        _ = await journal.RecordTerminalAsync(fixture.AuthorizedResult(TokenOne), TestContext.Current.CancellationToken);
        var afterTerminal = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        var final = afterTerminal.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence;
        final.State.ShouldBe(DurableOperationState.Completed);
        final.TerminalResultRecorded.ShouldBeTrue();
        _ = final.LatestCheckpoint.ShouldNotBeNull();
    }

    [Fact]
    public async Task WriteMethods_WhenTheClockAndLoggerFail_StillReturnTheirCommittedOutcome()
    {
        var fixture = new DurableJournalFixture(new ThrowingTimeProvider(), new ThrowingLogger<InMemoryDurableOperationJournal>());
        var journal = fixture.Journal;

        var started = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        var checkpointed = await journal.RecordCheckpointAsync(fixture.AuthorizedCheckpoint(TokenOne), TestContext.Current.CancellationToken);
        var completed = await journal.RecordTerminalAsync(fixture.AuthorizedResult(TokenOne), TestContext.Current.CancellationToken);
        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);

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
        var fixture = new DurableJournalFixture(new ThrowingElapsedTimeProvider(), new ThrowingLogger<InMemoryDurableOperationJournal>());
        var journal = fixture.Journal;

        var started = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        var checkpointed = await journal.RecordCheckpointAsync(fixture.AuthorizedCheckpoint(TokenOne), TestContext.Current.CancellationToken);
        var completed = await journal.RecordTerminalAsync(fixture.AuthorizedResult(TokenOne), TestContext.Current.CancellationToken);
        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);

        _ = started.ShouldBeOfType<DurableRecorded>();
        _ = checkpointed.ShouldBeOfType<DurableRecorded>();
        _ = completed.ShouldBeOfType<DurableRecorded>();
        _ = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>();
    }

    [Fact]
    public async Task WriteMethods_WhenElapsedTimeIsNegative_StillReturnTheirCommittedOutcomeDespiteMetricsFailure()
    {
        // DurableJournalMetrics.RecordWrite and RecordEvidenceLoad reject a negative elapsed duration. Producing one
        // from a clock that never throws exercises FinishWrite's and FinishEvidenceLoad's own metrics-recording
        // catches, distinct from TryGetElapsedTime's catch around the clock call itself (covered above).
        var fixture = new DurableJournalFixture(new NegativeElapsedTimeProvider());
        var journal = fixture.Journal;

        var started = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);

        _ = started.ShouldBeOfType<DurableRecorded>();
        _ = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>();
    }

    [Fact]
    public async Task RecordStartAsync_WhenCancelledAndLoggingIsEnabled_RecordsCancellationEvent()
    {
        var recorder = new RecordingLogger<InMemoryDurableOperationJournal>();
        var fixture = new DurableJournalFixture(TimeProvider.System, recorder);
        var journal = fixture.Journal;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), cancellation.Token));

        recorder.Snapshot().ShouldContain(entry => entry.EventId.Id == 21001 && entry.Level == LogLevel.Debug);
    }

    [Fact]
    public async Task RecordStartAsync_WhenTheClockFailsDuringTheWriteAndLoggingIsEnabled_RecordsFailureEventAndPropagates()
    {
        var recorder = new RecordingLogger<InMemoryDurableOperationJournal>();
        var fixture = new DurableJournalFixture(new ThrowingUtcNowTimeProvider(), recorder);
        var journal = fixture.Journal;

        _ = await Should.ThrowAsync<InvalidTimeZoneException>(async () =>
            _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken));

        recorder.Snapshot().ShouldContain(entry => entry.EventId.Id == 21002 && entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task RecordStartAsync_WhenTheClockFailsForANewRecord_DoesNotCommitTheWrite()
    {
        // If the clock is read after the mutation, a caller that received the propagated exception would
        // wrongly conclude nothing was recorded while LoadEvidenceAsync would actually find a record.
        var clock = new ConditionalThrowingTimeProvider { ShouldThrow = true };
        var fixture = new DurableJournalFixture(clock);
        var journal = fixture.Journal;

        _ = await Should.ThrowAsync<InvalidTimeZoneException>(async () =>
            _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken));

        clock.ShouldThrow = false;
        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        _ = evidence.ShouldBeOfType<RecoveryEvidenceNotFound>();
    }

    [Fact]
    public async Task RecordStartAsync_WhenTheClockFailsOnARestart_DoesNotAdvanceTheWriterToken()
    {
        // A record still in the Accepted state may be restarted with a fresh fencing token; the clock
        // failure here must hit the same "read before mutate" ordering as the new-record path.
        var clock = new ConditionalThrowingTimeProvider();
        var fixture = new DurableJournalFixture(clock);
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        var tokenTwo = new FencingToken(2);

        clock.ShouldThrow = true;
        _ = await Should.ThrowAsync<InvalidTimeZoneException>(async () =>
            _ = await journal.RecordStartAsync(fixture.AuthorizedStart(tokenTwo), TestContext.Current.CancellationToken));

        clock.ShouldThrow = false;
        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.LastWriterToken.ShouldBe(TokenOne);
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenTheClockFails_DoesNotCommitTheWrite()
    {
        var clock = new ConditionalThrowingTimeProvider();
        var fixture = new DurableJournalFixture(clock);
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        clock.ShouldThrow = true;
        _ = await Should.ThrowAsync<InvalidTimeZoneException>(async () =>
            _ = await journal.RecordCheckpointAsync(fixture.AuthorizedCheckpoint(TokenOne), TestContext.Current.CancellationToken));

        clock.ShouldThrow = false;
        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        var loaded = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence;
        loaded.State.ShouldBe(DurableOperationState.Accepted);
        loaded.LatestCheckpoint.ShouldBeNull();
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenTheClockFails_DoesNotCommitTheWrite()
    {
        var clock = new ConditionalThrowingTimeProvider();
        var fixture = new DurableJournalFixture(clock);
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        clock.ShouldThrow = true;
        _ = await Should.ThrowAsync<InvalidTimeZoneException>(async () =>
            _ = await journal.RecordTerminalAsync(fixture.AuthorizedResult(TokenOne), TestContext.Current.CancellationToken));

        clock.ShouldThrow = false;
        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        var loaded = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence;
        loaded.State.ShouldBe(DurableOperationState.Accepted);
        loaded.TerminalResultRecorded.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadEvidenceAsync_WhenCancelledAndLoggingIsEnabled_RecordsCancellationEvent()
    {
        var recorder = new RecordingLogger<InMemoryDurableOperationJournal>();
        var fixture = new DurableJournalFixture(TimeProvider.System, recorder);
        var journal = fixture.Journal;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            _ = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), cancellation.Token));

        recorder.Snapshot().ShouldContain(entry => entry.EventId.Id == 21004 && entry.Level == LogLevel.Debug);
    }

    [Fact]
    public async Task RecordWaitingAsync_WhenWaitingIsNull_RejectsExactArgument()
    {
        var fixture = new DurableJournalFixture();

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            _ = await fixture.Journal.RecordWaitingAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("waiting");
    }

    [Fact]
    public async Task RecordWaitingAsync_WhenNoAcceptedRecordExists_ReturnsFailed()
    {
        var fixture = new DurableJournalFixture();

        var result = await fixture.Journal.RecordWaitingAsync(
            fixture.AuthorizedWaiting(TokenOne), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
    }

    [Fact]
    public async Task RecordWaitingAsync_WhenAccepted_RecordsTheWaitConditionAndItsCertainty()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        var wakeAt = DurableJournalTestData.Now.AddHours(2);
        var reference = new ExternalOperationReference(new DurableBackendKey("backend"), "handle-1");
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        var result = await journal.RecordWaitingAsync(
            fixture.AuthorizedWaiting(
                TokenOne,
                notBefore: wakeAt,
                certainty: SideEffectCertainty.Unknown,
                externalReference: reference,
                externalIdempotencyKey: new IdempotencyKey("external-1")),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        var loaded = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence;
        loaded.State.ShouldBe(DurableOperationState.Waiting);
        loaded.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
        loaded.NotBefore.ShouldBe(wakeAt);
        loaded.ExternalReference.ShouldBe(reference);
        loaded.ExternalIdempotencyKey.ShouldBe(new IdempotencyKey("external-1"));
        loaded.StartDefinitelyAbsent.ShouldBeFalse();
    }

    [Fact]
    public async Task RecordWaitingAsync_WhenPresentedTokenIsOlderThanCurrent_ReturnsFenced()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenTwo), TestContext.Current.CancellationToken);

        var result = await journal.RecordWaitingAsync(
            fixture.AuthorizedWaiting(TokenOne), TestContext.Current.CancellationToken);

        var fenced = result.ShouldBeOfType<DurableRecordFenced>();
        fenced.PresentedToken.ShouldBe(TokenOne);
        fenced.CurrentToken.ShouldBe(TokenTwo);
    }

    [Fact]
    public async Task RecordWaitingAsync_WhenATerminalRecordAlreadyExists_ReturnsFailedWithoutMutating()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        _ = await journal.RecordTerminalAsync(fixture.AuthorizedResult(TokenOne), TestContext.Current.CancellationToken);

        var result = await journal.RecordWaitingAsync(
            fixture.AuthorizedWaiting(TokenOne), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(true);
        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.State.ShouldBe(DurableOperationState.Completed);
    }

    [Fact]
    public async Task RecordWaitingAsync_WhenExecutionContextDiffers_ReturnsFailed()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        var foreignContext = DurableJournalTestData.Context(DurableJournalTestData.ForeignAuthorization());

        var result = await journal.RecordWaitingAsync(
            fixture.AuthorizedWaiting(TokenOne, context: foreignContext), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
    }

    [Fact]
    public async Task RecordWaitingAsync_WhenCancelledBeforeCommit_DoesNotChangeState()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            _ = await journal.RecordWaitingAsync(fixture.AuthorizedWaiting(TokenOne), cancellation.Token));

        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.State.ShouldBe(DurableOperationState.Accepted);
    }

    [Fact]
    public async Task RecordStartAsync_WhenAuthorized_ConsumesExactlyOneGrantUseAndAudits()
    {
        var fixture = new DurableJournalFixture();

        _ = await fixture.Journal.RecordStartAsync(
            fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        fixture.Harness.ConsumedCount.ShouldBe(1);
        fixture.Harness.Records.Count.ShouldBe(1);
        fixture.Harness.Records[0].EventKind.ShouldBe(SecurityAuditEventKind.GrantConsumptionIntent);
    }

    [Fact]
    public async Task RecordStartAsync_WhenAuthorized_AttachesTheEnforcementReceiptToTheCommittedRecord()
    {
        var fixture = new DurableJournalFixture();
        var request = fixture.AuthorizedStart(TokenOne);

        var result = await fixture.Journal.RecordStartAsync(request, TestContext.Current.CancellationToken);

        var enforcement = result.ShouldBeOfType<DurableRecorded>().Enforcement.ShouldNotBeNull();
        enforcement.GrantId.ShouldBe(request.Grant.Id);
        enforcement.IntentId.ShouldBe(request.Intent.Id);
        enforcement.AuditRecordId.ShouldBe(fixture.Harness.Records[0].Id);
    }

    [Fact]
    public async Task RecordStartAsync_WhenAuthorized_DispatchesOnlyRedactedAuditValues()
    {
        var fixture = new DurableJournalFixture();

        _ = await fixture.Journal.RecordStartAsync(
            fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        var fields = fixture.Harness.Records[0].Fields;
        fields.Keys.OrderBy(static key => key, StringComparer.Ordinal)
            .ShouldBe(["audience", "effect", "fingerprint", "kind", "resource"]);
        fields["fingerprint"].Kind.ShouldBe(SecurityAuditValueKind.Fingerprint);
        fields["resource"].Kind.ShouldBe(SecurityAuditValueKind.Fingerprint);
        fields["audience"].Value.ShouldBe(fixture.Journal.SecurityAudience.Value);
        fields.Values.ShouldAllBe(value =>
            !value.Value.Contains(DurableJournalTestData.SessionId.Value.ToString(), StringComparison.OrdinalIgnoreCase)
            && !value.Value.Contains(DurableJournalTestData.OperationId.Value.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecordStartAsync_WhenTheSameAuthorizedRequestIsReplayed_IsDeniedWithoutCommitting()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        var request = fixture.AuthorizedStart(TokenOne);
        _ = await journal.RecordStartAsync(request, TestContext.Current.CancellationToken);

        var replay = await journal.RecordStartAsync(request, TestContext.Current.CancellationToken);

        replay.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
        fixture.Harness.ConsumedCount.ShouldBe(1);
    }

    [Fact]
    public async Task RecordStartAsync_WhenTheGrantTargetsAnotherJournal_IsDeniedBeforeConsumption()
    {
        var fixture = new DurableJournalFixture();
        var start = DurableJournalTestData.Start(TokenOne);
        var foreign = fixture.Harness.Authorize(
            start,
            new DurableJournalKey("other-journal"),
            fixture.Journal.SecurityAudience,
            start.Descriptor.Binding.Address,
            start.Descriptor.Binding.ExecutionContext.Authorization,
            DurableJournalSecurityBinding.Fingerprint(start),
            SecurityOperationKind.StateMutation,
            SecurityEffect.Create,
            TokenOne);

        var result = await fixture.Journal.RecordStartAsync(foreign, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
        fixture.Harness.ConsumedCount.ShouldBe(0);
        await AssertNoRecordAsync(fixture);
    }

    [Fact]
    public async Task RecordStartAsync_WhenTheIntentFenceDiffersFromThePresentedToken_IsDeniedBeforeConsumption()
    {
        var fixture = new DurableJournalFixture();
        var start = DurableJournalTestData.Start(TokenOne);
        var mismatched = fixture.Harness.Authorize(
            start,
            DurableJournalTestData.JournalKey,
            fixture.Journal.SecurityAudience,
            start.Descriptor.Binding.Address,
            start.Descriptor.Binding.ExecutionContext.Authorization,
            DurableJournalSecurityBinding.Fingerprint(start),
            SecurityOperationKind.StateMutation,
            SecurityEffect.Create,
            TokenTwo);

        var result = await fixture.Journal.RecordStartAsync(mismatched, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
        fixture.Harness.ConsumedCount.ShouldBe(0);
        await AssertNoRecordAsync(fixture);
    }

    [Fact]
    public async Task RecordStartAsync_WhenTheGrantBindingDoesNotMatchTheRecomputedEnforcement_IsDenied()
    {
        var fixture = new DurableJournalFixture();
        var start = DurableJournalTestData.Start(TokenOne);
        var wrongEffect = fixture.Harness.Authorize(
            start,
            DurableJournalTestData.JournalKey,
            fixture.Journal.SecurityAudience,
            start.Descriptor.Binding.Address,
            start.Descriptor.Binding.ExecutionContext.Authorization,
            DurableJournalSecurityBinding.Fingerprint(start),
            SecurityOperationKind.StateMutation,
            SecurityEffect.Delete,
            TokenOne);

        var result = await fixture.Journal.RecordStartAsync(wrongEffect, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
        await AssertNoRecordAsync(fixture);
    }

    [Theory]
    [InlineData(GrantConsumptionStatus.Expired)]
    [InlineData(GrantConsumptionStatus.Revoked)]
    [InlineData(GrantConsumptionStatus.Exhausted)]
    [InlineData(GrantConsumptionStatus.Mismatch)]
    public async Task RecordStartAsync_WhenTheGrantIsNotConsumed_IsDeniedWithoutCommitting(GrantConsumptionStatus status)
    {
        var fixture = new DurableJournalFixture();
        fixture.Harness.ForcedConsumptionStatus = status;

        var result = await fixture.Journal.RecordStartAsync(
            fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
        fixture.Harness.Records.ShouldBeEmpty();
        fixture.Harness.ForcedConsumptionStatus = null;
        await AssertNoRecordAsync(fixture);
    }

    [Fact]
    public async Task RecordStartAsync_WhenTheGrantStoreReturnsAnUnrelatedIntentReceipt_IsDeniedWithoutCommitting()
    {
        var fixture = new DurableJournalFixture();
        fixture.Harness.ForgeIntentReceipt = true;

        var result = await fixture.Journal.RecordStartAsync(
            fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
        fixture.Harness.ForgeIntentReceipt = false;
        await AssertNoRecordAsync(fixture);
    }

    [Fact]
    public async Task RecordStartAsync_WhenRequiredAuditRefusesTheRecord_IsDeniedWithoutCommitting()
    {
        var fixture = new DurableJournalFixture();
        fixture.Harness.RejectAudit = true;

        var result = await fixture.Journal.RecordStartAsync(
            fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
        fixture.Harness.RejectAudit = false;
        await AssertNoRecordAsync(fixture);
    }

    [Fact]
    public async Task RecordStartAsync_WhenTheAuditDispatcherIsUnavailable_IsDeniedWithoutCommitting()
    {
        var fixture = new DurableJournalFixture();
        fixture.Harness.FailAudit = true;

        var result = await fixture.Journal.RecordStartAsync(
            fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
        fixture.Harness.FailAudit = false;
        await AssertNoRecordAsync(fixture);
    }

    [Fact]
    public async Task RecordStartAsync_WhenTheGrantStoreIsUnavailable_IsDeniedWithoutCommitting()
    {
        var fixture = new DurableJournalFixture();
        fixture.Harness.FailGrantStore = true;

        var result = await fixture.Journal.RecordStartAsync(
            fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
        fixture.Harness.FailGrantStore = false;
        await AssertNoRecordAsync(fixture);
    }

    [Fact]
    public async Task RecordStartAsync_WhenDeniedAndLoggingIsEnabled_RecordsTheDenialEvent()
    {
        var recorder = new RecordingLogger<InMemoryDurableOperationJournal>();
        var fixture = new DurableJournalFixture(TimeProvider.System, recorder);
        fixture.Harness.RejectAudit = true;

        _ = await fixture.Journal.RecordStartAsync(
            fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);

        recorder.Snapshot().ShouldContain(entry => entry.EventId.Id == 21005 && entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenDenied_IsRefusedAfterAnAcceptedRecordExists()
    {
        var fixture = new DurableJournalFixture();
        var journal = fixture.Journal;
        _ = await journal.RecordStartAsync(fixture.AuthorizedStart(TokenOne), TestContext.Current.CancellationToken);
        fixture.Harness.RejectAudit = true;

        var result = await journal.RecordCheckpointAsync(
            fixture.AuthorizedCheckpoint(TokenOne), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
        fixture.Harness.RejectAudit = false;
        var evidence = await journal.LoadEvidenceAsync(fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence.LatestCheckpoint.ShouldBeNull();
    }

    [Fact]
    public async Task LoadEvidenceAsync_WhenAuthorized_ConsumesAGrantAndAudits()
    {
        var fixture = new DurableJournalFixture();

        _ = await fixture.Journal.LoadEvidenceAsync(
            fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);

        fixture.Harness.ConsumedCount.ShouldBe(1);
        fixture.Harness.Records.Count.ShouldBe(1);
    }

    [Fact]
    public async Task LoadEvidenceAsync_WhenTheIntentRequiresAFence_ReportsUnavailable()
    {
        var fixture = new DurableJournalFixture();
        var address = DurableJournalTestData.Address();
        var fenced = fixture.Harness.Authorize(
            address,
            DurableJournalTestData.JournalKey,
            fixture.Journal.SecurityAudience,
            address,
            DurableJournalTestData.Authorization(),
            DurableJournalSecurityBinding.Fingerprint(address),
            SecurityOperationKind.StateRead,
            SecurityEffect.Observe,
            TokenOne);

        var result = await fixture.Journal.LoadEvidenceAsync(fenced, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<RecoveryEvidenceUnavailable>();
        fixture.Harness.ConsumedCount.ShouldBe(0);
    }

    [Fact]
    public async Task LoadEvidenceAsync_WhenRequiredAuditIsUnavailable_ReportsUnavailableRatherThanNotFound()
    {
        var fixture = new DurableJournalFixture();
        fixture.Harness.RejectAudit = true;

        var result = await fixture.Journal.LoadEvidenceAsync(
            fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<RecoveryEvidenceUnavailable>();
    }

    private static async Task AssertNoRecordAsync(DurableJournalFixture fixture)
    {
        var evidence = await fixture.Journal.LoadEvidenceAsync(
            fixture.AuthorizedAddress(), TestContext.Current.CancellationToken);
        _ = evidence.ShouldBeOfType<RecoveryEvidenceNotFound>();
    }

    /// <summary>Produces distinct audit-record identities for constructor-argument tests.</summary>
    private sealed class TestAuditRecordIdGenerator: IIdentifierGenerator<SecurityAuditRecordId>
    {
        public SecurityAuditRecordId Create() => new(Guid.NewGuid());
    }

    private sealed class ThrowingUtcNowTimeProvider: TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => throw new InvalidTimeZoneException("clock failure during write");
    }

    /// <summary>A clock that only fails GetUtcNow() once armed, so a prior write can seed real state first.</summary>
    private sealed class ConditionalThrowingTimeProvider: TimeProvider
    {
        public bool ShouldThrow { get; set; }

        public override DateTimeOffset GetUtcNow() => ShouldThrow
            ? throw new InvalidTimeZoneException("clock failure during write")
            : base.GetUtcNow();
    }

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

    /// <summary>A clock that never throws but reports a negative elapsed duration, which the bounded metrics
    /// recorders reject; this exercises the journal's own metrics-recording catch rather than its clock-failure
    /// catch. Each operation calls <see cref="GetTimestamp"/> once at its start and once more internally through
    /// the default <see cref="TimeProvider.GetElapsedTime(long)"/> at its finish; alternating a later raw value on
    /// the first call of each pair with an earlier one on the second makes every operation's derived elapsed
    /// duration negative, without either call ever throwing.</summary>
    private sealed class NegativeElapsedTimeProvider: TimeProvider
    {
        private int _calls;

        public override DateTimeOffset GetUtcNow() => DurableJournalTestData.Now;

        public override long GetTimestamp() => ++_calls % 2 == 1 ? 1000 : 0;
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
