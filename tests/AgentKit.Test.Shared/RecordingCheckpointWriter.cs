// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>A coordinator-shaped checkpoint writer that records every mid-operation write it accepts.</summary>
/// <param name="operation">The non-null declaration whose binding the writer is bound to.</param>
/// <param name="fencingToken">The ownership generation every recorded write reports.</param>
/// <param name="refusal">
/// The refusal every write returns instead of being recorded, or <see langword="null"/> to accept and record writes.
/// </param>
/// <remarks>
/// Unlike <see cref="StubCheckpointWriter"/>, which discards writes, this fake lets a test assert exactly which
/// boundary kinds and wait conditions a component committed. Writes are appended in call order under a lock, so the
/// recorded lists are safe to read once the awaited operation has completed.
/// </remarks>
public sealed class RecordingCheckpointWriter(
    RecoverableOperationDescriptor operation,
    FencingToken fencingToken,
    DurableRecordResult? refusal = null): IDurableCheckpointWriter
{
    private readonly Lock _gate = new();
    private readonly List<DurableCheckpointKind> _checkpoints = [];
    private readonly List<DurableWaitCondition> _waits = [];

    /// <inheritdoc/>
    public DurableOperationBinding Binding => operation.Binding;

    /// <inheritdoc/>
    public FencingToken FencingToken => fencingToken;

    /// <summary>Gets the checkpoint kinds committed so far.</summary>
    /// <value>A snapshot of the kinds in call order.</value>
    public IReadOnlyList<DurableCheckpointKind> Checkpoints
    {
        get
        {
            lock (_gate)
            {
                return [.. _checkpoints];
            }
        }
    }

    /// <summary>Gets the wait conditions committed so far.</summary>
    /// <value>A snapshot of the conditions in call order.</value>
    public IReadOnlyList<DurableWaitCondition> Waits
    {
        get
        {
            lock (_gate)
            {
                return [.. _waits];
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordCheckpointAsync(
        DurableCheckpointKind kind,
        OperationPayload state,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        if (refusal is not null)
        {
            return ValueTask.FromResult(refusal);
        }

        lock (_gate)
        {
            _checkpoints.Add(kind);
        }

        return ValueTask.FromResult<DurableRecordResult>(new DurableRecorded(fencingToken, DateTimeOffset.UnixEpoch));
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordWaitingAsync(
        DurableWaitCondition condition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);
        cancellationToken.ThrowIfCancellationRequested();
        if (refusal is not null)
        {
            return ValueTask.FromResult(refusal);
        }

        lock (_gate)
        {
            _waits.Add(condition);
        }

        return ValueTask.FromResult<DurableRecordResult>(new DurableRecorded(fencingToken, DateTimeOffset.UnixEpoch));
    }
}
