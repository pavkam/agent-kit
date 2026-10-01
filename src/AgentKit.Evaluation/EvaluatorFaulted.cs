// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Reports that the evaluator itself threw instead of concluding, which is infrastructure error rather than agent quality.</summary>
public sealed record EvaluatorFaulted: EvaluationOutcome
{
    /// <summary>Initializes the outcome.</summary>
    /// <param name="errorType">The non-blank normalized exception type name; never raw exception content.</param>
    /// <param name="summary">The non-blank safe explanation.</param>
    /// <param name="evidence">Safe named facts the evaluator recorded; the default array means none.</param>
    /// <exception cref="ArgumentException"><paramref name="errorType"/> or <paramref name="summary"/> is blank, or <paramref name="evidence"/> contains null.</exception>
    public EvaluatorFaulted(string errorType, string summary, ImmutableArray<EvaluationEvidence> evidence = default)
        : base("evaluator_failed", summary, null, evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorType);
        ErrorType = errorType;
    }

    /// <summary>Gets the normalized exception type name.</summary>
    public string ErrorType { get; }
}
