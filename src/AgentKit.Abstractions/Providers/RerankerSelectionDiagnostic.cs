// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Explains why one reranker candidate was skipped or accepted.</summary>
public sealed record RerankerSelectionDiagnostic
{
    /// <summary>Initializes a diagnostic entry.</summary>
    /// <param name="alias">The candidate alias.</param>
    /// <param name="outcome">The candidate outcome.</param>
    /// <param name="reason">A redacted explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
    public RerankerSelectionDiagnostic(RerankerAlias alias, ModelCandidateOutcome outcome, string reason)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Alias = alias;
        Outcome = outcome;
        Reason = reason;
    }

    /// <summary>Gets the candidate alias.</summary>
    public RerankerAlias Alias { get; init; }

    /// <summary>Gets the candidate outcome.</summary>
    public ModelCandidateOutcome Outcome { get; init; }

    /// <summary>Gets the redacted explanation.</summary>
    public string Reason { get; init; }
}
