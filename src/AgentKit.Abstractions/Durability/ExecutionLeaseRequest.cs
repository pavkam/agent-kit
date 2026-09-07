// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A request for exclusive cross-process ownership of one durable operation
/// on behalf of a named worker.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// The requested duration is a lease length, not a deadline for the work. A
/// long operation renews rather than requesting a long lease, because a lease
/// that outlives a dead worker blocks recovery for exactly that long.
/// </para>
/// </remarks>
public sealed record ExecutionLeaseRequest
{
    private readonly DurableOperationAddress _address;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutionLeaseRequest"/>
    /// record.
    /// </summary>
    /// <param name="address">The operation to take ownership of.</param>
    /// <param name="workerId">The worker requesting ownership.</param>
    /// <param name="duration">
    /// The requested lease length. The lease manager may grant a shorter
    /// duration but never a longer one.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="workerId"/> is its default, empty identity, or
    /// <paramref name="duration"/> is zero or negative. A non-positive lease
    /// is expired the instant it is granted.
    /// </exception>
    public ExecutionLeaseRequest(
        DurableOperationAddress address,
        WorkerId workerId,
        TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentOutOfRangeException.ThrowIfEqual(workerId, default, nameof(workerId));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero, nameof(duration));

        _address = address;
        WorkerId = workerId;
        Duration = duration;
    }

    /// <summary>Gets the operation to take ownership of.</summary>
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

    /// <summary>Gets the worker requesting ownership.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, empty identity.
    /// </exception>
    public WorkerId WorkerId
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(WorkerId));
            field = value;
        }
    }

    /// <summary>Gets the requested lease length.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set a zero or negative duration.
    /// </exception>
    public TimeSpan Duration
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero, nameof(Duration));
            field = value;
        }
    }
}
