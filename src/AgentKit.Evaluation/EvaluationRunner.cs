// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Is the first-party <see cref="IEvaluationRunner"/>: a thread-safe singleton bound to one <see cref="AgentEngine"/>.</summary>
/// <remarks>
/// The runner owns no mutable state between calls and creates bounded case work only inside <see cref="RunAsync"/>. It relies on
/// the bound engine to create one scope per agent run, and it receives no loop, provider SDK, raw service provider, or
/// separate definition catalog. Direct implementations of <see cref="IEvaluationRunner"/> remain supported.
/// </remarks>
internal sealed class EvaluationRunner: IEvaluationRunner
{
    private readonly AgentEngine _engine;
    private readonly IEvaluatorCatalog _evaluators;
    private readonly IEvaluationResultStoreSelector _stores;
    private readonly IEvaluationReportExporterCatalog _exporters;
    private readonly IIdentifierGenerator<EvaluationRunId> _evaluationRunIds;
    private readonly TimeProvider _timeProvider;
    private readonly EvaluationOptionsSnapshot _options;
    private readonly ILogger _logger;

    /// <summary>Initializes the runner over one engine and its explicit collaborators.</summary>
    /// <param name="engine">The one engine every case runs through.</param>
    /// <param name="evaluators">The evaluator catalog.</param>
    /// <param name="stores">The result-store selector.</param>
    /// <param name="exporters">The exporter catalog.</param>
    /// <param name="evaluationRunIds">The generator of one identity per execution.</param>
    /// <param name="timeProvider">The injected clock.</param>
    /// <param name="options">The immutable caps captured for the runner lifetime.</param>
    /// <param name="logger">The content-free logger.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal EvaluationRunner(
        AgentEngine engine,
        IEvaluatorCatalog evaluators,
        IEvaluationResultStoreSelector stores,
        IEvaluationReportExporterCatalog exporters,
        IIdentifierGenerator<EvaluationRunId> evaluationRunIds,
        TimeProvider timeProvider,
        EvaluationOptionsSnapshot options,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(evaluators);
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(exporters);
        ArgumentNullException.ThrowIfNull(evaluationRunIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _engine = engine;
        _evaluators = evaluators;
        _stores = stores;
        _exporters = exporters;
        _evaluationRunIds = evaluationRunIds;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<EvaluationReport> RunAsync(EvaluationPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return EvaluationExecution.RunAsync(
            _engine, plan, _evaluators, _stores, _exporters, _evaluationRunIds, _timeProvider, _options, _logger, cancellationToken);
    }
}
