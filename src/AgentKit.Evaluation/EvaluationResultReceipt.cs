// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Identifies one result a store recorded.</summary>
public sealed record EvaluationResultReceipt
{
    /// <summary>Initializes a validated receipt.</summary>
    /// <param name="evaluationRunId">The evaluation run.</param>
    /// <param name="caseId">The case.</param>
    /// <param name="caseOrdinal">The zero-based case position.</param>
    /// <param name="repetition">The one-based repetition.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default or an ordinal or repetition is out of range.</exception>
    /// <exception cref="ArgumentException"><paramref name="caseId"/> is blank.</exception>
    public EvaluationResultReceipt(EvaluationRunId evaluationRunId, EvaluationCaseId caseId, int caseOrdinal, int repetition)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(evaluationRunId, default, nameof(evaluationRunId));
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId.Value, nameof(caseId));
        ArgumentOutOfRangeException.ThrowIfNegative(caseOrdinal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repetition);
        EvaluationRunId = evaluationRunId;
        CaseId = caseId;
        CaseOrdinal = caseOrdinal;
        Repetition = repetition;
    }

    /// <summary>Gets the evaluation run.</summary>
    public EvaluationRunId EvaluationRunId { get; }

    /// <summary>Gets the case.</summary>
    public EvaluationCaseId CaseId { get; }

    /// <summary>Gets the zero-based case position.</summary>
    public int CaseOrdinal { get; }

    /// <summary>Gets the one-based repetition.</summary>
    public int Repetition { get; }
}
