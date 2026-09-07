// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One durably recorded snapshot of a recoverable operation's complete state
/// at a semantic boundary, written under the fencing token of the lease that
/// produced it.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// A checkpoint replaces the complete current state rather than appending a
/// mutation to be folded later. Recovery dispatches from that total state, so
/// it never has to infer progress from an absent auxiliary value or replay a
/// journal of deltas.
/// </para>
/// <para>
/// The <see cref="FencingToken"/> recorded here is what makes takeover safe.
/// A store rejects a checkpoint whose token is older than the current
/// authoritative ownership generation, so a stale worker that resumes after a
/// pause cannot overwrite the state of the worker that replaced it.
/// </para>
/// </remarks>
public sealed record DurableCheckpoint
{
    private readonly DurableOperationAddress _address;
    private readonly DurableExecutionContext _executionContext;
    private readonly OperationPayload _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="DurableCheckpoint"/>
    /// record.
    /// </summary>
    /// <param name="id">The checkpoint's stable identity.</param>
    /// <param name="address">The operation these coordinates belong to.</param>
    /// <param name="executionContext">
    /// The captured durability composition the operation runs under.
    /// </param>
    /// <param name="kind">The semantic boundary this checkpoint marks.</param>
    /// <param name="state">The complete serialized operation state.</param>
    /// <param name="fencingToken">
    /// The ownership generation of the lease under which this checkpoint is
    /// written.
    /// </param>
    /// <param name="recordedAt">
    /// The instant the checkpoint was produced, from the injected
    /// <see cref="TimeProvider"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/>, <paramref name="executionContext"/>, or
    /// <paramref name="state"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="id"/> or <paramref name="fencingToken"/> is its
    /// default, unallocated value, or <paramref name="kind"/> is not a
    /// defined enumeration value.
    /// </exception>
    public DurableCheckpoint(
        CheckpointId id,
        DurableOperationAddress address,
        DurableExecutionContext executionContext,
        DurableCheckpointKind kind,
        OperationPayload state,
        FencingToken fencingToken,
        DateTimeOffset recordedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(executionContext);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfEqual(fencingToken, default, nameof(fencingToken));

        Id = id;
        _address = address;
        _executionContext = executionContext;
        Kind = kind;
        _state = state;
        FencingToken = fencingToken;
        RecordedAt = recordedAt;
    }

    /// <summary>Gets the checkpoint's stable identity.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, empty identity.
    /// </exception>
    public CheckpointId Id
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(Id));
            field = value;
        }
    }

    /// <summary>Gets the operation these coordinates belong to.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public DurableOperationAddress Address
    {
        get => _address;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Address));
            _address = value;
        }
    }

    /// <summary>Gets the captured durability composition.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public DurableExecutionContext ExecutionContext
    {
        get => _executionContext;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(ExecutionContext));
            _executionContext = value;
        }
    }

    /// <summary>Gets the semantic boundary this checkpoint marks.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set an undefined enumeration value.
    /// </exception>
    public DurableCheckpointKind Kind
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(Kind));
            field = value;
        }
    }

    /// <summary>Gets the complete serialized operation state.</summary>
    /// <value>
    /// The total state required to resume, not a delta against a previous
    /// checkpoint.
    /// </value>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public OperationPayload State
    {
        get => _state;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(State));
            _state = value;
        }
    }

    /// <summary>
    /// Gets the ownership generation under which this checkpoint was written.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, unallocated token.
    /// </exception>
    public FencingToken FencingToken
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(FencingToken));
            field = value;
        }
    }

    /// <summary>Gets the instant the checkpoint was produced.</summary>
    /// <value>
    /// An instant read from the injected <see cref="TimeProvider"/>, never an
    /// ambient clock, so recovery ordering is deterministic in tests.
    /// </value>
    public DateTimeOffset RecordedAt { get; init; }
}
