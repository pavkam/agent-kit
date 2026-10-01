// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Reports that the evaluator did not run because a declared precondition was not met.</summary>
public sealed record EvaluationSkipped: EvaluationOutcome
{
    /// <summary>Initializes the outcome.</summary>
    /// <param name="summary">The non-blank safe explanation.</param>
    /// <param name="evidence">Safe named facts the evaluator recorded; the default array means none.</param>
    /// <exception cref="ArgumentException"><paramref name="summary"/> is blank or <paramref name="evidence"/> contains null.</exception>
    public EvaluationSkipped(string summary, ImmutableArray<EvaluationEvidence> evidence = default)
        : base("skipped", summary, null, evidence)
    {
    }
}
