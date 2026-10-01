// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Executes one evaluation plan through the public engine surface and returns its report.</summary>
/// <remarks>
/// <para>
/// <see cref="RunAsync"/> accepts a plan, never an engine: a runner is bound through composition to exactly one built
/// <see cref="AgentEngine"/>. Direct implementations are supported; the first-party runner is replaceable through
/// <c>ReplaceEvaluationRunner</c>.
/// </para>
/// <para>Implementations are thread-safe and may run concurrent plans. They receive no privileged runtime state.</para>
/// </remarks>
public interface IEvaluationRunner
{
    /// <summary>Validates and runs one plan, evaluates each case repetition, records results, and publishes the report.</summary>
    /// <param name="plan">The immutable plan to run.</param>
    /// <param name="cancellationToken">Cancels the execution; see remarks for the partial-report contract.</param>
    /// <returns>The report in deterministic plan order, including cases already evaluated when the run was cancelled or stopped.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> is <see langword="null"/>.</exception>
    /// <exception cref="EvaluationPlanRejectedException">The plan is incompatible with the composition; no session, run, or store effect has occurred.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled before any effect occurred.</exception>
    /// <remarks>Once effects have begun, cancellation stops scheduling new case repetitions, propagates to active runs and evaluators, and returns a truthful partial report with <see cref="EvaluationReportStatus.Cancelled"/> instead of throwing. Results already persisted are never deleted.</remarks>
    public Task<EvaluationReport> RunAsync(EvaluationPlan plan, CancellationToken cancellationToken = default);
}
