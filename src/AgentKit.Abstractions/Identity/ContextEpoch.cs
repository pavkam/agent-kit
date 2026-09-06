// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>
/// Identifies one version of the effective instructions and configuration a
/// session's context was assembled under. A new epoch means old
/// compaction summaries may no longer safely represent the current
/// instruction contract.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural, numeric equality
/// and ordering over <see cref="Value"/>, safe to share across threads
/// without synchronization. A replacement instruction set that contradicts
/// the historical prefix starts a new epoch rather than silently pretending
/// old messages occurred under the new contract; compaction policy compares
/// a candidate's captured epoch against the session's current epoch before
/// treating an older checkpoint as still applicable.
/// </remarks>
public readonly record struct ContextEpoch
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContextEpoch"/> struct,
    /// validating that it is not a nonsensical negative epoch number.
    /// </summary>
    /// <param name="value">The non-negative epoch number.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative.
    /// </exception>
    public ContextEpoch(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the epoch number.</summary>
    public long Value { get; }

    /// <summary>Returns the epoch number as invariant-culture text.</summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
