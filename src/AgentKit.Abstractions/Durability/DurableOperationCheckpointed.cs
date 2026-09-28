// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Observes that one semantic-boundary state snapshot committed for a running operation.</summary>
/// <remarks>
/// Published only after <see cref="IDurableOperationJournal.RecordCheckpointAsync"/> returns
/// <see cref="DurableRecorded"/>. The event carries checkpoint identity and kind but never the serialized payload,
/// because checkpoint state is content and is not exported through observation.
/// </remarks>
public sealed record DurableOperationCheckpointed: DurableExecutionEvent
{
    /// <summary>Initializes a checkpoint observation.</summary>
    /// <param name="binding">The non-null address and captured execution context of the checkpointed operation.</param>
    /// <param name="occurredAt">The injected-clock instant at which the checkpoint committed.</param>
    /// <param name="checkpointId">The generated identity of the committed checkpoint.</param>
    /// <param name="kind">The semantic boundary the checkpoint represents.</param>
    /// <param name="fencingToken">The ownership generation that committed the checkpoint.</param>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="checkpointId"/> or <paramref name="fencingToken"/> is the default value, or
    /// <paramref name="kind"/> is not a defined member.
    /// </exception>
    public DurableOperationCheckpointed(
        DurableOperationBinding binding,
        DateTimeOffset occurredAt,
        CheckpointId checkpointId,
        DurableCheckpointKind kind,
        FencingToken fencingToken)
        : base(binding, occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(checkpointId, default, nameof(checkpointId));
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfEqual(fencingToken, default, nameof(fencingToken));
        CheckpointId = checkpointId;
        Kind = kind;
        FencingToken = fencingToken;
    }

    /// <summary>Gets the committed checkpoint identity.</summary>
    /// <value>The non-default generated identity of the snapshot that replaced the operation's recorded state.</value>
    public CheckpointId CheckpointId { get; }

    /// <summary>Gets the semantic boundary the checkpoint marks.</summary>
    /// <value>A defined <see cref="DurableCheckpointKind"/> member.</value>
    public DurableCheckpointKind Kind { get; }

    /// <summary>Gets the ownership generation that committed the checkpoint.</summary>
    /// <value>The non-default fencing token presented by the writing worker.</value>
    public FencingToken FencingToken { get; }
}
