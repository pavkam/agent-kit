// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The stable identity of one worker process or node that can acquire
/// execution leases and perform durable operations.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>. It carries no mutable state and is safe to share and
/// compare across threads without synchronization.
/// </para>
/// <para>
/// The worker identity is supplied by the host composition rather than
/// discovered from ambient machine state, so that tests can run several
/// logical workers in one process and prove that a stale owner is fenced out
/// after takeover. It records <em>who</em> holds a lease; the
/// <see cref="FencingToken"/> records <em>which</em> ownership generation is
/// currently authoritative.
/// </para>
/// </remarks>
public readonly record struct WorkerId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerId"/> struct,
    /// validating that it carries a usable, non-empty identity.
    /// </summary>
    /// <param name="value">The non-empty worker identity.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>, which is the
    /// default value and never identifies a worker.
    /// </exception>
    public WorkerId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>Gets the underlying worker identity.</summary>
    public Guid Value { get; }

    /// <summary>
    /// Returns the worker identity in its canonical form, suitable for
    /// logging, tracing, and lease-ownership diagnostics.
    /// </summary>
    public override string ToString() => Value.ToString();
}
