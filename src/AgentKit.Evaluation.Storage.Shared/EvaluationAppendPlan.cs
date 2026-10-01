// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Is the planner decision for one append: persist it, replay it, or refuse it.</summary>
internal sealed class EvaluationAppendPlan
{
    private EvaluationAppendPlan(EvaluationAppendPlanKind kind, EvaluationCaseResult result, EvaluationStoreFailure? failure)
    {
        Kind = kind;
        Result = result;
        Failure = failure;
    }

    /// <summary>Gets the decision.</summary>
    internal EvaluationAppendPlanKind Kind { get; }

    /// <summary>Gets the result the decision concerns: the new result, or the already recorded identical one.</summary>
    internal EvaluationCaseResult Result { get; }

    /// <summary>Gets the typed failure when <see cref="Kind"/> is <see cref="EvaluationAppendPlanKind.Rejected"/>.</summary>
    internal EvaluationStoreFailure? Failure { get; }

    /// <summary>Creates a plan to persist a new result.</summary>
    /// <param name="result">The new result.</param>
    /// <returns>The plan.</returns>
    internal static EvaluationAppendPlan Apply(EvaluationCaseResult result) => new(EvaluationAppendPlanKind.Applied, result, null);

    /// <summary>Creates a plan that replays an identical recorded result.</summary>
    /// <param name="result">The recorded result.</param>
    /// <returns>The plan.</returns>
    internal static EvaluationAppendPlan Replay(EvaluationCaseResult result) => new(EvaluationAppendPlanKind.Replayed, result, null);

    /// <summary>Creates a plan that refuses the append.</summary>
    /// <param name="result">The refused result.</param>
    /// <param name="failure">The typed failure.</param>
    /// <returns>The plan.</returns>
    internal static EvaluationAppendPlan Reject(EvaluationCaseResult result, EvaluationStoreFailure failure) =>
        new(EvaluationAppendPlanKind.Rejected, result, failure);

    /// <summary>Gets the acknowledgement the plan implies, once any required persistence succeeded.</summary>
    /// <returns>The typed store result.</returns>
    internal EvaluationStoreResult ToResult() => Kind == EvaluationAppendPlanKind.Rejected
        ? new EvaluationStoreRejected(Failure!)
        : new EvaluationStoreAppended(
            new EvaluationResultReceipt(Result.EvaluationRunId, Result.CaseId, Result.CaseOrdinal, Result.Repetition),
            replayed: Kind == EvaluationAppendPlanKind.Replayed);
}
