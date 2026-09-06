// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The complete, immutable input to one candidate-validation attempt.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record CompactionValidationRequest
{
    /// <summary>Initializes a new instance of the <see cref="CompactionValidationRequest"/> record.</summary>
    /// <param name="request">The compaction request this validation serves.</param>
    /// <param name="source">The source snapshot the candidate was produced from.</param>
    /// <param name="cut">The cut the candidate was produced for.</param>
    /// <param name="candidate">The candidate to validate.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/>, <paramref name="source"/>,
    /// <paramref name="cut"/>, or <paramref name="candidate"/> is null.
    /// </exception>
    public CompactionValidationRequest(
        CompactionRequest request, CompactionSourceSnapshot source, CompactionCut cut, CompactionCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(cut);
        ArgumentNullException.ThrowIfNull(candidate);

        Request = request;
        Source = source;
        Cut = cut;
        Candidate = candidate;
    }

    /// <summary>Gets the compaction request this validation serves.</summary>
    public CompactionRequest Request { get; init; }

    /// <summary>Gets the source snapshot the candidate was produced from.</summary>
    public CompactionSourceSnapshot Source { get; init; }

    /// <summary>Gets the cut the candidate was produced for.</summary>
    public CompactionCut Cut { get; init; }

    /// <summary>Gets the candidate to validate.</summary>
    public CompactionCandidate Candidate { get; init; }
}
