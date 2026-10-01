// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Reports that the case repetition satisfied every criterion the evaluator assessed.</summary>
public sealed record EvaluationPassed: EvaluationOutcome
{
    /// <summary>Initializes the outcome.</summary>
    /// <param name="score">The score and its uncertainty, or <see langword="null"/>.</param>
    /// <param name="summary">The non-blank safe explanation.</param>
    /// <param name="evidence">Safe named facts the evaluator recorded; the default array means none.</param>
    /// <exception cref="ArgumentException"><paramref name="summary"/> is blank or <paramref name="evidence"/> contains null.</exception>
    public EvaluationPassed(EvaluationScore? score, string summary, ImmutableArray<EvaluationEvidence> evidence = default)
        : base("passed", summary, score, evidence)
    {
    }
}
