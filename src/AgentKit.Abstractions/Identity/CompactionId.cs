// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one logical compaction checkpoint, stable across retries of
/// the same attempt.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>, safe to share across threads without
/// synchronization. Unlike <see cref="OperationId"/>, which identifies one
/// causal invocation, <see cref="CompactionId"/> identifies the checkpoint
/// itself: a retried attempt after a transient failure may reuse the same
/// <see cref="CompactionId"/> while still getting a fresh
/// <see cref="OperationId"/> for each invocation.
/// </remarks>
public readonly record struct CompactionId
{
    /// <summary>Initializes a new instance of the <see cref="CompactionId"/> struct.</summary>
    /// <param name="value">The non-empty underlying globally unique identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="Guid.Empty"/>.
    /// </exception>
    public CompactionId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty, nameof(value));
        Value = value;
    }

    /// <summary>Gets the underlying globally unique identifier.</summary>
    public Guid Value { get; }

    /// <summary>Returns the canonical text form of this identity.</summary>
    public override string ToString() => Value.ToString("D");
}
