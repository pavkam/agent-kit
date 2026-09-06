// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The requesting run acquired exclusive mutating access to the session.</summary>
public sealed record SessionRunLeaseAcquired: SessionRunLeaseResult
{
    /// <summary>Initializes a new instance of the <see cref="SessionRunLeaseAcquired"/> record.</summary>
    /// <param name="lease">The acquired lease. The caller owns and must dispose it exactly once.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lease"/> is null.</exception>
    public SessionRunLeaseAcquired(ISessionRunLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        Lease = lease;
    }

    /// <summary>Gets the acquired lease. The caller owns and must dispose it exactly once.</summary>
    public ISessionRunLease Lease { get; init; }
}
