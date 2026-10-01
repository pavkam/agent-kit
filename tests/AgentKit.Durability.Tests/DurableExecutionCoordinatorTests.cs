// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

public sealed class DurableExecutionCoordinatorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOperationIsNull_ThrowsArgumentNullException()
    {
        using var harness = new DurabilityRuntimeHarness();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await harness.Coordinator.ExecuteAsync(null!, hooks: null, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("operation");
    }

    [Fact]
    public async Task RecoverAsync_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        using var harness = new DurabilityRuntimeHarness();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await harness.Coordinator.RecoverAsync(
                null!, DurabilityRuntimeHarness.Context(), hooks: null, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public async Task RecoverAsync_WhenContextIsNull_ThrowsArgumentNullException()
    {
        using var harness = new DurabilityRuntimeHarness();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await harness.Coordinator.RecoverAsync(
                DurableJournalTestData.Address(), null!, hooks: null, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyCancelled_ThrowsBeforeAnyDurableWrite()
    {
        using var harness = new DurabilityRuntimeHarness();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await harness.Coordinator.ExecuteAsync(
                DurableJournalTestData.Descriptor(), hooks: null, cancellation.Token));

        harness.Journal.Calls.ShouldBeEmpty();
        harness.Handler!.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheOperationSucceeds_CommitsAcceptanceBeforeInvokingTheEffect()
    {
        using var harness = new DurabilityRuntimeHarness();
        var invocationOrder = new List<string>();
        var handler = new RecordingDurableOperationHandler
        {
            DuringInvocation = () =>
            {
                invocationOrder.Add("invoke");
                return ValueTask.CompletedTask;
            },
        };
        using var composed = new DurabilityRuntimeHarness(handlers: [handler]);

        var result = await composed.Coordinator.ExecuteAsync(
            DurableJournalTestData.Descriptor(), hooks: null, TestContext.Current.CancellationToken);

        result.State.ShouldBe(DurableOperationState.Completed);
        composed.Journal.Calls.ShouldBe(["RecordStart", "RecordTerminal"]);
        handler.Invocations.ShouldBe(1);
        invocationOrder.ShouldBe(["invoke"]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenWriting_PresentsTheAcquiredGenerationOnEveryRecord()
    {
        using var harness = new DurabilityRuntimeHarness();

        _ = await harness.Coordinator.ExecuteAsync(
            DurableJournalTestData.Descriptor(), hooks: null, TestContext.Current.CancellationToken);

        var acquired = harness.Journal.Starts.Single().Request.FencingToken;
        acquired.ShouldNotBe(default);
        harness.Journal.Terminals.Single().Request.FencingToken.ShouldBe(acquired);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthorizing_RequestsTheExactAudienceKindEffectAndResourceTheJournalRecomputes()
    {
        using var harness = new DurabilityRuntimeHarness();
        var operation = DurableJournalTestData.Descriptor();

        _ = await harness.Coordinator.ExecuteAsync(operation, hooks: null, TestContext.Current.CancellationToken);

        var start = harness.Authority.Requests[0];
        start.Audience.ShouldBe(harness.Journal.SecurityAudience);
        start.Kind.ShouldBe(SecurityOperationKind.StateMutation);
        start.Effect.ShouldBe(SecurityEffect.Create);
        start.Resources.ShouldBe([
            DurableJournalSecurityBinding.Resource(DurabilityRuntimeHarness.JournalKey, operation.Address)]);
        start.Identity.ShouldBe(operation.ExecutionContext.Authorization.Identity);
        harness.Authority.Requests[1].Effect.ShouldBe(SecurityEffect.Append);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheAuthorityDenies_RefusesBeforeTheJournalIsTouched()
    {
        using var harness = new DurabilityRuntimeHarness();
        harness.Authority.DenyReason = "The test authority denied this durable write.";

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await harness.Coordinator.ExecuteAsync(
                DurableJournalTestData.Descriptor(), hooks: null, TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("The test authority denied this durable write.");
        harness.Journal.Calls.ShouldBeEmpty();
        harness.Handler!.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoHandlerOwnsTheOperationName_RefusesBeforeAcquiringOwnership()
    {
        using var harness = new DurabilityRuntimeHarness(
            handlers: [new RecordingDurableOperationHandler(new DurableOperationName("other.operation"))]);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await harness.Coordinator.ExecuteAsync(
                DurableJournalTestData.Descriptor(), hooks: null, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("No durable operation handler");
        harness.Journal.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAcceptanceIsRefusedByANewerGeneration_NeverInvokesTheEffect()
    {
        // Acceptance is committed before any effect precisely so a fenced refusal can stop the effect entirely.
        using var harness = new DurabilityRuntimeHarness();
        harness.Journal.WriteResult = new DurableRecordFenced(new FencingToken(1), new FencingToken(9));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await harness.Coordinator.ExecuteAsync(
                DurableJournalTestData.Descriptor(), hooks: null, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("newer ownership generation");
        harness.Journal.Calls.ShouldBe(["RecordStart"]);
        harness.Handler!.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheLeaseIsLostMidOperation_StillCommitsTheTerminalRecordThroughTheJournal()
    {
        // Losing renewal does not interrupt an effect that may already be running. Only the durable write is allowed
        // to fail closed, because only the store can prove which generation currently owns the operation.
        using var harness = new DurabilityRuntimeHarness();
        var handler = new RecordingDurableOperationHandler();
        using var composed = new DurabilityRuntimeHarness(handlers: [handler]);
        handler.DuringInvocation = () =>
        {
            composed.TimeProvider.Advance(TimeSpan.FromMinutes(5));
            return ValueTask.CompletedTask;
        };

        _ = await composed.Coordinator.ExecuteAsync(
            DurableJournalTestData.Descriptor(), hooks: null, TestContext.Current.CancellationToken);

        handler.Invocations.ShouldBe(1);
        composed.Journal.Calls.ShouldBe(["RecordStart", "RecordTerminal"]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheEffectFaults_PropagatesWithoutCommittingATerminalRecord()
    {
        var handler = new RecordingDurableOperationHandler
        {
            Failure = new TimeoutException("The effect could not be completed."),
        };
        using var harness = new DurabilityRuntimeHarness(handlers: [handler]);

        _ = await Should.ThrowAsync<TimeoutException>(
            async () => await harness.Coordinator.ExecuteAsync(
                DurableJournalTestData.Descriptor(), hooks: null, TestContext.Current.CancellationToken));

        // The acceptance record survives: a lost outcome is exactly what recovery must be able to discover.
        harness.Journal.Calls.ShouldBe(["RecordStart"]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheBackendPerformsNoHandoff_NeverDispatchesOrRecordsAWait()
    {
        using var harness = new DurabilityRuntimeHarness();

        _ = await harness.Coordinator.ExecuteAsync(
            DurableJournalTestData.Descriptor(), hooks: null, TestContext.Current.CancellationToken);

        harness.Journal.Calls.ShouldNotContain("RecordWaiting");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheBackendHandsOffExternally_RecordsTheReferenceBeforeWaiting()
    {
        var reference = new ExternalOperationReference(DurabilityRuntimeHarness.BackendKey, "external-1");
        using var harness = new DurabilityRuntimeHarness(
            backend: new HandoffDurableExecutionBackend(DurabilityRuntimeHarness.BackendKey, reference));

        _ = await harness.Coordinator.ExecuteAsync(
            DurableJournalTestData.Descriptor(), hooks: null, TestContext.Current.CancellationToken);

        harness.Journal.Calls.ShouldBe(["RecordStart", "RecordWaiting", "RecordTerminal"]);
        var waiting = harness.Journal.Waits.Single().Request;
        waiting.ExternalReference.ShouldBe(reference);
        waiting.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheBackendRefusesAHandoff_NeverInvokesTheEffect()
    {
        using var harness = new DurabilityRuntimeHarness(
            backend: new HandoffDurableExecutionBackend(
                DurabilityRuntimeHarness.BackendKey, reference: null, refuse: true));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await harness.Coordinator.ExecuteAsync(
                DurableJournalTestData.Descriptor(), hooks: null, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("handoff");
        harness.Handler!.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task RecoverAsync_WhenEvidenceIsAbsent_RefusesWithoutInvokingTheEffect()
    {
        using var harness = new DurabilityRuntimeHarness();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await harness.Coordinator.RecoverAsync(
                DurableJournalTestData.Address(),
                DurabilityRuntimeHarness.Context(),
                hooks: null,
                TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("no durable record");
        harness.Handler!.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task RecoverAsync_WhenEvidenceRetainsNoDeclaration_RefusesAsIncompatible()
    {
        using var harness = new DurabilityRuntimeHarness();
        harness.Journal.EvidenceResult = new RecoveryEvidenceLoaded(Evidence(descriptor: null));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await harness.Coordinator.RecoverAsync(
                DurableJournalTestData.Address(),
                DurabilityRuntimeHarness.Context(),
                hooks: null,
                TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("retains no accepted declaration");
        harness.Handler!.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task RecoverAsync_WhenATerminalResultWasRecorded_CommitsItWithoutReinvokingTheEffect()
    {
        using var harness = new DurabilityRuntimeHarness();
        var operation = DurableJournalTestData.Descriptor();
        var recorded = DurableJournalTestData.Result(new FencingToken(1), state: DurableOperationState.OutcomeReady);
        harness.Journal.EvidenceResult = new RecoveryEvidenceLoaded(Evidence(
            state: DurableOperationState.OutcomeReady,
            terminalResultRecorded: true,
            recordedResult: recorded,
            descriptor: operation));

        var result = await harness.Coordinator.RecoverAsync(
            operation.Address, operation.ExecutionContext, hooks: null, TestContext.Current.CancellationToken);

        harness.Handler!.Invocations.ShouldBe(0);
        result.Output.ShouldBe(recorded.Output);
        harness.Journal.Calls.ShouldBe(["LoadEvidence", "RecordTerminal"]);
    }

    [Theory]
    [InlineData(DurableOperationState.Completed)]
    [InlineData(DurableOperationState.Faulted)]
    public async Task RecoverAsync_WhenTheRecordedResultIsAlreadySettled_ReturnsItWithoutWritingOrReinvoking(
        DurableOperationState settledState)
    {
        using var harness = new DurabilityRuntimeHarness();
        var operation = DurableJournalTestData.Descriptor();
        var recorded = DurableJournalTestData.Result(new FencingToken(1), state: settledState);
        harness.Journal.EvidenceResult = new RecoveryEvidenceLoaded(Evidence(
            state: settledState,
            terminalResultRecorded: true,
            recordedResult: recorded,
            descriptor: operation));

        var result = await harness.Coordinator.RecoverAsync(
            operation.Address, operation.ExecutionContext, hooks: null, TestContext.Current.CancellationToken);

        result.ShouldBe(recorded);
        harness.Handler!.Invocations.ShouldBe(0);
        harness.Journal.Calls.ShouldBe(["LoadEvidence"]);
    }

    [Fact]
    public async Task RecoverAsync_WhenCommittingARecordedResult_RestampsItUnderTheCurrentGeneration()
    {
        using var harness = new DurabilityRuntimeHarness();
        var operation = DurableJournalTestData.Descriptor();
        var recorded = DurableJournalTestData.Result(new FencingToken(1), state: DurableOperationState.OutcomeReady);
        harness.Journal.EvidenceResult = new RecoveryEvidenceLoaded(Evidence(
            state: DurableOperationState.OutcomeReady,
            terminalResultRecorded: true,
            recordedResult: recorded,
            descriptor: operation));

        var result = await harness.Coordinator.RecoverAsync(
            operation.Address, operation.ExecutionContext, hooks: null, TestContext.Current.CancellationToken);

        var written = harness.Journal.Terminals.Single().Request;
        written.FencingToken.ShouldBe(result.FencingToken);
        written.SideEffectCertainty.ShouldBe(recorded.SideEffectCertainty);
    }

    [Fact]
    public async Task RecoverAsync_WhenTheStartIsProvenAbsent_ReplaysTheEffectExactlyOnce()
    {
        using var harness = new DurabilityRuntimeHarness();
        var operation = DurableJournalTestData.Descriptor();
        harness.Journal.EvidenceResult = new RecoveryEvidenceLoaded(Evidence(
            state: DurableOperationState.Accepted,
            certainty: SideEffectCertainty.DefinitelyNotPerformed,
            startDefinitelyAbsent: true,
            descriptor: operation));

        _ = await harness.Coordinator.RecoverAsync(
            operation.Address, operation.ExecutionContext, hooks: null, TestContext.Current.CancellationToken);

        harness.Handler!.Invocations.ShouldBe(1);
        harness.Journal.Calls.ShouldBe(["LoadEvidence", "RecordTerminal"]);
    }

    [Fact]
    public async Task RecoverAsync_WhenTheOutcomeIsUnknownForANonIdempotentEffect_RequiresOperatorWithoutReplaying()
    {
        using var harness = new DurabilityRuntimeHarness();
        var operation = DurableJournalTestData.Descriptor();
        harness.Journal.EvidenceResult = new RecoveryEvidenceLoaded(Evidence(
            state: DurableOperationState.EffectPending,
            certainty: SideEffectCertainty.Unknown,
            descriptor: operation));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await harness.Coordinator.RecoverAsync(
                operation.Address, operation.ExecutionContext, hooks: null, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("operator action");
        harness.Handler!.Invocations.ShouldBe(0);
        harness.Journal.Calls.ShouldBe(["LoadEvidence"]);
    }

    [Fact]
    public async Task RecoverAsync_WhenReconciliationIsRequiredButTheBackendCannotAnswer_RequiresOperator()
    {
        using var harness = new DurabilityRuntimeHarness();
        var operation = DurableJournalTestData.Descriptor() with
        {
            Idempotency = IdempotencyClassification.Idempotent,
        };
        harness.Journal.EvidenceResult = new RecoveryEvidenceLoaded(Evidence(
            state: DurableOperationState.EffectPending,
            certainty: SideEffectCertainty.DefinitelyPerformed,
            externalReference: new ExternalOperationReference(
                DurabilityRuntimeHarness.BackendKey, "external-1"),
            descriptor: operation));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await harness.Coordinator.RecoverAsync(
                operation.Address, operation.ExecutionContext, hooks: null, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("cannot reconcile");
        harness.Handler!.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task RecoverAsync_WhenTheBackendReconciles_CommitsTheEstablishedCertaintyWithoutReplaying()
    {
        var reference = new ExternalOperationReference(DurabilityRuntimeHarness.BackendKey, "external-1");
        using var harness = new DurabilityRuntimeHarness(
            backend: new HandoffDurableExecutionBackend(
                DurabilityRuntimeHarness.BackendKey,
                reference,
                reconciledCertainty: SideEffectCertainty.DefinitelyPerformed));
        var operation = DurableJournalTestData.Descriptor() with
        {
            Idempotency = IdempotencyClassification.Idempotent,
        };
        harness.Journal.EvidenceResult = new RecoveryEvidenceLoaded(Evidence(
            state: DurableOperationState.EffectPending,
            certainty: SideEffectCertainty.DefinitelyPerformed,
            externalReference: reference,
            descriptor: operation));

        var result = await harness.Coordinator.RecoverAsync(
            operation.Address, operation.ExecutionContext, hooks: null, TestContext.Current.CancellationToken);

        harness.Handler!.Invocations.ShouldBe(0);
        result.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyPerformed);
        result.Output.Data.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecoverAsync_WhenAlreadyCancelled_ThrowsBeforeReadingEvidence()
    {
        using var harness = new DurabilityRuntimeHarness();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await harness.Coordinator.RecoverAsync(
                DurableJournalTestData.Address(),
                DurabilityRuntimeHarness.Context(),
                hooks: null,
                cancellation.Token));

        harness.Journal.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledDuringTheEffect_StopsLocalAwaitingWithoutCommittingATerminalRecord()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new RecordingDurableOperationHandler
        {
            DuringInvocation = async () =>
            {
                await cancellation.CancelAsync();
                cancellation.Token.ThrowIfCancellationRequested();
            },
        };
        using var harness = new DurabilityRuntimeHarness(handlers: [handler]);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await harness.Coordinator.ExecuteAsync(
                DurableJournalTestData.Descriptor(), hooks: null, cancellation.Token));

        handler.Invocations.ShouldBe(1);
        harness.Journal.Calls.ShouldBe(["RecordStart"]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenObserved_StartsOneExecuteActivityWithATruthfulTerminalStatus()
    {
        using var activities = new TraceScopedActivityCollector(AgentKitActivityNames.DurableExecute);
        using var harness = new DurabilityRuntimeHarness();

        _ = await harness.Coordinator.ExecuteAsync(
            DurableJournalTestData.Descriptor(), hooks: null, TestContext.Current.CancellationToken);

        activities.Snapshot().Single().ShouldBe(ActivityStatusCode.Ok);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheOperationFails_MarksTheExecuteActivityAsError()
    {
        using var activities = new TraceScopedActivityCollector(AgentKitActivityNames.DurableExecute);
        using var harness = new DurabilityRuntimeHarness();
        harness.Authority.DenyReason = "The test authority denied this durable write.";

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await harness.Coordinator.ExecuteAsync(
                DurableJournalTestData.Descriptor(), hooks: null, TestContext.Current.CancellationToken));

        activities.Snapshot().Single().ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task RecoverAsync_WhenObserved_StartsOneRecoverActivity()
    {
        using var activities = new TraceScopedActivityCollector(AgentKitActivityNames.DurableRecover);
        using var harness = new DurabilityRuntimeHarness();
        var operation = DurableJournalTestData.Descriptor();
        harness.Journal.EvidenceResult = new RecoveryEvidenceLoaded(Evidence(
            startDefinitelyAbsent: true, descriptor: operation));

        _ = await harness.Coordinator.RecoverAsync(
            operation.Address, operation.ExecutionContext, hooks: null, TestContext.Current.CancellationToken);

        activities.Snapshot().Single().ShouldBe(ActivityStatusCode.Ok);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoListenerIsAttached_BehavesIdentically()
    {
        using var harness = new DurabilityRuntimeHarness();

        var result = await harness.Coordinator.ExecuteAsync(
            DurableJournalTestData.Descriptor(), hooks: null, TestContext.Current.CancellationToken);

        result.State.ShouldBe(DurableOperationState.Completed);
        harness.Journal.Calls.ShouldBe(["RecordStart", "RecordTerminal"]);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLogged_EmitsStageEventsUnderThePackageOwnedIdentifiers()
    {
        var logger = new RecordingLogger<DurableExecutionCoordinator>();
        using var harness = new DurabilityRuntimeHarness(logger: logger);

        _ = await harness.Coordinator.ExecuteAsync(
            DurableJournalTestData.Descriptor(), hooks: null, TestContext.Current.CancellationToken);

        var entries = logger.Snapshot();
        entries.ShouldNotBeEmpty();
        entries.ShouldAllBe(entry => entry.EventId.Id >= 26000 && entry.EventId.Id <= 26009);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLogged_NeverCarriesTheOperationPayload()
    {
        // Operation payloads are content. No stage template may carry them, whatever the log level.
        var logger = new RecordingLogger<DurableExecutionCoordinator>();
        using var harness = new DurabilityRuntimeHarness(logger: logger);
        var operation = DurableJournalTestData.Descriptor();

        _ = await harness.Coordinator.ExecuteAsync(operation, hooks: null, TestContext.Current.CancellationToken);

        var rendered = string.Join('\n', logger.Snapshot().Select(static entry => entry.Message));
        rendered.ShouldNotContain(operation.IdempotencyKey!.Value);
        rendered.ShouldNotContain(operation.Address.SessionId.Value.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheLoggerFails_LeavesTheSemanticOutcomeUnchanged()
    {
        var logger = new RecordingLogger<DurableExecutionCoordinator> { ThrowOnWrite = true };
        using var harness = new DurabilityRuntimeHarness(logger: logger);

        // Instrumentation is observational. A failing logging provider must not change what the coordinator does.
        var result = await harness.Coordinator.ExecuteAsync(
            DurableJournalTestData.Descriptor(), hooks: null, TestContext.Current.CancellationToken);

        result.State.ShouldBe(DurableOperationState.Completed);
        harness.Journal.Calls.ShouldBe(["RecordStart", "RecordTerminal"]);
    }

    [Fact]
    public void Constructor_WhenTwoHandlersClaimOneOperationName_ThrowsArgumentException()
    {
        using var harness = new DurabilityRuntimeHarness(handlers: [
            new RecordingDurableOperationHandler(),
            new RecordingDurableOperationHandler(),
        ]);

        var exception = Should.Throw<ArgumentException>(() => _ = harness.Coordinator);

        exception.ParamName.ShouldBe("handlers");
    }

    private static RecoveryEvidence Evidence(
        DurableOperationState state = DurableOperationState.Accepted,
        SideEffectCertainty certainty = SideEffectCertainty.DefinitelyNotPerformed,
        bool startDefinitelyAbsent = false,
        bool terminalResultRecorded = false,
        DurableOperationResult? recordedResult = null,
        ExternalOperationReference? externalReference = null,
        RecoverableOperationDescriptor? descriptor = null) =>
        new(
            (descriptor ?? DurableJournalTestData.Descriptor()).Binding,
            state,
            certainty,
            startDefinitelyAbsent,
            terminalResultRecorded,
            recordedResult,
            notBefore: null,
            latestCheckpoint: null,
            externalReference,
            externalIdempotencyKey: null,
            lastWriterToken: new FencingToken(1),
            descriptor);
}
