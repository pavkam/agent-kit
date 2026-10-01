// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Decides appends and reads over recorded results so every adapter agrees on identity, idempotency, and order.</summary>
/// <remarks>
/// The planner is pure: it reads through <see cref="IEvaluationResultLookup"/> and returns a decision. The adapter applies the
/// decision atomically with respect to other writers. Identity is evaluation run, case ordinal, and repetition; a run is pinned
/// to the plan identity and version of its first result, and a case occupies exactly one ordinal.
/// </remarks>
internal static class EvaluationResultPlanner
{
    /// <summary>Plans one append.</summary>
    /// <param name="lookup">The recorded evidence.</param>
    /// <param name="result">The result to append.</param>
    /// <returns>The decision.</returns>
    internal static EvaluationAppendPlan PlanAppend(IEvaluationResultLookup lookup, EvaluationCaseResult result)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(result is not null, "Adapters validate the result before planning.");
        var pinnedElsewhere = lookup.FindRun(result.EvaluationRunId) is { } pin
            && (pin.PlanId != result.PlanId || pin.PlanVersion != result.PlanVersion);
        return pinnedElsewhere
            ? Conflict(result, "The evaluation run is already pinned to a different plan identity or version.")
            : lookup.Find(result.EvaluationRunId, result.CaseOrdinal, result.Repetition) is { } existing
                ? existing.Equals(result)
                    ? EvaluationAppendPlan.Replay(existing)
                    : Conflict(result, "A different result is already recorded for this case repetition.")
                : lookup.FindCaseAt(result.EvaluationRunId, result.CaseOrdinal) is { } occupant && occupant != result.CaseId
                    ? Conflict(result, "Another case already occupies this case ordinal in the run.")
                    : lookup.FindOrdinalOf(result.EvaluationRunId, result.CaseId) is { } ordinal && ordinal != result.CaseOrdinal
                        ? Conflict(result, "This case is already recorded at a different case ordinal in the run.")
                        : EvaluationAppendPlan.Apply(result);
    }

    /// <summary>Reads one deterministic page.</summary>
    /// <param name="lookup">The recorded evidence.</param>
    /// <param name="query">The validated query.</param>
    /// <returns>The page; empty for an unknown run.</returns>
    internal static EvaluationResultsRead Read(IEvaluationResultLookup lookup, EvaluationResultQuery query)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(query is not null, "Adapters validate the query before planning.");
        var fetched = lookup.Read(query.EvaluationRunId, query.After, query.PageSize + 1);
        var page = fetched.Take(query.PageSize).ToImmutableArray();
        var more = fetched.Count > query.PageSize;
        return new EvaluationResultsRead(
            page,
            more ? new EvaluationResultCursor(page[^1].CaseOrdinal, page[^1].Repetition) : null);
    }

    private static EvaluationAppendPlan Conflict(EvaluationCaseResult result, string message) =>
        EvaluationAppendPlan.Reject(result, new EvaluationStoreFailure(EvaluationStoreFailureKind.IdentityConflict, message));
}
