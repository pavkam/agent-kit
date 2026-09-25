// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One complete rerank attempt request.</summary>
public sealed record RerankModelRequest
{
    /// <summary>Initializes a rerank model request.</summary>
    /// <param name="operation">The protected operation context.</param>
    /// <param name="selection">The captured reranker selection.</param>
    /// <param name="request">The rerank request body.</param>
    /// <param name="attempt">The one-based attempt number.</param>
    /// <param name="deadline">The attempt deadline.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="attempt"/> is less than one.</exception>
    public RerankModelRequest(
        ProtectedSemanticOperationContext operation,
        RerankerSelectionDecision selection,
        RerankRequest request,
        int attempt,
        DateTimeOffset deadline)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        Operation = operation;
        Selection = selection;
        Request = request;
        Attempt = attempt;
        Deadline = deadline;
    }

    /// <summary>Gets the protected operation context.</summary>
    public ProtectedSemanticOperationContext Operation { get; init; }

    /// <summary>Gets the captured selection.</summary>
    public RerankerSelectionDecision Selection { get; init; }

    /// <summary>Gets the rerank request body.</summary>
    public RerankRequest Request { get; init; }

    /// <summary>Gets the one-based attempt number.</summary>
    public int Attempt { get; init; }

    /// <summary>Gets the attempt deadline.</summary>
    public DateTimeOffset Deadline { get; init; }
}
