// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Selects one page of the results recorded for one evaluation run.</summary>
public sealed record EvaluationResultQuery
{
    /// <summary>Gets the largest page a query may request.</summary>
    public const int MaximumPageSize = 500;

    /// <summary>Initializes a validated query.</summary>
    /// <param name="evaluationRunId">The evaluation run to read.</param>
    /// <param name="pageSize">The page size from one to <see cref="MaximumPageSize"/>.</param>
    /// <param name="after">The cursor of the last result already read, or <see langword="null"/> to start at the beginning.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="evaluationRunId"/> is default or <paramref name="pageSize"/> is outside its range.</exception>
    public EvaluationResultQuery(EvaluationRunId evaluationRunId, int pageSize, EvaluationResultCursor? after = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(evaluationRunId, default, nameof(evaluationRunId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaximumPageSize);
        EvaluationRunId = evaluationRunId;
        PageSize = pageSize;
        After = after;
    }

    /// <summary>Gets the evaluation run to read.</summary>
    public EvaluationRunId EvaluationRunId { get; }

    /// <summary>Gets the page size.</summary>
    public int PageSize { get; }

    /// <summary>Gets the cursor of the last result already read, or <see langword="null"/>.</summary>
    public EvaluationResultCursor? After { get; }
}
