// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Is thrown when a plan is incompatible with the composition, before any session, run, store, or exporter effect.</summary>
/// <remarks>The exception carries every detected problem so a dataset author fixes them in one pass. Messages never contain dataset content.</remarks>
public sealed class EvaluationPlanRejectedException: InvalidOperationException
{
    /// <summary>Initializes the exception with the detected problems.</summary>
    /// <param name="problems">At least one problem.</param>
    /// <exception cref="ArgumentException"><paramref name="problems"/> is default, empty, or contains null.</exception>
    public EvaluationPlanRejectedException(ImmutableArray<EvaluationPlanProblem> problems)
        : base(BuildMessage(problems)) => Problems = problems;

    /// <summary>Gets every detected problem in detection order.</summary>
    public ImmutableArray<EvaluationPlanProblem> Problems { get; }

    private static string BuildMessage(ImmutableArray<EvaluationPlanProblem> problems)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(problems);
        ArgumentException.ThrowIfContainsNull(problems);
        return $"The evaluation plan cannot run: {string.Join("; ", problems.Select(static problem => problem.SafeMessage))}";
    }
}
