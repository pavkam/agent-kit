// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

public sealed class FencedDurableOperationJournalTests
{
    private static readonly FencingToken Held = new(7);

    [Fact]
    public void Constructor_WhenInnerJournalIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new FencedDurableOperationJournal(null!, Lease()));

        exception.ParamName.ShouldBe("inner");
    }

    [Fact]
    public void Constructor_WhenLeaseIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new FencedDurableOperationJournal(new RecordingDurableOperationJournal(), null!));

        exception.ParamName.ShouldBe("lease");
    }

    [Fact]
    public void SecurityAudience_WhenDecorating_ReportsTheDecoratedJournalAudience()
    {
        var inner = new RecordingDurableOperationJournal();
        var journal = new FencedDurableOperationJournal(inner, Lease());

        journal.SecurityAudience.ShouldBe(inner.SecurityAudience);
    }

    [Fact]
    public async Task RecordStartAsync_WhenStartIsNull_ThrowsArgumentNullException()
    {
        var journal = new FencedDurableOperationJournal(new RecordingDurableOperationJournal(), Lease());

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await journal.RecordStartAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("start");
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenCheckpointIsNull_ThrowsArgumentNullException()
    {
        var journal = new FencedDurableOperationJournal(new RecordingDurableOperationJournal(), Lease());

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await journal.RecordCheckpointAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("checkpoint");
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenResultIsNull_ThrowsArgumentNullException()
    {
        var journal = new FencedDurableOperationJournal(new RecordingDurableOperationJournal(), Lease());

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await journal.RecordTerminalAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("result");
    }

    [Fact]
    public async Task RecordWaitingAsync_WhenWaitingIsNull_ThrowsArgumentNullException()
    {
        var journal = new FencedDurableOperationJournal(new RecordingDurableOperationJournal(), Lease());

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await journal.RecordWaitingAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("waiting");
    }

    [Fact]
    public async Task RecordStartAsync_WhenTheHeldGenerationIsPresented_ForwardsToTheInnerJournal()
    {
        var inner = new RecordingDurableOperationJournal();
        var journal = new FencedDurableOperationJournal(inner, Lease());

        var result = await journal.RecordStartAsync(Start(Held), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
        inner.Calls.ShouldBe(["RecordStart"]);
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenAnOlderGenerationIsPresented_ReportsFencedWithoutWriting()
    {
        var inner = new RecordingDurableOperationJournal();
        var journal = new FencedDurableOperationJournal(inner, Lease());
        var stale = new FencingToken(Held.Value - 1);

        var result = await journal.RecordTerminalAsync(Terminal(stale), TestContext.Current.CancellationToken);

        var fenced = result.ShouldBeOfType<DurableRecordFenced>();
        fenced.PresentedToken.ShouldBe(stale);
        fenced.CurrentToken.ShouldBe(Held);
        inner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenANewerGenerationIsPresented_FailsClosedAndProvesNothingWasCommitted()
    {
        // Presenting a generation the attempt never acquired is a caller error, not a takeover. Forwarding it would
        // let a worker write under authority it cannot demonstrate.
        var inner = new RecordingDurableOperationJournal();
        var journal = new FencedDurableOperationJournal(inner, Lease());

        var result = await journal.RecordTerminalAsync(
            Terminal(new FencingToken(Held.Value + 1)), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<DurableRecordFailed>();
        failed.Committed.ShouldBe(false);
        inner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecordWaitingAsync_WhenAnOlderGenerationIsPresented_ReportsFencedWithoutWriting()
    {
        var inner = new RecordingDurableOperationJournal();
        var journal = new FencedDurableOperationJournal(inner, Lease());

        var result = await journal.RecordWaitingAsync(
            Waiting(new FencingToken(Held.Value - 2)), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecordFenced>();
        inner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecordCheckpointAsync_WhenAnOlderGenerationIsPresented_ReportsFencedWithoutWriting()
    {
        var inner = new RecordingDurableOperationJournal();
        var journal = new FencedDurableOperationJournal(inner, Lease());

        var result = await journal.RecordCheckpointAsync(
            Checkpoint(new FencingToken(Held.Value - 3)), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecordFenced>();
        inner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadEvidenceAsync_WhenReading_ForwardsWithoutRequiringAFence()
    {
        // A recovering worker must learn what happened before it seeks ownership, so the read carries no generation.
        var inner = new RecordingDurableOperationJournal();
        var journal = new FencedDurableOperationJournal(inner, Lease());
        var address = DurableJournalTestData.Address();

        var result = await journal.LoadEvidenceAsync(
            Authorized(address), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<RecoveryEvidenceNotFound>();
        inner.Calls.ShouldBe(["LoadEvidence"]);
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenTwoWorkersHoldDifferentGenerations_OnlyTheCurrentOwnerReachesTheStore()
    {
        var inner = new RecordingDurableOperationJournal();
        var displaced = new FencedDurableOperationJournal(inner, Lease(new FencingToken(1)));
        var owner = new FencedDurableOperationJournal(inner, Lease(new FencingToken(2)));

        var displacedWrite = await displaced.RecordTerminalAsync(
            Terminal(new FencingToken(2)), TestContext.Current.CancellationToken);
        var ownerWrite = await owner.RecordTerminalAsync(
            Terminal(new FencingToken(2)), TestContext.Current.CancellationToken);

        // The displaced worker still presents its own generation; it is refused before the store is touched.
        _ = displacedWrite.ShouldBeOfType<DurableRecordFailed>();
        _ = ownerWrite.ShouldBeOfType<DurableRecorded>();
        inner.Calls.ShouldBe(["RecordTerminal"]);
    }

    [Fact]
    public async Task RecordStartAsync_WhenConcurrentWritersPresentTheHeldGeneration_AllReachTheStore()
    {
        // The decorator adds no serialization of its own: concurrency control belongs to the journal store.
        var inner = new RecordingDurableOperationJournal();
        var journal = new FencedDurableOperationJournal(inner, Lease());

        for (var index = 0; index < 4; index++)
        {
            _ = await journal.RecordStartAsync(Start(Held), TestContext.Current.CancellationToken);
        }

        inner.Calls.Count.ShouldBe(4);
    }

    private static TestExecutionLease Lease(FencingToken? token = null) =>
        new(DurableJournalTestData.Address(), token ?? Held);

    private static AuthorizedDurableRequest<DurableOperationStart> Start(FencingToken token) =>
        Authorized(new DurableOperationStart(
            DurableJournalTestData.Descriptor(),
            DurableJournalTestData.Payload(),
            token,
            DurableJournalTestData.Now));

    private static AuthorizedDurableRequest<DurableOperationResult> Terminal(FencingToken token) =>
        Authorized(DurableJournalTestData.Result(token));

    private static AuthorizedDurableRequest<DurableOperationWaiting> Waiting(FencingToken token) =>
        Authorized(new DurableOperationWaiting(
            DurableJournalTestData.Descriptor().Binding,
            token,
            DurableJournalTestData.Now,
            SideEffectCertainty.Unknown,
            notBefore: DurableJournalTestData.Now.AddMinutes(1)));

    private static AuthorizedDurableRequest<DurableCheckpoint> Checkpoint(FencingToken token) =>
        Authorized(new DurableCheckpoint(
            new CheckpointId(Guid.Parse("a0000000-0000-0000-0000-000000000001")),
            DurableJournalTestData.Descriptor().Binding,
            DurableCheckpointKind.ToolCallRecorded,
            DurableJournalTestData.Payload(),
            token,
            DurableJournalTestData.Now));

    private static AuthorizedDurableRequest<TRequest> Authorized<TRequest>(TRequest request)
        where TRequest : class =>
        new(
            request,
            new DurableJournalKey("journal"),
            DurabilityGrantFactory.Create(DurableJournalTestData.Authorization()),
            new SecurityEnforcementIntent(
                new SecurityEnforcementIntentId(Guid.Parse("b0000000-0000-0000-0000-000000000001")),
                requiredFence: null));
}
