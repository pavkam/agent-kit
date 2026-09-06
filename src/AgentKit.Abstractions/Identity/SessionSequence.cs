// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>
/// A monotonically increasing sequence number for one session branch,
/// identifying a message's position within that branch's append order.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural, numeric equality
/// and ordering over <see cref="Value"/>. It carries no mutable state itself
/// and is safe to share and compare across threads without synchronization.
/// </para>
/// <para>
/// A sequence number is ordering data, not identity: it describes "the Nth
/// message appended to this branch," not a globally meaningful value on its
/// own, and it is only comparable within the same session and branch. A
/// history cursor pairs a <see cref="BranchId"/>,
/// <see cref="SessionVersion"/>, and <see cref="SessionSequence"/> together
/// to describe a specific, reproducible read position, so replay and
/// context assembly can request "everything after sequence N" deterministically.
/// </para>
/// <para>
/// Unlike the GUID-based identities, zero is a legitimate
/// <see cref="SessionSequence"/> (the position before any message has been
/// appended); only negative values are rejected. First-party stores number
/// entries within a branch starting at 1, so <c>new SessionSequence(0)</c>
/// unambiguously means "before the first entry" without requiring a
/// negative sentinel, and a branch's sequence count after N appended
/// entries equals its <see cref="SessionVersion"/>.
/// </para>
/// </remarks>
public readonly record struct SessionSequence
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionSequence"/>
    /// struct, validating that it is not a nonsensical negative sequence
    /// number.
    /// </summary>
    /// <param name="value">The non-negative sequence number.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative.
    /// </exception>
    public SessionSequence(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the sequence number.</summary>
    public long Value { get; }

    /// <summary>
    /// Returns the sequence number as invariant-culture text, suitable for
    /// logging and cursor diagnostics.
    /// </summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
