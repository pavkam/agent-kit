// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The stable identity of one distributed execution lease granting a single
/// worker authoritative ownership of a session or durable operation.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state and is safe to share and
/// compare across threads without synchronization.
/// </para>
/// <para>
/// An execution lease identity is distinct from <see cref="SessionLeaseId"/>.
/// A session lease provides process-local coordination for the single active
/// mutating run; an execution lease is the cross-process ownership claim that
/// carries a <see cref="FencingToken"/> and can be lost to takeover while its
/// former owner is still running.
/// </para>
/// </remarks>
public readonly record struct ExecutionLeaseId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutionLeaseId"/>
    /// struct, validating that it carries a usable, non-empty identity.
    /// </summary>
    /// <param name="value">The non-empty execution lease identity.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which is the
    /// default value and never identifies an acquired lease.
    /// </exception>
    public ExecutionLeaseId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>Gets the underlying execution lease identity.</summary>
    public Guid Value { get; }

    /// <summary>
    /// Returns the lease identity in its canonical form, suitable for
    /// logging, tracing, and ownership-conflict diagnostics.
    /// </summary>
    public override string ToString() => Value.ToString();
}
