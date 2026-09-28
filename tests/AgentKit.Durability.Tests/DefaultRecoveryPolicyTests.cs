// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

public sealed class DefaultRecoveryPolicyTests
{
    [Fact]
    public void Constructor_WhenOptionsAreNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultRecoveryPolicy(null!));

        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task DecideAsync_WhenOperationIsNull_ThrowsArgumentNullException()
    {
        var policy = Policy();
        var evidence = Evidence(Descriptor(), DurableOperationState.Accepted, SideEffectCertainty.NotApplicable);

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await policy.DecideAsync(null!, evidence, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("operation");
    }

    [Fact]
    public async Task DecideAsync_WhenEvidenceIsNull_ThrowsArgumentNullException()
    {
        var policy = Policy();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await policy.DecideAsync(Descriptor(), null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("evidence");
    }

    [Fact]
    public async Task DecideAsync_WhenAlreadyCancelled_ThrowsOperationCanceledException()
    {
        var policy = Policy();
        var operation = Descriptor();
        var evidence = Evidence(operation, DurableOperationState.Accepted, SideEffectCertainty.NotApplicable);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await policy.DecideAsync(operation, evidence, cancellation.Token));
    }

    [Fact]
    public async Task DecideAsync_WhenTerminalResultRecorded_CommitsRecordedResult()
    {
        var policy = Policy();
        var operation = Descriptor();
        var token = new FencingToken(1);
        var recorded = DurableJournalTestData.Result(token, state: DurableOperationState.OutcomeReady);
        var evidence = Evidence(
            operation,
            DurableOperationState.OutcomeReady,
            SideEffectCertainty.DefinitelyPerformed,
            terminalResultRecorded: true,
            recordedResult: recorded,
            lastWriterToken: token);

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        var commit = decision.ShouldBeOfType<RecoveryCommitRecordedResult>();
        commit.Result.ShouldBe(recorded);
    }

    [Fact]
    public async Task DecideAsync_WhenTerminalResultRecordedAndEffectUnknown_StillCommitsWithoutReinvoking()
    {
        // A recorded outcome outranks certainty: the effect already reached a terminal record, so nothing may re-run.
        var policy = Policy();
        var operation = Descriptor(IdempotencyClassification.NonIdempotent);
        var token = new FencingToken(4);
        var recorded = DurableJournalTestData.Result(token, certainty: SideEffectCertainty.Unknown);
        var evidence = Evidence(
            operation,
            DurableOperationState.OutcomeReady,
            SideEffectCertainty.Unknown,
            terminalResultRecorded: true,
            recordedResult: recorded,
            lastWriterToken: token);

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<RecoveryCommitRecordedResult>();
    }

    [Fact]
    public async Task DecideAsync_WhenWaiting_RetriesAfterTheRecordedInstant()
    {
        var policy = Policy();
        var operation = Descriptor();
        var notBefore = DurableJournalTestData.Now.AddMinutes(3);
        var evidence = Evidence(
            operation,
            DurableOperationState.Waiting,
            SideEffectCertainty.Unknown,
            notBefore: notBefore,
            externalIdempotencyKey: new IdempotencyKey("external-1"));

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        var retry = decision.ShouldBeOfType<RecoveryRetryOperation>();
        retry.NotBefore.ShouldBe(notBefore);
        retry.ExternalIdempotencyKey.ShouldBe(new IdempotencyKey("external-1"));
    }

    [Fact]
    public async Task DecideAsync_WhenStartDefinitelyAbsent_StartsTheOperation()
    {
        var policy = Policy();
        var operation = Descriptor(IdempotencyClassification.NonIdempotent);
        var evidence = Evidence(
            operation,
            DurableOperationState.Accepted,
            SideEffectCertainty.DefinitelyNotPerformed,
            startDefinitelyAbsent: true);

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<RecoveryStartOperation>();
    }

    [Theory]
    [InlineData(SideEffectCertainty.NotApplicable)]
    [InlineData(SideEffectCertainty.DefinitelyNotPerformed)]
    public async Task DecideAsync_WhenEffectDidNotHappenAndOperationOnlyAccepted_StartsTheOperation(
        SideEffectCertainty certainty)
    {
        var policy = Policy();
        var operation = Descriptor(IdempotencyClassification.NonIdempotent);
        var evidence = Evidence(operation, DurableOperationState.Accepted, certainty);

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<RecoveryStartOperation>();
    }

    [Fact]
    public async Task DecideAsync_WhenEffectDidNotHappenAfterAcceptance_RetriesTheOperation()
    {
        var policy = Policy();
        var operation = Descriptor(IdempotencyClassification.NonIdempotent);
        var evidence = Evidence(
            operation,
            DurableOperationState.EffectPending,
            SideEffectCertainty.DefinitelyNotPerformed);

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<RecoveryRetryOperation>();
    }

    [Theory]
    [InlineData(SideEffectCertainty.DefinitelyPerformed)]
    [InlineData(SideEffectCertainty.PartiallyPerformed)]
    public async Task DecideAsync_WhenEffectHappenedAndAReferenceExists_ReconcilesThroughTheBackend(
        SideEffectCertainty certainty)
    {
        var policy = Policy();
        var operation = Descriptor(IdempotencyClassification.Idempotent);
        var reference = Reference();
        var evidence = Evidence(
            operation,
            DurableOperationState.EffectPending,
            certainty,
            externalReference: reference);

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        var reconcile = decision.ShouldBeOfType<RecoveryReconcileOperation>();
        reconcile.Reference.ShouldBe(reference);
    }

    [Theory]
    [InlineData(IdempotencyClassification.ReadOnly)]
    [InlineData(IdempotencyClassification.Idempotent)]
    public async Task DecideAsync_WhenEffectHappenedAndRepeatingItIsSafe_RetriesTheOperation(
        IdempotencyClassification classification)
    {
        var policy = Policy();
        var operation = Descriptor(classification);
        var evidence = Evidence(
            operation,
            DurableOperationState.EffectPending,
            SideEffectCertainty.DefinitelyPerformed);

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<RecoveryRetryOperation>();
    }

    [Fact]
    public async Task DecideAsync_WhenEffectHappenedAndTheReceiverKeyIsKnown_RetriesWithThatKey()
    {
        var policy = Policy();
        var operation = Descriptor(IdempotencyClassification.IdempotentWithKey);
        var evidence = Evidence(
            operation,
            DurableOperationState.EffectPending,
            SideEffectCertainty.DefinitelyPerformed,
            externalIdempotencyKey: new IdempotencyKey("receiver-1"));

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        var retry = decision.ShouldBeOfType<RecoveryRetryOperation>();
        retry.ExternalIdempotencyKey.ShouldBe(new IdempotencyKey("receiver-1"));
    }

    [Theory]
    [InlineData(IdempotencyClassification.IdempotentWithKey)]
    [InlineData(IdempotencyClassification.NonIdempotent)]
    public async Task DecideAsync_WhenEffectHappenedAndNothingMakesRepetitionSafe_RequiresOperator(
        IdempotencyClassification classification)
    {
        var policy = Policy();
        var operation = Descriptor(classification);
        var evidence = Evidence(
            operation,
            DurableOperationState.EffectPending,
            SideEffectCertainty.DefinitelyPerformed);

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<RecoveryRequiresOperator>();
    }

    [Theory]
    [InlineData(IdempotencyClassification.ReadOnly)]
    [InlineData(IdempotencyClassification.Idempotent)]
    public async Task DecideAsync_WhenOutcomeUnknownAndRepetitionIsSafe_RetriesTheOperation(
        IdempotencyClassification classification)
    {
        var policy = Policy();
        var operation = Descriptor(classification);
        var evidence = Evidence(operation, DurableOperationState.EffectPending, SideEffectCertainty.Unknown);

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<RecoveryRetryOperation>();
    }

    [Fact]
    public async Task DecideAsync_WhenOutcomeUnknownAndTheReceiverKeyIsKnown_RetriesWithThatKey()
    {
        var policy = Policy();
        var operation = Descriptor(IdempotencyClassification.IdempotentWithKey);
        var evidence = Evidence(
            operation,
            DurableOperationState.EffectPending,
            SideEffectCertainty.Unknown,
            externalIdempotencyKey: new IdempotencyKey("receiver-2"));

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        var retry = decision.ShouldBeOfType<RecoveryRetryOperation>();
        retry.ExternalIdempotencyKey.ShouldBe(new IdempotencyKey("receiver-2"));
    }

    [Fact]
    public async Task DecideAsync_WhenOutcomeUnknownWithoutAKeyButAReferenceExists_Reconciles()
    {
        var policy = Policy();
        var operation = Descriptor(IdempotencyClassification.IdempotentWithKey);
        var reference = Reference();
        var evidence = Evidence(
            operation,
            DurableOperationState.EffectPending,
            SideEffectCertainty.Unknown,
            externalReference: reference);

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<RecoveryReconcileOperation>().Reference.ShouldBe(reference);
    }

    [Fact]
    public async Task DecideAsync_WhenOutcomeUnknownWithNeitherKeyNorReference_RequiresOperator()
    {
        var policy = Policy();
        var operation = Descriptor(IdempotencyClassification.IdempotentWithKey);
        var evidence = Evidence(operation, DurableOperationState.EffectPending, SideEffectCertainty.Unknown);

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<RecoveryRequiresOperator>();
    }

    [Fact]
    public async Task DecideAsync_WhenUnknownNonIdempotentEffect_RequiresOperatorByDefault()
    {
        var policy = Policy();
        var operation = Descriptor(IdempotencyClassification.NonIdempotent);
        var evidence = Evidence(
            operation,
            DurableOperationState.EffectPending,
            SideEffectCertainty.Unknown,
            externalReference: Reference());

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<RecoveryRequiresOperator>();
    }

    [Fact]
    public async Task DecideAsync_WhenUnknownNonIdempotentEffectAndReconciliationIsEnabled_Reconciles()
    {
        var policy = Policy(UnknownEffectRecoveryMode.ReconcileWhenSupported);
        var operation = Descriptor(IdempotencyClassification.NonIdempotent);
        var reference = Reference();
        var evidence = Evidence(
            operation,
            DurableOperationState.EffectPending,
            SideEffectCertainty.Unknown,
            externalReference: reference);

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<RecoveryReconcileOperation>().Reference.ShouldBe(reference);
    }

    [Fact]
    public async Task DecideAsync_WhenUnknownNonIdempotentEffectIsEnabledButNoReferenceExists_RequiresOperator()
    {
        // Reconciliation needs something to reconcile against. Enabling the mode never makes a retry safe on its own.
        var policy = Policy(UnknownEffectRecoveryMode.ReconcileWhenSupported);
        var operation = Descriptor(IdempotencyClassification.NonIdempotent);
        var evidence = Evidence(operation, DurableOperationState.EffectPending, SideEffectCertainty.Unknown);

        var decision = await policy.DecideAsync(operation, evidence, TestContext.Current.CancellationToken);

        _ = decision.ShouldBeOfType<RecoveryRequiresOperator>();
    }

    private static DefaultRecoveryPolicy Policy(
        UnknownEffectRecoveryMode mode = UnknownEffectRecoveryMode.RequireOperator) =>
        new(Options.Create(new AgentDurabilityOptions { UnknownEffectMode = mode }));

    private static RecoverableOperationDescriptor Descriptor(
        IdempotencyClassification classification = IdempotencyClassification.NonIdempotent) =>
        DurableJournalTestData.Descriptor() with { Idempotency = classification };

    private static ExternalOperationReference Reference() =>
        new(new DurableBackendKey("backend"), "external-handle");

    private static RecoveryEvidence Evidence(
        RecoverableOperationDescriptor operation,
        DurableOperationState state,
        SideEffectCertainty certainty,
        bool startDefinitelyAbsent = false,
        bool terminalResultRecorded = false,
        DurableOperationResult? recordedResult = null,
        DateTimeOffset? notBefore = null,
        ExternalOperationReference? externalReference = null,
        IdempotencyKey? externalIdempotencyKey = null,
        FencingToken? lastWriterToken = null) =>
        new(
            operation.Binding,
            state,
            certainty,
            startDefinitelyAbsent,
            terminalResultRecorded,
            recordedResult,
            notBefore,
            latestCheckpoint: null,
            externalReference,
            externalIdempotencyKey,
            lastWriterToken ?? new FencingToken(1),
            operation);
}
