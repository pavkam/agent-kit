// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Holds recorded results in memory in deterministic order and answers the planner lookups.</summary>
/// <remarks>The class is not thread-safe; its owner serializes access under one lock. Entries are ordered by case ordinal then repetition regardless of append order.</remarks>
internal sealed class EvaluationResultState: IEvaluationResultLookup
{
    private readonly Dictionary<EvaluationRunId, RunEntry> _runs = [];

    /// <summary>Records an accepted result.</summary>
    /// <param name="result">The result the planner accepted.</param>
    internal void Add(EvaluationCaseResult result)
    {
        Debug.Assert(result is not null, "Adapters add only planner-accepted results.");
        if (!_runs.TryGetValue(result.EvaluationRunId, out var run))
        {
            run = new RunEntry(new EvaluationRunPin(result.PlanId, result.PlanVersion));
            _runs.Add(result.EvaluationRunId, run);
        }

        run.Results.Add((result.CaseOrdinal, result.Repetition), result);
        _ = run.CaseAt.TryAdd(result.CaseOrdinal, result.CaseId);
        _ = run.OrdinalOf.TryAdd(result.CaseId, result.CaseOrdinal);
    }

    /// <inheritdoc/>
    public EvaluationRunPin? FindRun(EvaluationRunId runId) => _runs.TryGetValue(runId, out var run) ? run.Pin : null;

    /// <inheritdoc/>
    public EvaluationCaseResult? Find(EvaluationRunId runId, int caseOrdinal, int repetition) =>
        _runs.TryGetValue(runId, out var run) && run.Results.TryGetValue((caseOrdinal, repetition), out var result) ? result : null;

    /// <inheritdoc/>
    public EvaluationCaseId? FindCaseAt(EvaluationRunId runId, int caseOrdinal) =>
        _runs.TryGetValue(runId, out var run) && run.CaseAt.TryGetValue(caseOrdinal, out var caseId) ? caseId : null;

    /// <inheritdoc/>
    public int? FindOrdinalOf(EvaluationRunId runId, EvaluationCaseId caseId) =>
        _runs.TryGetValue(runId, out var run) && run.OrdinalOf.TryGetValue(caseId, out var ordinal) ? ordinal : null;

    /// <inheritdoc/>
    public IReadOnlyList<EvaluationCaseResult> Read(EvaluationRunId runId, EvaluationResultCursor? after, int limit)
    {
        Debug.Assert(limit > 0, "The planner requests a positive limit.");
        if (!_runs.TryGetValue(runId, out var run))
        {
            return [];
        }

        var page = new List<EvaluationCaseResult>(Math.Min(limit, run.Results.Count));
        foreach (var ((ordinal, repetition), result) in run.Results)
        {
            if (after is { } cursor && (ordinal < cursor.CaseOrdinal || (ordinal == cursor.CaseOrdinal && repetition <= cursor.Repetition)))
            {
                continue;
            }

            page.Add(result);
            if (page.Count == limit)
            {
                break;
            }
        }

        return page;
    }

    private sealed class RunEntry(EvaluationRunPin pin)
    {
        internal EvaluationRunPin Pin { get; } = pin;

        internal SortedDictionary<(int Ordinal, int Repetition), EvaluationCaseResult> Results { get; } = [];

        internal Dictionary<int, EvaluationCaseId> CaseAt { get; } = [];

        internal Dictionary<EvaluationCaseId, int> OrdinalOf { get; } = [];
    }
}
