// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Explains why one embedding candidate alias was skipped or accepted.</summary>
public sealed record EmbeddingSelectionDiagnostic
{
    /// <summary>Initializes a diagnostic entry.</summary>
    /// <param name="alias">The candidate alias.</param>
    /// <param name="outcome">What happened to the candidate.</param>
    /// <param name="reason">A redacted explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
    public EmbeddingSelectionDiagnostic(
        EmbeddingModelAlias alias,
        ModelCandidateOutcome outcome,
        string reason)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Alias = alias;
        Outcome = outcome;
        Reason = reason;
    }

    /// <summary>Gets the candidate alias.</summary>
    public EmbeddingModelAlias Alias { get; init; }

    /// <summary>Gets the candidate outcome.</summary>
    public ModelCandidateOutcome Outcome { get; init; }

    /// <summary>Gets the redacted explanation.</summary>
    public string Reason { get; init; }
}
