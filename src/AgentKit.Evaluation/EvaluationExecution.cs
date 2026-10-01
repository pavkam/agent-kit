// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Orchestrates one evaluation run: validation, bounded scheduling, recording, and publication.</summary>
/// <remarks>
/// <para>
/// Validation resolves every collaborator and fails with <see cref="EvaluationPlanRejectedException"/> before any session,
/// run, store, or exporter effect. Scheduling then hands case repetitions to a bounded set of workers in plan order, so the
/// report is deterministic by plan case position and repetition even though execution is parallel.
/// </para>
/// <para>
/// Cancellation stops scheduling new repetitions, flows to active runs and evaluators, and still returns a truthful partial
/// report. It never deletes results already persisted. Exporters are separate effects: they are isolated, never alter a
/// recorded result, and are skipped (and recorded as cancelled) when the caller cancelled.
/// </para>
/// </remarks>
internal static class EvaluationExecution
{
    /// <summary>Runs one plan.</summary>
    /// <param name="engine">The one engine whose public surface runs every case.</param>
    /// <param name="plan">The plan to run.</param>
    /// <param name="evaluators">The evaluator catalog.</param>
    /// <param name="stores">The result-store selector.</param>
    /// <param name="exporters">The exporter catalog.</param>
    /// <param name="evaluationRunIds">The generator of the run identity.</param>
    /// <param name="timeProvider">The injected clock used for timestamps, timeouts, and durations.</param>
    /// <param name="options">The immutable runner caps.</param>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The report, partial when the run was cancelled, expired, or stopped.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="EvaluationPlanRejectedException">The plan is incompatible with the composition; no effect occurred.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled before any effect occurred.</exception>
    internal static async Task<EvaluationReport> RunAsync(
        AgentEngine engine,
        EvaluationPlan plan,
        IEvaluatorCatalog evaluators,
        IEvaluationResultStoreSelector stores,
        IEvaluationReportExporterCatalog exporters,
        IIdentifierGenerator<EvaluationRunId> evaluationRunIds,
        TimeProvider timeProvider,
        EvaluationOptionsSnapshot options,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(evaluators);
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(exporters);
        ArgumentNullException.ThrowIfNull(evaluationRunIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        cancellationToken.ThrowIfCancellationRequested();

        var runId = evaluationRunIds.Create();
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default, nameof(evaluationRunIds));
        var startedAt = timeProvider.GetUtcNow();
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.EvaluationRun,
            ActivityKind.Internal,
            [
                new(AgentKitTagNames.EvaluationRunId, runId.ToString()),
                new(AgentKitTagNames.EvaluationPlanId, plan.Id.Value),
                new(AgentKitTagNames.EvaluationPlanVersion, plan.Version.Value),
            ]);

        ValidatedEvaluationPlan validated;
        try
        {
            validated = await EvaluationPlanValidator.ValidateAsync(engine, plan, evaluators, stores, exporters, options, cancellationToken).ConfigureAwait(false);
        }
        catch (EvaluationPlanRejectedException rejected)
        {
            EvaluationObservation.Safe(() => scope.Activity.SetFailed("rejected", nameof(EvaluationPlanRejectedException)));
            EvaluationObservation.Safe(() => EvaluationLog.PlanRejected(logger, runId, plan.Id, rejected.Problems.Length));
            EvaluationObservation.Safe(() => EvaluationMetrics.RecordRun("rejected"));
            throw;
        }
        catch (OperationCanceledException)
        {
            EvaluationObservation.Safe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            EvaluationObservation.Safe(() => EvaluationMetrics.RecordRun("cancelled"));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            EvaluationObservation.Safe(() => scope.Activity.SetFailed("faulted", errorType));
            EvaluationObservation.Safe(() => EvaluationMetrics.RecordRun("faulted"));
            throw;
        }

        var policy = plan.Execution;
        var repetitions = policy.Repetitions;
        var total = plan.Cases.Length * repetitions;
        EvaluationObservation.Safe(() => EvaluationLog.RunStarted(logger, runId, plan.Id, plan.Version, total));

        using var deadline = policy.PlanDeadline is { } limit ? new CancellationTokenSource(limit, timeProvider) : null;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline?.Token ?? CancellationToken.None);
        var context = new EvaluationCaseRunContext(
            engine, runId, plan, validated, timeProvider, policy.CaseTimeout ?? options.DefaultCaseTimeout, options.DefaultCaseTimeout, logger);
        var results = new EvaluationCaseResult?[total];
        var appends = new EvaluationStoreAppendRecord?[total];
        var next = -1;
        var stopped = 0;

        async Task WorkerAsync()
        {
            while (!linked.IsCancellationRequested && Volatile.Read(ref stopped) == 0)
            {
                var index = Interlocked.Increment(ref next);
                if (index >= total)
                {
                    return;
                }

                var outcome = await EvaluationCaseRunner.RunAsync(context, index / repetitions, (index % repetitions) + 1, linked.Token).ConfigureAwait(false);
                results[index] = outcome.Result;
                appends[index] = outcome.Store;
                if (outcome.EvaluatorFaulted && policy.StopOnEvaluatorFailure)
                {
                    Volatile.Write(ref stopped, 1);
                }
            }
        }

        var workers = Math.Min(policy.MaximumConcurrentCases, total);
        await Task.WhenAll(Enumerable.Range(0, workers).Select(_ => WorkerAsync())).ConfigureAwait(false);

        var recorded = results.Where(static result => result is not null).Select(static result => result!).ToImmutableArray();
        var notStarted = total - recorded.Length;
        var status = Status(deadline, linked, recorded, notStarted, Volatile.Read(ref stopped) != 0, cancellationToken);
        var report = new EvaluationReport(
            runId,
            plan.Id,
            plan.Version,
            startedAt,
            timeProvider.GetUtcNow(),
            status,
            recorded,
            notStarted,
            [.. appends.Where(static append => append is not null).Select(static append => append!)],
            []);

        var exports = await ExportAsync(context, report, status == EvaluationReportStatus.Cancelled).ConfigureAwait(false);
        report = report.WithExportResults(exports);

        var statusName = EvaluationObservation.Name(status);
        EvaluationObservation.Safe(() => scope.Activity.SetSuccessful(statusName));
        EvaluationObservation.Safe(() => EvaluationLog.RunCompleted(
            logger,
            status == EvaluationReportStatus.Completed ? LogLevel.Information : LogLevel.Warning,
            runId,
            plan.Id,
            statusName,
            recorded.Length,
            notStarted));
        EvaluationObservation.Safe(() => EvaluationMetrics.RecordRun(statusName));
        return report;
    }

    private static EvaluationReportStatus Status(
        CancellationTokenSource? deadline,
        CancellationTokenSource linked,
        ImmutableArray<EvaluationCaseResult> recorded,
        int notStarted,
        bool stopped,
        CancellationToken caller)
    {
        var interrupted = linked.IsCancellationRequested
            && (notStarted > 0 || recorded.Any(static result => result.Disposition == EvaluationCaseDisposition.Cancelled));
        var deadlineExpired = !caller.IsCancellationRequested && deadline is { IsCancellationRequested: true };
        return interrupted
            ? deadlineExpired ? EvaluationReportStatus.DeadlineExceeded : EvaluationReportStatus.Cancelled
            : stopped && notStarted > 0 ? EvaluationReportStatus.StoppedOnEvaluatorFailure : EvaluationReportStatus.Completed;
    }

    private static async ValueTask<ImmutableArray<EvaluationExportRecord>> ExportAsync(
        EvaluationCaseRunContext context,
        EvaluationReport report,
        bool skip)
    {
        var records = ImmutableArray.CreateBuilder<EvaluationExportRecord>();
        foreach (var (key, exporter) in context.Validated.Exporters)
        {
            using var scope = AgentKitActivityScope.Start(
                AgentKitActivityNames.EvaluationExport,
                ActivityKind.Internal,
                [
                    new(AgentKitTagNames.EvaluationRunId, context.RunId.ToString()),
                    new(AgentKitTagNames.EvaluationExporterKey, key.Value),
                ]);
            EvaluationExportResult answer;
            if (skip)
            {
                answer = new EvaluationExportRejected(EvaluationExportFailureKind.Cancelled, "The run was cancelled, so the report was not published.");
            }
            else
            {
                using var bound = new CancellationTokenSource(context.RecordingTimeout, context.Time);
                try
                {
                    answer = await exporter.ExportAsync(report, bound.Token).ConfigureAwait(false)
                        ?? new EvaluationExportRejected(EvaluationExportFailureKind.Faulted, "The exporter returned no result.");
                }
                catch (OperationCanceledException)
                {
                    answer = new EvaluationExportRejected(EvaluationExportFailureKind.Cancelled, "The export was cancelled before the exporter finished.");
                }
                catch (Exception)
                {
                    answer = new EvaluationExportRejected(EvaluationExportFailureKind.Faulted, "The exporter threw an exception.");
                }
            }

            var outcome = EvaluationObservation.Name(answer);
            EvaluationObservation.Safe(() =>
            {
                if (answer is EvaluationExported)
                {
                    scope.Activity.SetSuccessful(outcome);
                }
                else
                {
                    scope.Activity.SetFailed(outcome, outcome);
                }
            });
            EvaluationObservation.Safe(() => EvaluationLog.ExportCompleted(
                context.Logger, answer is EvaluationExported ? LogLevel.Information : LogLevel.Warning, context.RunId, key.Value, outcome));
            EvaluationObservation.Safe(() => EvaluationMetrics.RecordExport(key.Value, outcome));
            records.Add(new EvaluationExportRecord(key, answer));
        }

        return records.ToImmutable();
    }
}
