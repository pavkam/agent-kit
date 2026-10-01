// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Reports that the evaluator cannot assess the case; produced only where the plan permits unsupported evaluators.</summary>
public sealed record EvaluationUnsupported: EvaluationOutcome
{
    /// <summary>Initializes the outcome.</summary>
    /// <param name="summary">The non-blank safe explanation.</param>
    /// <param name="evidence">Safe named facts the evaluator recorded; the default array means none.</param>
    /// <exception cref="ArgumentException"><paramref name="summary"/> is blank or <paramref name="evidence"/> contains null.</exception>
    public EvaluationUnsupported(string summary, ImmutableArray<EvaluationEvidence> evidence = default)
        : base("unsupported", summary, null, evidence)
    {
    }
}
