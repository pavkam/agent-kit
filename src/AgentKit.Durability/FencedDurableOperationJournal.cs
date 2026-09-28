// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Decorates a journal so every write must present the active execution lease's fencing token.</summary>
/// <remarks>
/// <para>
/// The decorator guarantees that every write leaving this attempt presents the generation of the lease the attempt
/// actually holds. A write presenting an older generation returns <see cref="DurableRecordFenced"/>; a write
/// presenting a newer or unallocated generation is a caller error and returns <see cref="DurableRecordFailed"/> with
/// <c>Committed</c> proven <see langword="false"/>. Neither refusal reaches the store, so neither can leave a partial
/// record. The journal store remains authoritative for cross-worker takeover, because only it can compare a presented
/// generation against the one currently recorded.
/// </para>
/// <para>
/// Evidence loading is a read and carries no fence. The decorator borrows the lease rather than owning it, so it is
/// not disposable and never releases ownership. Refusing a write also proves nothing about an external effect that may
/// already be running.
/// </para>
/// </remarks>
public sealed class FencedDurableOperationJournal: IDurableOperationJournal
{
    private readonly IDurableOperationJournal _inner;
    private readonly IExecutionLease _lease;

    /// <summary>Wraps one journal so writes are checked against a held execution lease.</summary>
    /// <param name="inner">The non-null journal that performs the authoritative write.</param>
    /// <param name="lease">The non-null, borrowed execution lease whose fencing token every write must present.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> or <paramref name="lease"/> is null.</exception>
    public FencedDurableOperationJournal(IDurableOperationJournal inner, IExecutionLease lease)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(lease);
        _inner = inner;
        _lease = lease;
    }

    /// <inheritdoc/>
    /// <remarks>The decorator adds no enforcement identity of its own; it reports the decorated journal's audience.</remarks>
    public ComponentId SecurityAudience => _inner.SecurityAudience;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="start"/> is null.</exception>
    public async ValueTask<DurableRecordResult> RecordStartAsync(
        AuthorizedDurableRequest<DurableOperationStart> start,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);
        return CheckFence(start.Request.FencingToken) is { } refusal
            ? refusal
            : await _inner.RecordStartAsync(start, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="checkpoint"/> is null.</exception>
    public async ValueTask<DurableRecordResult> RecordCheckpointAsync(
        AuthorizedDurableRequest<DurableCheckpoint> checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        return CheckFence(checkpoint.Request.FencingToken) is { } refusal
            ? refusal
            : await _inner.RecordCheckpointAsync(checkpoint, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public async ValueTask<DurableRecordResult> RecordTerminalAsync(
        AuthorizedDurableRequest<DurableOperationResult> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return CheckFence(result.Request.FencingToken) is { } refusal
            ? refusal
            : await _inner.RecordTerminalAsync(result, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="waiting"/> is null.</exception>
    public async ValueTask<DurableRecordResult> RecordWaitingAsync(
        AuthorizedDurableRequest<DurableOperationWaiting> waiting,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(waiting);
        return CheckFence(waiting.Request.FencingToken) is { } refusal
            ? refusal
            : await _inner.RecordWaitingAsync(waiting, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>Evidence loading presents no fencing token, so the decorator forwards the read unchanged.</remarks>
    public ValueTask<RecoveryEvidenceResult> LoadEvidenceAsync(
        AuthorizedDurableRequest<DurableOperationAddress> address,
        CancellationToken cancellationToken = default) =>
        _inner.LoadEvidenceAsync(address, cancellationToken);

    private DurableRecordResult? CheckFence(FencingToken presented)
    {
        var held = _lease.FencingToken;
        return presented == held
            ? null
            : presented != default && presented.Value < held.Value
                ? new DurableRecordFenced(presented, held)
                : new DurableRecordFailed(
                    "The presented fencing token is not the active execution lease generation.",
                    committed: false);
    }
}
