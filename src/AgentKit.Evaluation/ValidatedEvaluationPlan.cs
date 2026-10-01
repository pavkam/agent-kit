// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Holds the exact collaborators resolved for one plan before any effect occurs.</summary>
/// <remarks>The values are borrowed from the composition that owns them and are never disposed by the runner.</remarks>
internal sealed class ValidatedEvaluationPlan
{
    /// <summary>Initializes the resolved collaborators.</summary>
    /// <param name="agents">The engine agent handles by identity.</param>
    /// <param name="evaluators">The registered evaluators by key.</param>
    /// <param name="store">The selected result store, or <see langword="null"/> when the plan persists nothing.</param>
    /// <param name="exporters">The resolved exporters in plan order.</param>
    internal ValidatedEvaluationPlan(
        ImmutableDictionary<AgentId, Agent> agents,
        ImmutableDictionary<EvaluatorKey, IEvaluator> evaluators,
        IEvaluationResultStore? store,
        ImmutableArray<KeyValuePair<EvaluationReportExporterKey, IEvaluationReportExporter>> exporters)
    {
        Debug.Assert(agents is not null && evaluators is not null, "The validator supplies resolved collaborators.");
        Agents = agents;
        Evaluators = evaluators;
        Store = store;
        Exporters = exporters;
    }

    /// <summary>Gets the engine agent handles by identity.</summary>
    internal ImmutableDictionary<AgentId, Agent> Agents { get; }

    /// <summary>Gets the registered evaluators by key.</summary>
    internal ImmutableDictionary<EvaluatorKey, IEvaluator> Evaluators { get; }

    /// <summary>Gets the selected result store, or <see langword="null"/>.</summary>
    internal IEvaluationResultStore? Store { get; }

    /// <summary>Gets the resolved exporters in plan order.</summary>
    internal ImmutableArray<KeyValuePair<EvaluationReportExporterKey, IEvaluationReportExporter>> Exporters { get; }
}
