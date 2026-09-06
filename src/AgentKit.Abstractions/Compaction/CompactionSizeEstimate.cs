// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A bounded, approximate size measurement of a compaction source or
/// candidate, used to decide whether a compaction attempt achieved a
/// measurable reduction.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. These are
/// estimates, not exact provider-billed usage; <see cref="ModelUsage"/>
/// remains the authoritative record of actual token consumption when a
/// model-backed strategy is involved.
/// </remarks>
public sealed record CompactionSizeEstimate
{
    /// <summary>Initializes a new instance of the <see cref="CompactionSizeEstimate"/> record.</summary>
    /// <param name="tokens">The estimated token count.</param>
    /// <param name="bytes">The estimated serialized byte size.</param>
    /// <param name="entryCount">The number of session entries this estimate covers.</param>
    /// <exception cref="ArgumentOutOfRangeException">Any parameter is negative.</exception>
    public CompactionSizeEstimate(int tokens, long bytes, int entryCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tokens);
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(entryCount);

        Tokens = tokens;
        Bytes = bytes;
        EntryCount = entryCount;
    }

    /// <summary>Gets the estimated token count.</summary>
    public int Tokens { get; init; }

    /// <summary>Gets the estimated serialized byte size.</summary>
    public long Bytes { get; init; }

    /// <summary>Gets the number of session entries this estimate covers.</summary>
    public int EntryCount { get; init; }
}
