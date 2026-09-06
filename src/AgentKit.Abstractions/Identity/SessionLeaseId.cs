// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one acquired <see cref="ISessionRunLease"/>, proving
/// process-local ownership of the single active mutating run for a session.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>, safe to share across threads without
/// synchronization. This constructor rejects <see cref="Guid.Empty"/> so a
/// valid instance always addresses a real lease.
/// </remarks>
public readonly record struct SessionLeaseId
{
    /// <summary>Initializes a new instance of the <see cref="SessionLeaseId"/> struct.</summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>.
    /// </exception>
    public SessionLeaseId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>Gets the underlying globally unique identifier.</summary>
    public Guid Value { get; }

    /// <summary>Returns the canonical text form of this identity.</summary>
    public override string ToString() => Value.ToString("D");
}
