// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>An inclusive range of session sequences covered by a compaction.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record CompactionSourceRange
{
    /// <summary>Initializes a new instance of the <see cref="CompactionSourceRange"/> record.</summary>
    /// <param name="startInclusive">The first covered sequence.</param>
    /// <param name="endInclusive">The last covered sequence.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="endInclusive"/> precedes <paramref name="startInclusive"/>.
    /// </exception>
    public CompactionSourceRange(SessionSequence startInclusive, SessionSequence endInclusive)
    {
        if (endInclusive.Value < startInclusive.Value)
        {
            throw new ArgumentException("EndInclusive must not precede StartInclusive.", nameof(endInclusive));
        }

        StartInclusive = startInclusive;
        EndInclusive = endInclusive;
    }

    /// <summary>Gets the first covered sequence.</summary>
    public SessionSequence StartInclusive { get; init; }

    /// <summary>Gets the last covered sequence.</summary>
    public SessionSequence EndInclusive { get; init; }
}
