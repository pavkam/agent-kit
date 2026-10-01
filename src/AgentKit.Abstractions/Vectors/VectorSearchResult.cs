// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of a vector search: ranked matches with the index watermark, or a typed refusal.</summary>
public sealed record VectorSearchResult
{
    private VectorSearchResult(ImmutableArray<VectorMatch> matches, long watermark, MemoryStoreFailure? failure)
    {
        Matches = matches;
        Watermark = watermark;
        Failure = failure;
    }

    /// <summary>Gets the matches from best to worst, ties broken by chunk identity so ranking is deterministic.</summary>
    /// <value>Empty when the search was refused.</value>
    public ImmutableArray<VectorMatch> Matches { get; }

    /// <summary>Gets the index watermark when the search ran; it advances with every index mutation.</summary>
    public long Watermark { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> when the search ran.</summary>
    public MemoryStoreFailure? Failure { get; }

    /// <summary>Gets a value indicating whether the search ran.</summary>
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsSearched => Failure is null;

    /// <summary>Creates a result for a search that ran.</summary>
    /// <param name="matches">The ranked matches.</param>
    /// <param name="watermark">The index watermark when the search ran.</param>
    /// <returns>A searched result.</returns>
    /// <exception cref="ArgumentException"><paramref name="matches"/> is default or contains null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="watermark"/> is negative.</exception>
    public static VectorSearchResult Searched(ImmutableArray<VectorMatch> matches, long watermark)
    {
        ArgumentException.ThrowIfDefault(matches);
        ArgumentException.ThrowIfContainsNull(matches);
        ArgumentOutOfRangeException.ThrowIfNegative(watermark);
        return new(matches, watermark, null);
    }

    /// <summary>Creates a refused result.</summary>
    /// <param name="failure">The typed refusal.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static VectorSearchResult Rejected(MemoryStoreFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new([], 0, failure);
    }
}
