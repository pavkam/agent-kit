// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The complete set of authoritative terminal records produced by one executed <see cref="ToolBatch"/>.</summary>
/// <remarks>
/// Every entry accepted into the originating batch reaches exactly one terminal <see cref="ToolCallResult"/> here,
/// including a pre-invocation rejection, a timeout, or an interruption after cancellation; completion order may
/// differ from the batch's source order. This type is an immutable value object with structural equality over its
/// fields and is safe to share across threads without synchronization.
/// </remarks>
public sealed record ToolBatchResult
{
    /// <summary>Initializes an immutable batch result.</summary>
    /// <param name="results">The initialized terminal results, one per accepted batch entry.</param>
    /// <exception cref="ArgumentException"><paramref name="results"/> is uninitialized or contains a null element.</exception>
    public ToolBatchResult(ImmutableArray<ToolCallResult> results)
    {
        ArgumentException.ThrowIfContainsNull(results);
        Results = results;
    }

    /// <summary>Gets the terminal results produced by the batch.</summary>
    /// <value>An initialized sequence with one entry per accepted call; may be empty for an empty batch.</value>
    public ImmutableArray<ToolCallResult> Results { get; }

    /// <summary>Determines complete structural result equality.</summary>
    /// <param name="other">The record to compare, or null.</param>
    /// <returns>True when every ordered result is equal.</returns>
    public bool Equals(ToolBatchResult? other) => other is not null && Results.SequenceEqual(other.Results);

    /// <summary>Returns a hash compatible with complete structural equality.</summary>
    /// <returns>A hash over every ordered result.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var result in Results)
        {
            hash.Add(result);
        }

        return hash.ToHashCode();
    }
}
