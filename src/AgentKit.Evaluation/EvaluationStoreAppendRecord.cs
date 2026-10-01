// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Records what a result store answered when one case result was appended.</summary>
public sealed record EvaluationStoreAppendRecord
{
    /// <summary>Initializes a validated record.</summary>
    /// <param name="caseId">The appended case.</param>
    /// <param name="repetition">The one-based repetition.</param>
    /// <param name="result">The typed store answer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="caseId"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="repetition"/> is not positive.</exception>
    public EvaluationStoreAppendRecord(EvaluationCaseId caseId, int repetition, EvaluationStoreResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId.Value, nameof(caseId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repetition);
        ArgumentNullException.ThrowIfNull(result);
        CaseId = caseId;
        Repetition = repetition;
        Result = result;
    }

    /// <summary>Gets the appended case.</summary>
    public EvaluationCaseId CaseId { get; }

    /// <summary>Gets the one-based repetition.</summary>
    public int Repetition { get; }

    /// <summary>Gets the typed store answer.</summary>
    public EvaluationStoreResult Result { get; }
}
