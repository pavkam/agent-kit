// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Marks the last result of a page so a read continues strictly after it.</summary>
/// <remarks>The cursor is a position in the deterministic result order, not an identity: a result recorded later at an earlier position is not returned by a read that already passed it.</remarks>
public readonly record struct EvaluationResultCursor
{
    /// <summary>Initializes a validated cursor.</summary>
    /// <param name="caseOrdinal">The zero-based case position of the last returned result.</param>
    /// <param name="repetition">The one-based repetition of the last returned result.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="caseOrdinal"/> is negative or <paramref name="repetition"/> is not positive.</exception>
    public EvaluationResultCursor(int caseOrdinal, int repetition)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(caseOrdinal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repetition);
        CaseOrdinal = caseOrdinal;
        Repetition = repetition;
    }

    /// <summary>Gets the zero-based case position of the last returned result.</summary>
    public int CaseOrdinal { get; }

    /// <summary>Gets the one-based repetition of the last returned result.</summary>
    public int Repetition { get; }
}
