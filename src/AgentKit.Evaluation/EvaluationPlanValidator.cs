// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Validates a plan against the composition and resolves its collaborators before any session, run, or store effect.</summary>
/// <remarks>Validation performs only reads: agent catalog resolution, evaluator and exporter lookup, and store selection. It reports every problem it can find in one pass.</remarks>
internal static class EvaluationPlanValidator
{
    /// <summary>Validates one plan.</summary>
    /// <param name="engine">The engine whose public catalog resolves each case agent.</param>
    /// <param name="plan">The plan to validate.</param>
    /// <param name="evaluators">The evaluator catalog.</param>
    /// <param name="stores">The result-store selector.</param>
    /// <param name="exporters">The exporter catalog.</param>
    /// <param name="options">The immutable runner caps.</param>
    /// <param name="cancellationToken">Cancels resolution before any effect.</param>
    /// <returns>The resolved collaborators.</returns>
    /// <exception cref="EvaluationPlanRejectedException">The plan is incompatible with the composition.</exception>
    /// <exception cref="InvalidOperationException">A registered evaluator reports a descriptor key other than its registration key.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    internal static async ValueTask<ValidatedEvaluationPlan> ValidateAsync(
        AgentEngine engine,
        EvaluationPlan plan,
        IEvaluatorCatalog evaluators,
        IEvaluationResultStoreSelector stores,
        IEvaluationReportExporterCatalog exporters,
        EvaluationOptionsSnapshot options,
        CancellationToken cancellationToken)
    {
        Debug.Assert(engine is not null && plan is not null && options is not null, "The runner supplies its collaborators.");
        var problems = ImmutableArray.CreateBuilder<EvaluationPlanProblem>();
        var policy = plan.Execution;
        if (policy.MaximumConcurrentCases > options.MaximumConcurrentCases)
        {
            problems.Add(new(EvaluationPlanProblemKind.ExceedsLimits, null, $"The plan asks for {policy.MaximumConcurrentCases} concurrent cases but the runner allows {options.MaximumConcurrentCases}."));
        }

        if (policy.Repetitions > options.MaximumRepetitions)
        {
            problems.Add(new(EvaluationPlanProblemKind.ExceedsLimits, null, $"The plan asks for {policy.Repetitions} repetitions but the runner allows {options.MaximumRepetitions}."));
        }

        var caseRuns = (long) plan.Cases.Length * policy.Repetitions;
        if (policy.MaximumCaseRuns is { } cap && caseRuns > cap)
        {
            problems.Add(new(EvaluationPlanProblemKind.ExceedsLimits, null, $"The plan schedules {caseRuns} case repetitions but declares a cap of {cap}."));
        }

        var agents = ImmutableDictionary.CreateBuilder<AgentId, Agent>();
        var missingAgents = new HashSet<AgentId>();
        var evaluatorMap = ImmutableDictionary.CreateBuilder<EvaluatorKey, IEvaluator>();
        var missingEvaluators = new HashSet<EvaluatorKey>();
        foreach (var evaluationCase in plan.Cases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!agents.TryGetValue(evaluationCase.AgentId, out var agent) && !missingAgents.Contains(evaluationCase.AgentId))
            {
                if (await engine.GetAgentAsync(evaluationCase.AgentId, cancellationToken).ConfigureAwait(false) is ResolvedAgent resolved)
                {
                    agent = resolved.Agent;
                    agents.Add(evaluationCase.AgentId, agent);
                }
                else
                {
                    _ = missingAgents.Add(evaluationCase.AgentId);
                }
            }

            if (agent is null)
            {
                problems.Add(new(EvaluationPlanProblemKind.AgentNotFound, evaluationCase.Id, "The case names an agent the engine does not host."));
            }
            else if (agent.Definition.SessionProfile != evaluationCase.Execution.SessionProfile)
            {
                problems.Add(new(EvaluationPlanProblemKind.SessionProfileMismatch, evaluationCase.Id, "The agent definition does not select the session profile the case declares."));
            }

            foreach (var reference in evaluationCase.Evaluators)
            {
                if (!evaluatorMap.TryGetValue(reference.Key, out var evaluator) && !missingEvaluators.Contains(reference.Key))
                {
                    evaluator = evaluators.Find(reference.Key);
                    if (evaluator is null)
                    {
                        _ = missingEvaluators.Add(reference.Key);
                    }
                    else
                    {
                        evaluatorMap.Add(reference.Key, evaluator);
                    }
                }

                if (evaluator is null)
                {
                    problems.Add(new(EvaluationPlanProblemKind.EvaluatorUnavailable, evaluationCase.Id, $"The evaluator '{reference.Key}' is not registered."));
                    continue;
                }

                var descriptor = evaluator.Descriptor;
                if (reference.RequiredVersion is { } required && descriptor.Version != required)
                {
                    problems.Add(new(EvaluationPlanProblemKind.EvaluatorUnavailable, evaluationCase.Id, $"The evaluator '{reference.Key}' is registered at version {descriptor.Version}, not the required version {required}."));
                }
                else if (!policy.PermitUnsupportedEvaluators && !descriptor.Supports(evaluationCase.Criteria, evaluationCase.Fixture))
                {
                    problems.Add(new(EvaluationPlanProblemKind.EvaluatorUnsupported, evaluationCase.Id, $"The evaluator '{reference.Key}' cannot assess the criteria and fixture of this case."));
                }
            }
        }

        IEvaluationResultStore? store = null;
        if (plan.Recording.ResultStore is { } storeKey)
        {
            if (await stores.SelectAsync(storeKey, cancellationToken).ConfigureAwait(false) is EvaluationResultStoreSelected selected)
            {
                store = selected.Store;
            }
            else
            {
                problems.Add(new(EvaluationPlanProblemKind.DestinationUnavailable, null, $"The result store '{storeKey}' is not registered."));
            }
        }

        var resolvedExporters = ImmutableArray.CreateBuilder<KeyValuePair<EvaluationReportExporterKey, IEvaluationReportExporter>>();
        foreach (var exporterKey in plan.Recording.Exporters)
        {
            if (exporters.Find(exporterKey) is { } exporter)
            {
                resolvedExporters.Add(new(exporterKey, exporter));
            }
            else
            {
                problems.Add(new(EvaluationPlanProblemKind.DestinationUnavailable, null, $"The report exporter '{exporterKey}' is not registered."));
            }
        }

        return problems.Count > 0
            ? throw new EvaluationPlanRejectedException(problems.ToImmutable())
            : new ValidatedEvaluationPlan(agents.ToImmutable(), evaluatorMap.ToImmutable(), store, resolvedExporters.ToImmutable());
    }
}
