// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>The coordinator-owned mid-operation writer handed to one handler invocation.</summary>
/// <remarks>
/// <para>
/// The writer closes over the attempt's activated runtime, its fenced journal, and its execution lease. A handler
/// therefore supplies only what it actually knows — the boundary kind and state, or the wait condition — while
/// identity, the ownership generation, the captured authorization, and checkpoint-identity allocation stay with the
/// coordinator that owns them.
/// </para>
/// <para>
/// The writer is revoked as soon as the invocation it was created for returns. A handler that captured it into
/// background work writes under a lease the coordinator may already have released, so a revoked writer refuses
/// instead of committing a record the operation no longer owns.
/// </para>
/// <para>
/// Instances are safe for concurrent use within one invocation. Writes are not ordered relative to each other; a
/// checkpoint replaces the operation's complete recorded state, so a handler that needs ordering awaits each write.
/// </para>
/// </remarks>
internal sealed class CoordinatorCheckpointWriter: IDurableCheckpointWriter
{
    private readonly DurableExecutionCoordinator _coordinator;
    private readonly IDurabilityRuntimeLease _runtime;
    private readonly FencedDurableOperationJournal _journal;
    private readonly RecoverableOperationDescriptor _operation;
    private readonly IExecutionLease _lease;
    private volatile bool _revoked;

    /// <summary>Initializes a writer bound to one operation attempt.</summary>
    /// <param name="coordinator">The owning coordinator that performs authorization and observation.</param>
    /// <param name="runtime">The activated runtime lease naming the captured journal and authority.</param>
    /// <param name="journal">The fenced journal decorator that enforces this attempt's ownership generation.</param>
    /// <param name="operation">The accepted declaration whose records this writer commits.</param>
    /// <param name="lease">The active execution lease presenting the fencing generation.</param>
    internal CoordinatorCheckpointWriter(
        DurableExecutionCoordinator coordinator,
        IDurabilityRuntimeLease runtime,
        FencedDurableOperationJournal journal,
        RecoverableOperationDescriptor operation,
        IExecutionLease lease)
    {
        Debug.Assert(coordinator is not null, "The owning coordinator is required.");
        Debug.Assert(runtime is not null, "An activated runtime lease is required.");
        Debug.Assert(journal is not null, "A fenced journal is required.");
        Debug.Assert(operation is not null, "The accepted declaration is required.");
        Debug.Assert(lease is not null, "An acquired execution lease is required.");
        _coordinator = coordinator;
        _runtime = runtime;
        _journal = journal;
        _operation = operation;
        _lease = lease;
    }

    /// <inheritdoc/>
    /// <value>The accepted declaration's exact binding, which a handler may compare against its own descriptor.</value>
    public DurableOperationBinding Binding => _operation.Binding;

    /// <inheritdoc/>
    /// <value>The active lease's ownership generation, which every write from this writer carries.</value>
    public FencingToken FencingToken => _lease.FencingToken;

    /// <inheritdoc/>
    public async ValueTask<DurableRecordResult> RecordCheckpointAsync(
        DurableCheckpointKind kind,
        OperationPayload state,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        if (_revoked)
        {
            return Revoked();
        }

        var recordedAt = _coordinator.Now;
        var checkpoint = new DurableCheckpoint(
            _coordinator.NextCheckpointId(), Binding, kind, state, _lease.FencingToken, recordedAt);
        var result = await _coordinator.WriteForHandlerAsync(
            DurableCoordinatorStage.Checkpoint,
            _runtime,
            _operation.Address,
            checkpoint,
            DurableJournalSecurityBinding.Fingerprint(checkpoint),
            SecurityEffect.Append,
            _lease.FencingToken,
            _journal.RecordCheckpointAsync,
            cancellationToken).ConfigureAwait(false);
        if (result is DurableRecorded)
        {
            await _coordinator.PublishForHandlerAsync(
                _runtime.Context,
                new DurableOperationCheckpointed(
                    Binding, recordedAt, checkpoint.Id, kind, _lease.FencingToken),
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<DurableRecordResult> RecordWaitingAsync(
        DurableWaitCondition condition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);
        cancellationToken.ThrowIfCancellationRequested();
        if (_revoked)
        {
            return Revoked();
        }

        var recordedAt = _coordinator.Now;
        var waiting = new DurableOperationWaiting(
            Binding,
            _lease.FencingToken,
            recordedAt,
            condition.SideEffectCertainty,
            condition.ExternalReference,
            condition.ExternalIdempotencyKey,
            condition.NotBefore);
        var result = await _coordinator.WriteForHandlerAsync(
            DurableCoordinatorStage.RecordWaiting,
            _runtime,
            _operation.Address,
            waiting,
            DurableJournalSecurityBinding.Fingerprint(waiting),
            SecurityEffect.Mutate,
            _lease.FencingToken,
            _journal.RecordWaitingAsync,
            cancellationToken).ConfigureAwait(false);
        if (result is DurableRecorded)
        {
            await _coordinator.PublishForHandlerAsync(
                _runtime.Context,
                new DurableOperationDeferred(
                    Binding,
                    recordedAt,
                    condition.SideEffectCertainty,
                    _lease.FencingToken,
                    condition.NotBefore,
                    condition.ExternalReference),
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Revokes the writer so no record can be committed after its invocation returned.</summary>
    /// <remarks>Revocation is idempotent and is applied whether the invocation completed, faulted, or was cancelled.</remarks>
    internal void Revoke() => _revoked = true;

    private static DurableRecordFailed Revoked() => new(
        "This durable checkpoint writer belongs to an invocation that already returned and can no longer commit records.",
        committed: false);
}
