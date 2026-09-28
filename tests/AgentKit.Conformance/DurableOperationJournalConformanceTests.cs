// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines portable recording, fencing, evidence, authorization, and cancellation behavior for <see cref="IDurableOperationJournal"/>.</summary>
/// <typeparam name="TFixture">The adapter-specific isolated fixture.</typeparam>
/// <remarks>
/// Every case here is required contract behavior for all journal adapters. Durability-only cases run when the
/// fixture declares <see cref="ConformanceCapabilities.SupportsDurability"/>; an ephemeral adapter skips them rather
/// than reporting a guarantee it does not provide.
/// </remarks>
public abstract class DurableOperationJournalConformanceTests<TFixture>
    where TFixture : IDurableOperationJournalConformanceFixture, new()
{
    private static readonly FencingToken TokenOne = new(1);
    private static readonly FencingToken TokenTwo = new(2);

    /// <summary>Verifies an unrecorded address reports absence rather than fabricating evidence.</summary>
    [Fact]
    public async Task LoadEvidenceAsync_WhenNoRecordExists_ReportsNotFoundForTheRequestedAddress()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();

        var evidence = await journal.LoadEvidenceAsync(
            fixture.Authorize(DurabilityConformanceData.Address()), TestContext.Current.CancellationToken);

        evidence.ShouldBeOfType<RecoveryEvidenceNotFound>().Address.ShouldBe(DurabilityConformanceData.Address());
    }

    /// <summary>Verifies acceptance commits under the presented ownership generation.</summary>
    [Fact]
    public async Task RecordStartAsync_WhenNoPriorRecordExists_CommitsUnderThePresentedToken()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();

        var result = await journal.RecordStartAsync(
            fixture.Authorize(DurabilityConformanceData.Start(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecorded>().FencingToken.ShouldBe(TokenOne);
    }

    /// <summary>Verifies acceptance alone proves the effect has not started.</summary>
    [Fact]
    public async Task LoadEvidenceAsync_AfterAcceptance_ReportsAcceptedWithTheStartDefinitelyAbsent()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        _ = await journal.RecordStartAsync(
            fixture.Authorize(DurabilityConformanceData.Start(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        var evidence = await journal.LoadEvidenceAsync(
            fixture.Authorize(DurabilityConformanceData.Address()), TestContext.Current.CancellationToken);

        var loaded = evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence;
        loaded.State.ShouldBe(DurableOperationState.Accepted);
        loaded.StartDefinitelyAbsent.ShouldBeTrue();
        loaded.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        loaded.TerminalResultRecorded.ShouldBeFalse();
        loaded.RecordedResult.ShouldBeNull();
        loaded.LastWriterToken.ShouldBe(TokenOne);
    }

    /// <summary>Verifies a checkpoint cannot precede acceptance and commits nothing when it tries.</summary>
    [Fact]
    public async Task RecordCheckpointAsync_WhenNoAcceptedRecordExists_FailsWithoutCommitting()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();

        var result = await journal.RecordCheckpointAsync(
            fixture.Authorize(DurabilityConformanceData.CheckpointRecord(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
        await ShouldNotBeRecordedAsync(fixture, journal);
    }

    /// <summary>Verifies a checkpoint makes the effect's outcome unknown rather than absent.</summary>
    [Fact]
    public async Task RecordCheckpointAsync_WhenAccepted_MovesToEffectPendingWithUnknownCertainty()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenOne);

        var result = await journal.RecordCheckpointAsync(
            fixture.Authorize(DurabilityConformanceData.CheckpointRecord(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
        var loaded = await LoadAsync(fixture, journal);
        loaded.State.ShouldBe(DurableOperationState.EffectPending);
        loaded.StartDefinitelyAbsent.ShouldBeFalse();
        loaded.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
        loaded.LatestCheckpoint.ShouldNotBeNull().Id.ShouldBe(DurabilityConformanceData.Checkpoint);
    }

    /// <summary>Verifies the latest snapshot replaces the previous one rather than accumulating.</summary>
    [Fact]
    public async Task RecordCheckpointAsync_WhenRepeated_RetainsOnlyTheLatestSnapshot()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenOne);
        _ = await journal.RecordCheckpointAsync(
            fixture.Authorize(DurabilityConformanceData.CheckpointRecord(fixture.JournalKey, TokenOne, marker: 5)),
            TestContext.Current.CancellationToken);

        _ = await journal.RecordCheckpointAsync(
            fixture.Authorize(DurabilityConformanceData.CheckpointRecord(fixture.JournalKey, TokenOne, marker: 9)),
            TestContext.Current.CancellationToken);

        var loaded = await LoadAsync(fixture, journal);
        loaded.LatestCheckpoint.ShouldNotBeNull().State.ShouldBe(DurabilityConformanceData.Payload(9));
    }

    /// <summary>Verifies a terminal record cannot precede acceptance and commits nothing when it tries.</summary>
    [Fact]
    public async Task RecordTerminalAsync_WhenNoAcceptedRecordExists_FailsWithoutCommitting()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();

        var result = await journal.RecordTerminalAsync(
            fixture.Authorize(DurabilityConformanceData.Result(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
        await ShouldNotBeRecordedAsync(fixture, journal);
    }

    /// <summary>Verifies the terminal record's own certainty and result become the operation's evidence.</summary>
    [Fact]
    public async Task RecordTerminalAsync_WhenAccepted_RetainsTheResultAndItsCertainty()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenOne);
        var terminal = DurabilityConformanceData.Result(
            fixture.JournalKey, TokenOne, certainty: SideEffectCertainty.PartiallyPerformed);

        var result = await journal.RecordTerminalAsync(
            fixture.Authorize(terminal), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
        var loaded = await LoadAsync(fixture, journal);
        loaded.State.ShouldBe(DurableOperationState.Completed);
        loaded.SideEffectCertainty.ShouldBe(SideEffectCertainty.PartiallyPerformed);
        loaded.TerminalResultRecorded.ShouldBeTrue();
        loaded.RecordedResult.ShouldBe(terminal);
        loaded.StartDefinitelyAbsent.ShouldBeFalse();
    }

    /// <summary>Verifies a replayed identical terminal record does not become a conflict.</summary>
    [Fact]
    public async Task RecordTerminalAsync_WhenRepeatedWithAnEquivalentResult_IsIdempotent()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenOne);
        _ = await journal.RecordTerminalAsync(
            fixture.Authorize(DurabilityConformanceData.Result(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        var result = await journal.RecordTerminalAsync(
            fixture.Authorize(DurabilityConformanceData.Result(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
    }

    /// <summary>Verifies a conflicting terminal record is refused while the committed one is preserved.</summary>
    [Fact]
    public async Task RecordTerminalAsync_WhenADifferentResultIsAlreadyRecorded_FailsAndPreservesTheCommittedResult()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenOne);
        var committed = DurabilityConformanceData.Result(fixture.JournalKey, TokenOne, marker: 3);
        _ = await journal.RecordTerminalAsync(fixture.Authorize(committed), TestContext.Current.CancellationToken);

        var result = await journal.RecordTerminalAsync(
            fixture.Authorize(DurabilityConformanceData.Result(fixture.JournalKey, TokenOne, marker: 9)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(true);
        (await LoadAsync(fixture, journal)).RecordedResult.ShouldBe(committed);
    }

    /// <summary>Verifies a waiting record makes the deferred condition discoverable without settling the operation.</summary>
    [Fact]
    public async Task RecordWaitingAsync_WhenAccepted_RecordsTheWaitConditionWithoutATerminalResult()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenOne);
        var wakeAt = DurabilityConformanceData.Now.AddHours(3);
        var reference = new ExternalOperationReference(new DurableBackendKey("conformance-backend"), "handle-1");

        var result = await journal.RecordWaitingAsync(
            fixture.Authorize(DurabilityConformanceData.Waiting(
                fixture.JournalKey,
                TokenOne,
                notBefore: wakeAt,
                externalReference: reference,
                externalIdempotencyKey: new IdempotencyKey("external-conformance"))),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
        var loaded = await LoadAsync(fixture, journal);
        loaded.State.ShouldBe(DurableOperationState.Waiting);
        loaded.NotBefore.ShouldBe(wakeAt);
        loaded.ExternalReference.ShouldBe(reference);
        loaded.ExternalIdempotencyKey.ShouldBe(new IdempotencyKey("external-conformance"));
        loaded.TerminalResultRecorded.ShouldBeFalse();
    }

    /// <summary>Verifies waiting cannot precede acceptance and commits nothing when it tries.</summary>
    [Fact]
    public async Task RecordWaitingAsync_WhenNoAcceptedRecordExists_FailsWithoutCommitting()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();

        var result = await journal.RecordWaitingAsync(
            fixture.Authorize(DurabilityConformanceData.Waiting(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(false);
        await ShouldNotBeRecordedAsync(fixture, journal);
    }

    /// <summary>Verifies a settled operation accepts no further checkpoint and keeps its terminal state.</summary>
    [Fact]
    public async Task RecordCheckpointAsync_WhenATerminalRecordExists_FailsAndPreservesTheTerminalState()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenOne);
        _ = await journal.RecordTerminalAsync(
            fixture.Authorize(DurabilityConformanceData.Result(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        var result = await journal.RecordCheckpointAsync(
            fixture.Authorize(DurabilityConformanceData.CheckpointRecord(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(true);
        (await LoadAsync(fixture, journal)).State.ShouldBe(DurableOperationState.Completed);
    }

    /// <summary>Verifies a settled operation accepts no further waiting record.</summary>
    [Fact]
    public async Task RecordWaitingAsync_WhenATerminalRecordExists_FailsAndPreservesTheTerminalState()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenOne);
        _ = await journal.RecordTerminalAsync(
            fixture.Authorize(DurabilityConformanceData.Result(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        var result = await journal.RecordWaitingAsync(
            fixture.Authorize(DurabilityConformanceData.Waiting(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFailed>().Committed.ShouldBe(true);
        (await LoadAsync(fixture, journal)).State.ShouldBe(DurableOperationState.Completed);
    }

    /// <summary>Verifies a stale owner's acceptance is fenced and reports the authoritative generation.</summary>
    [Fact]
    public async Task RecordStartAsync_WhenThePresentedTokenIsOlder_IsFencedAndReportsTheCurrentToken()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenTwo);

        var result = await journal.RecordStartAsync(
            fixture.Authorize(DurabilityConformanceData.Start(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        var fenced = result.ShouldBeOfType<DurableRecordFenced>();
        fenced.PresentedToken.ShouldBe(TokenOne);
        fenced.CurrentToken.ShouldBe(TokenTwo);
    }

    /// <summary>Verifies a stale owner's checkpoint is fenced rather than silently accepted.</summary>
    [Fact]
    public async Task RecordCheckpointAsync_WhenThePresentedTokenIsOlder_IsFenced()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenTwo);

        var result = await journal.RecordCheckpointAsync(
            fixture.Authorize(DurabilityConformanceData.CheckpointRecord(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFenced>().CurrentToken.ShouldBe(TokenTwo);
    }

    /// <summary>Verifies a stale owner cannot settle an operation a newer owner took over.</summary>
    [Fact]
    public async Task RecordTerminalAsync_WhenThePresentedTokenIsOlder_IsFencedAndRecordsNoResult()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenTwo);

        var result = await journal.RecordTerminalAsync(
            fixture.Authorize(DurabilityConformanceData.Result(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFenced>().CurrentToken.ShouldBe(TokenTwo);
        (await LoadAsync(fixture, journal)).TerminalResultRecorded.ShouldBeFalse();
    }

    /// <summary>Verifies a stale owner cannot defer an operation a newer owner took over.</summary>
    [Fact]
    public async Task RecordWaitingAsync_WhenThePresentedTokenIsOlder_IsFenced()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenTwo);

        var result = await journal.RecordWaitingAsync(
            fixture.Authorize(DurabilityConformanceData.Waiting(fixture.JournalKey, TokenOne)),
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<DurableRecordFenced>().CurrentToken.ShouldBe(TokenTwo);
    }

    /// <summary>Verifies a newer owner may retake an operation that has not progressed past acceptance.</summary>
    [Fact]
    public async Task RecordStartAsync_WhenANewerTokenRetakesAnAcceptedOperation_AdvancesOwnership()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenOne);

        var result = await journal.RecordStartAsync(
            fixture.Authorize(DurabilityConformanceData.Start(fixture.JournalKey, TokenTwo)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
        (await LoadAsync(fixture, journal)).LastWriterToken.ShouldBe(TokenTwo);
    }

    /// <summary>Verifies records for distinct operations never observe each other.</summary>
    [Fact]
    public async Task Addresses_WithDifferentOperationIdentities_AreTrackedIndependently()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        var other = DurabilityConformanceData.OtherOperation;
        await AcceptAsync(fixture, journal, TokenOne);

        _ = await journal.RecordStartAsync(
            fixture.Authorize(DurabilityConformanceData.Start(fixture.JournalKey, TokenOne, other)),
            TestContext.Current.CancellationToken);
        _ = await journal.RecordTerminalAsync(
            fixture.Authorize(DurabilityConformanceData.Result(fixture.JournalKey, TokenOne, other)),
            TestContext.Current.CancellationToken);

        (await LoadAsync(fixture, journal)).State.ShouldBe(DurableOperationState.Accepted);
        (await LoadAsync(fixture, journal, other)).State.ShouldBe(DurableOperationState.Completed);
    }

    /// <summary>Verifies a previously authorized request cannot be replayed into a second commit.</summary>
    [Fact]
    public async Task RecordStartAsync_WhenTheSameAuthorizedRequestIsReplayed_IsRefused()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        var request = fixture.Authorize(DurabilityConformanceData.Start(fixture.JournalKey, TokenOne));
        _ = await journal.RecordStartAsync(request, TestContext.Current.CancellationToken);

        var replay = await journal.RecordStartAsync(request, TestContext.Current.CancellationToken);

        replay.ShouldNotBeOfType<DurableRecorded>();
    }

    /// <summary>Verifies acceptance cancelled before commit leaves no record behind.</summary>
    [Fact]
    public async Task RecordStartAsync_WhenCancelledBeforeCommit_CommitsNoRecord()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => _ = await journal.RecordStartAsync(
            fixture.Authorize(DurabilityConformanceData.Start(fixture.JournalKey, TokenOne)), cancellation.Token));

        await ShouldNotBeRecordedAsync(fixture, journal);
    }

    /// <summary>Verifies a checkpoint cancelled before commit leaves the accepted state unchanged.</summary>
    [Fact]
    public async Task RecordCheckpointAsync_WhenCancelledBeforeCommit_LeavesTheStateUnchanged()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenOne);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => _ = await journal.RecordCheckpointAsync(
            fixture.Authorize(DurabilityConformanceData.CheckpointRecord(fixture.JournalKey, TokenOne)),
            cancellation.Token));

        var loaded = await LoadAsync(fixture, journal);
        loaded.State.ShouldBe(DurableOperationState.Accepted);
        loaded.LatestCheckpoint.ShouldBeNull();
    }

    /// <summary>Verifies a terminal record cancelled before commit does not settle the operation.</summary>
    [Fact]
    public async Task RecordTerminalAsync_WhenCancelledBeforeCommit_RecordsNoTerminalResult()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenOne);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => _ = await journal.RecordTerminalAsync(
            fixture.Authorize(DurabilityConformanceData.Result(fixture.JournalKey, TokenOne)), cancellation.Token));

        (await LoadAsync(fixture, journal)).TerminalResultRecorded.ShouldBeFalse();
    }

    /// <summary>Verifies a waiting record cancelled before commit does not defer the operation.</summary>
    [Fact]
    public async Task RecordWaitingAsync_WhenCancelledBeforeCommit_LeavesTheStateUnchanged()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenOne);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => _ = await journal.RecordWaitingAsync(
            fixture.Authorize(DurabilityConformanceData.Waiting(fixture.JournalKey, TokenOne)), cancellation.Token));

        (await LoadAsync(fixture, journal)).State.ShouldBe(DurableOperationState.Accepted);
    }

    /// <summary>Verifies an evidence read cancelled before it completes throws instead of reporting absence.</summary>
    [Fact]
    public async Task LoadEvidenceAsync_WhenCancelledBeforeTheRead_Throws() =>
        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            var fixture = new TFixture();
            var journal = fixture.CreateJournal();
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            _ = await journal.LoadEvidenceAsync(
                fixture.Authorize(DurabilityConformanceData.Address()), cancellation.Token);
        });

    /// <summary>Verifies every argument-null guard names its own parameter before any authorization happens.</summary>
    [Fact]
    public async Task RecordMethods_WhenTheRequestIsNull_RejectTheExactArgument()
    {
        var fixture = new TFixture();
        var journal = fixture.CreateJournal();

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            _ = await journal.RecordStartAsync(null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("start");
        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            _ = await journal.RecordCheckpointAsync(null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("checkpoint");
        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            _ = await journal.RecordTerminalAsync(null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("result");
        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            _ = await journal.RecordWaitingAsync(null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("waiting");
        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            _ = await journal.LoadEvidenceAsync(null!, TestContext.Current.CancellationToken))).ParamName.ShouldBe("address");
    }

    /// <summary>Verifies a durable adapter's acknowledged records survive reopening its storage.</summary>
    [Fact]
    public async Task Reopen_WhenTheAdapterIsDurable_ObservesEveryAcknowledgedRecord()
    {
        var fixture = new TFixture();
        if (!fixture.Capabilities.SupportsDurability)
        {
            return;
        }

        var journal = fixture.CreateJournal();
        await AcceptAsync(fixture, journal, TokenOne);
        var terminal = DurabilityConformanceData.Result(fixture.JournalKey, TokenOne);
        _ = await journal.RecordTerminalAsync(fixture.Authorize(terminal), TestContext.Current.CancellationToken);

        var reopened = fixture.Reopen();

        var loaded = await LoadAsync(fixture, reopened);
        loaded.State.ShouldBe(DurableOperationState.Completed);
        loaded.RecordedResult.ShouldBe(terminal);
        loaded.LastWriterToken.ShouldBe(TokenOne);
    }

    private static async Task AcceptAsync(TFixture fixture, IDurableOperationJournal journal, FencingToken token)
    {
        var result = await journal.RecordStartAsync(
            fixture.Authorize(DurabilityConformanceData.Start(fixture.JournalKey, token)),
            TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<DurableRecorded>();
    }

    private static async Task<RecoveryEvidence> LoadAsync(
        TFixture fixture,
        IDurableOperationJournal journal,
        OperationId? operationId = null)
    {
        var evidence = await journal.LoadEvidenceAsync(
            fixture.Authorize(DurabilityConformanceData.Address(operationId)), TestContext.Current.CancellationToken);
        return evidence.ShouldBeOfType<RecoveryEvidenceLoaded>().Evidence;
    }

    private static async Task ShouldNotBeRecordedAsync(TFixture fixture, IDurableOperationJournal journal)
    {
        var evidence = await journal.LoadEvidenceAsync(
            fixture.Authorize(DurabilityConformanceData.Address()), TestContext.Current.CancellationToken);
        _ = evidence.ShouldBeOfType<RecoveryEvidenceNotFound>();
    }
}
