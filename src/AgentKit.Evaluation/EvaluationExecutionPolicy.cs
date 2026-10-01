// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Declares how one plan is executed: concurrency, repetition, deadlines, caps, and failure behavior.</summary>
/// <remarks>
/// The runner caps the policy against its immutable <c>EvaluationOptions</c> snapshot and rejects a plan that asks for more
/// before any case runs. Result ordering is always deterministic by plan case position and repetition, so no ordering setting exists.
/// </remarks>
public sealed record EvaluationExecutionPolicy
{
    /// <summary>Gets the serial single-repetition policy with no deadlines, caps, or early stop.</summary>
    public static EvaluationExecutionPolicy Default { get; } = new(1, 1, null, null, null, false, false);

    /// <summary>Initializes a validated policy.</summary>
    /// <param name="maximumConcurrentCases">The positive number of case repetitions that may run at once.</param>
    /// <param name="repetitions">The positive number of times each case runs.</param>
    /// <param name="caseTimeout">The per-repetition time limit, or <see langword="null"/> to use the runner default.</param>
    /// <param name="planDeadline">The whole-plan time limit, or <see langword="null"/> for none.</param>
    /// <param name="maximumCaseRuns">The cap on total case repetitions (cases times repetitions), or <see langword="null"/> for none.</param>
    /// <param name="stopOnEvaluatorFailure">Whether an evaluator failure stops scheduling later case repetitions.</param>
    /// <param name="permitUnsupportedEvaluators">Whether an evaluator that cannot assess a case yields a typed unsupported outcome instead of failing plan validation.</param>
    /// <exception cref="ArgumentOutOfRangeException">A count or cap is not positive, or a time limit is not positive.</exception>
    public EvaluationExecutionPolicy(
        int maximumConcurrentCases,
        int repetitions,
        TimeSpan? caseTimeout,
        TimeSpan? planDeadline,
        int? maximumCaseRuns,
        bool stopOnEvaluatorFailure,
        bool permitUnsupportedEvaluators)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumConcurrentCases);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repetitions);
        if (caseTimeout is { } caseLimit)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(caseLimit, TimeSpan.Zero, nameof(caseTimeout));
        }

        if (planDeadline is { } deadline)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(deadline, TimeSpan.Zero, nameof(planDeadline));
        }

        if (maximumCaseRuns is { } cap)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cap, nameof(maximumCaseRuns));
        }

        MaximumConcurrentCases = maximumConcurrentCases;
        Repetitions = repetitions;
        CaseTimeout = caseTimeout;
        PlanDeadline = planDeadline;
        MaximumCaseRuns = maximumCaseRuns;
        StopOnEvaluatorFailure = stopOnEvaluatorFailure;
        PermitUnsupportedEvaluators = permitUnsupportedEvaluators;
    }

    /// <summary>Gets the number of case repetitions that may run at once.</summary>
    public int MaximumConcurrentCases { get; }

    /// <summary>Gets the number of times each case runs.</summary>
    public int Repetitions { get; }

    /// <summary>Gets the per-repetition time limit, or <see langword="null"/> to use the runner default.</summary>
    public TimeSpan? CaseTimeout { get; }

    /// <summary>Gets the whole-plan time limit, or <see langword="null"/> for none.</summary>
    public TimeSpan? PlanDeadline { get; }

    /// <summary>Gets the cap on total case repetitions, or <see langword="null"/> for none.</summary>
    public int? MaximumCaseRuns { get; }

    /// <summary>Gets whether an evaluator failure stops scheduling later case repetitions.</summary>
    public bool StopOnEvaluatorFailure { get; }

    /// <summary>Gets whether an evaluator that cannot assess a case yields a typed unsupported outcome instead of failing plan validation.</summary>
    public bool PermitUnsupportedEvaluators { get; }
}
