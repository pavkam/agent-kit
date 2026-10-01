// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Reads recorded results so the shared planner can decide an append or a read without knowing the storage.</summary>
/// <remarks>The caller serializes access: an adapter calls the planner and applies its plan inside one lock or one write transaction, so the lookup never observes a concurrent writer.</remarks>
internal interface IEvaluationResultLookup
{
    /// <summary>Finds the plan identity and version a run is pinned to.</summary>
    /// <param name="runId">The evaluation run.</param>
    /// <returns>The pin, or <see langword="null"/> when no result is recorded for the run.</returns>
    public EvaluationRunPin? FindRun(EvaluationRunId runId);

    /// <summary>Finds the result recorded at one position.</summary>
    /// <param name="runId">The evaluation run.</param>
    /// <param name="caseOrdinal">The zero-based case position.</param>
    /// <param name="repetition">The one-based repetition.</param>
    /// <returns>The result, or <see langword="null"/>.</returns>
    public EvaluationCaseResult? Find(EvaluationRunId runId, int caseOrdinal, int repetition);

    /// <summary>Finds the case recorded at an ordinal.</summary>
    /// <param name="runId">The evaluation run.</param>
    /// <param name="caseOrdinal">The zero-based case position.</param>
    /// <returns>The case identity, or <see langword="null"/>.</returns>
    public EvaluationCaseId? FindCaseAt(EvaluationRunId runId, int caseOrdinal);

    /// <summary>Finds the ordinal a case is recorded at.</summary>
    /// <param name="runId">The evaluation run.</param>
    /// <param name="caseId">The case.</param>
    /// <returns>The ordinal, or <see langword="null"/>.</returns>
    public int? FindOrdinalOf(EvaluationRunId runId, EvaluationCaseId caseId);

    /// <summary>Reads results after a cursor in case-ordinal then repetition order.</summary>
    /// <param name="runId">The evaluation run.</param>
    /// <param name="after">The exclusive lower bound, or <see langword="null"/> to start at the beginning.</param>
    /// <param name="limit">The positive greatest number of results to return.</param>
    /// <returns>At most <paramref name="limit"/> results, ordered.</returns>
    public IReadOnlyList<EvaluationCaseResult> Read(EvaluationRunId runId, EvaluationResultCursor? after, int limit);
}
