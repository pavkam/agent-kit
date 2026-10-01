// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of a retrieval: authorized, budgeted candidates with provenance, or a typed failure.</summary>
/// <remarks>A completed result with no candidates is a valid answer. A failure is typed and content-free, and the pipeline never silently drops provenance or stringifies unsupported media to produce a result.</remarks>
public sealed record RetrievalResult
{
    private RetrievalResult(RetrievalRequestId requestId, ImmutableArray<RetrievalCandidate> candidates, RetrievalSummary? summary, RetrievalFailure? failure)
    {
        RequestId = requestId;
        Candidates = candidates;
        Summary = summary;
        Failure = failure;
    }

    /// <summary>Gets the retrieval request this result answers.</summary>
    public RetrievalRequestId RequestId { get; }

    /// <summary>Gets the authorized candidates in final rank order.</summary>
    /// <value>Empty when the retrieval failed.</value>
    public ImmutableArray<RetrievalCandidate> Candidates { get; }

    /// <summary>Gets the content-free omission summary, or <see langword="null"/> when the retrieval failed.</summary>
    public RetrievalSummary? Summary { get; }

    /// <summary>Gets the typed failure, or <see langword="null"/> when the retrieval completed.</summary>
    public RetrievalFailure? Failure { get; }

    /// <summary>Gets a value indicating whether the retrieval completed.</summary>
    [MemberNotNullWhen(true, nameof(Summary))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsCompleted => Failure is null;

    /// <summary>Creates a completed result.</summary>
    /// <param name="requestId">The retrieval request identity.</param>
    /// <param name="candidates">The authorized candidates in rank order.</param>
    /// <param name="summary">The content-free omission summary.</param>
    /// <returns>A completed result.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="requestId"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="candidates"/> is default or contains null, or a candidate belongs to another request.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="summary"/> is null.</exception>
    public static RetrievalResult Completed(RetrievalRequestId requestId, ImmutableArray<RetrievalCandidate> candidates, RetrievalSummary summary)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(requestId, default);
        ArgumentException.ThrowIfDefault(candidates);
        ArgumentException.ThrowIfContainsNull(candidates);
        ArgumentNullException.ThrowIfNull(summary);
        foreach (var candidate in candidates)
        {
            ArgumentException.ThrowIfNotEqual(candidate.RequestId, requestId, nameof(candidates));
        }

        return new(requestId, candidates, summary, null);
    }

    /// <summary>Creates a failed result.</summary>
    /// <param name="requestId">The retrieval request identity.</param>
    /// <param name="failure">The typed failure.</param>
    /// <returns>A failed result.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="requestId"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static RetrievalResult Failed(RetrievalRequestId requestId, RetrievalFailure failure)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(requestId, default);
        ArgumentNullException.ThrowIfNull(failure);
        return new(requestId, [], null, failure);
    }
}
