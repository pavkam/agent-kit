// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Reports that cancellation or a deadline stopped the evaluator before it concluded.</summary>
public sealed record EvaluationCancelled: EvaluationOutcome
{
    /// <summary>Initializes the outcome.</summary>
    /// <param name="summary">The non-blank safe explanation.</param>
    /// <param name="evidence">Safe named facts the evaluator recorded; the default array means none.</param>
    /// <exception cref="ArgumentException"><paramref name="summary"/> is blank or <paramref name="evidence"/> contains null.</exception>
    public EvaluationCancelled(string summary, ImmutableArray<EvaluationEvidence> evidence = default)
        : base("cancelled", summary, null, evidence)
    {
    }
}
