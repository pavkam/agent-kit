// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Ownership was granted, and the caller now holds the returned lease.
/// </summary>
/// <remarks>
/// The caller owns the returned <see cref="IExecutionLease"/> and must
/// dispose it exactly once, normally with <see langword="await using"/>
/// spanning the durable work. Disposal releases ownership so another worker
/// can take over promptly instead of waiting for expiry.
/// </remarks>
public sealed record ExecutionLeaseAcquired: ExecutionLeaseResult
{
    private readonly IExecutionLease _lease;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ExecutionLeaseAcquired"/> record.
    /// </summary>
    /// <param name="lease">
    /// The acquired lease, transferred to the caller's ownership.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="lease"/> is <see langword="null"/>.
    /// </exception>
    public ExecutionLeaseAcquired(IExecutionLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        _lease = lease;
    }

    /// <summary>Gets the acquired lease.</summary>
    /// <value>
    /// A caller-owned resource. The lease is not disposed by the result
    /// record, and the container never owns it.
    /// </value>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public IExecutionLease Lease
    {
        get => _lease;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Lease));
            _lease = value;
        }
    }
}
