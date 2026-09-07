// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The acceptance record committed before a recoverable operation performs
/// any effect, binding its immutable declaration to one complete initial
/// state.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// Acceptance and execution ownership are deliberately separate. Committing a
/// start record begins no effect; it only makes the operation discoverable to
/// a future recovering worker. Process loss between acceptance and the first
/// drive therefore leaves work that can be started exactly once rather than
/// work nobody knows about.
/// </para>
/// </remarks>
public sealed record DurableOperationStart
{
    private readonly RecoverableOperationDescriptor _descriptor;
    private readonly OperationPayload _initialState;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="DurableOperationStart"/> record.
    /// </summary>
    /// <param name="descriptor">
    /// The complete immutable declaration of the operation.
    /// </param>
    /// <param name="initialState">
    /// The complete initial state a recovering worker dispatches from.
    /// </param>
    /// <param name="fencingToken">
    /// The ownership generation of the lease committing this record.
    /// </param>
    /// <param name="acceptedAt">
    /// The instant acceptance was committed, from the injected
    /// <see cref="TimeProvider"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="descriptor"/> or <paramref name="initialState"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="fencingToken"/> is the default, unallocated token.
    /// </exception>
    public DurableOperationStart(
        RecoverableOperationDescriptor descriptor,
        OperationPayload initialState,
        FencingToken fencingToken,
        DateTimeOffset acceptedAt)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentOutOfRangeException.ThrowIfEqual(fencingToken, default, nameof(fencingToken));

        _descriptor = descriptor;
        _initialState = initialState;
        FencingToken = fencingToken;
        AcceptedAt = acceptedAt;
    }

    /// <summary>Gets the complete immutable declaration of the operation.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public RecoverableOperationDescriptor Descriptor
    {
        get => _descriptor;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Descriptor));
            _descriptor = value;
        }
    }

    /// <summary>Gets the complete initial state.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public OperationPayload InitialState
    {
        get => _initialState;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(InitialState));
            _initialState = value;
        }
    }

    /// <summary>
    /// Gets the ownership generation of the lease committing this record.
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

    /// <summary>Gets the instant acceptance was committed.</summary>
    public DateTimeOffset AcceptedAt { get; init; }
}
