// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of one source search: candidates with the observed watermarks, or a typed failure.</summary>
public sealed record RetrievalSourceResult
{
    private RetrievalSourceResult(ImmutableArray<RetrievalCandidate> candidates, long? deletionGeneration, RetrievalFailure? failure)
    {
        Candidates = candidates;
        DeletionGeneration = deletionGeneration;
        Failure = failure;
    }

    /// <summary>Gets the candidates the source found, best first.</summary>
    /// <value>Empty when the search failed.</value>
    public ImmutableArray<RetrievalCandidate> Candidates { get; }

    /// <summary>Gets the highest store deletion generation the source observed, or <see langword="null"/> when it observed none.</summary>
    public long? DeletionGeneration { get; }

    /// <summary>Gets the typed failure, or <see langword="null"/> when the search ran.</summary>
    public RetrievalFailure? Failure { get; }

    /// <summary>Gets a value indicating whether the search ran.</summary>
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsSucceeded => Failure is null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="candidates">The candidates, best first.</param>
    /// <param name="deletionGeneration">The highest deletion generation observed, or <see langword="null"/>.</param>
    /// <returns>A successful result.</returns>
    /// <exception cref="ArgumentException"><paramref name="candidates"/> is default or contains null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deletionGeneration"/> is negative.</exception>
    public static RetrievalSourceResult Succeeded(ImmutableArray<RetrievalCandidate> candidates, long? deletionGeneration)
    {
        ArgumentException.ThrowIfDefault(candidates);
        ArgumentException.ThrowIfContainsNull(candidates);
        if (deletionGeneration is { } generation)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(generation, nameof(deletionGeneration));
        }

        return new(candidates, deletionGeneration, null);
    }

    /// <summary>Creates a failed result.</summary>
    /// <param name="failure">The typed failure.</param>
    /// <returns>A failed result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static RetrievalSourceResult Failed(RetrievalFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new([], null, failure);
    }
}
