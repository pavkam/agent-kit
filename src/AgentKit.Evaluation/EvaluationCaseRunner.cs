// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Runs one case repetition through the public engine, evaluates it, and records the result.</summary>
/// <remarks>
/// <para>
/// Every repetition gets a fresh session so repetitions never share history. The runner observes only the public result of
/// <see cref="AgentEngine.RunAsync{TOutput}"/>; it never touches the loop or run scope. Session creation and admission
/// rejections, cancellation, timeout, and unexpected engine exceptions each produce a typed disposition instead of an
/// exception, so one bad repetition never hides the others.
/// </para>
/// <para>The recording write uses its own bounded token, independent of the caller, so evidence already computed is never lost to a cancellation that arrives afterwards.</para>
/// </remarks>
internal static class EvaluationCaseRunner
{
    /// <summary>Runs one repetition.</summary>
    /// <param name="context">The shared run collaborators.</param>
    /// <param name="ordinal">The zero-based case position in the plan.</param>
    /// <param name="repetition">The one-based repetition.</param>
    /// <param name="runToken">The run-wide token combining caller cancellation and the plan deadline.</param>
    /// <returns>The recorded outcome; never throws for an engine, evaluator, or store failure.</returns>
    internal static async ValueTask<EvaluationCaseOutcome> RunAsync(
        EvaluationCaseRunContext context,
        int ordinal,
        int repetition,
        CancellationToken runToken)
    {
        Debug.Assert(context is not null, "The scheduler supplies the shared context.");
        Debug.Assert(ordinal >= 0 && ordinal < context.Plan.Cases.Length, "The scheduler derives a valid ordinal.");
        Debug.Assert(repetition >= 1, "Repetitions are one-based.");
        var evaluationCase = context.Plan.Cases[ordinal];
        var agent = context.Validated.Agents[evaluationCase.AgentId];
        var startedAt = context.Time.GetUtcNow();
        var startedTimestamp = context.Time.GetTimestamp();
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.EvaluationCase,
            ActivityKind.Internal,
            [
                new(AgentKitTagNames.EvaluationRunId, context.RunId.ToString()),
                new(AgentKitTagNames.EvaluationPlanId, context.Plan.Id.Value),
                new(AgentKitTagNames.EvaluationCaseId, evaluationCase.Id.Value),
                new(AgentKitTagNames.EvaluationCaseRepetition, repetition),
                new(AgentKitTagNames.AgentId, evaluationCase.AgentId.ToString()),
            ]);
        var traceId = scope.Activity is { } activity ? activity.TraceId.ToString() : null;
        using var timeout = new CancellationTokenSource(context.CaseTimeout, context.Time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(runToken, timeout.Token);
        var token = linked.Token;

        var diagnostics = new List<EvaluationDiagnostic>();
        var evaluatorResults = new List<EvaluatorResult>();
        var disposition = EvaluationCaseDisposition.Evaluated;
        EvaluationRunRecord? runRecord = null;
        AgentRunFinished<ValidatedOutput>? finished = null;
        try
        {
            token.ThrowIfCancellationRequested();
            var created = await context.Engine.CreateSessionAsync(
                new AgentSessionCreateRequest(
                    evaluationCase.AgentId,
                    evaluationCase.Execution.Identity,
                    conversationId: null,
                    new IdempotencyKey($"agentkit.evaluation:{context.RunId}:{evaluationCase.Id.Value}:{repetition}"),
                    ExtensionData.Empty),
                token).ConfigureAwait(false);
            if (created is AgentSessionCreated session)
            {
                var result = await context.Engine.RunAsync<ValidatedOutput>(
                    new AgentRunRequest(
                        evaluationCase.AgentId,
                        session.SessionId,
                        session.ConversationId,
                        evaluationCase.Execution.Identity,
                        evaluationCase.Input,
                        evaluationCase.RunOptions),
                    token).ConfigureAwait(false);
                if (result is AgentRunFinished<ValidatedOutput> run)
                {
                    finished = run;
                    runRecord = new EvaluationRunRecord(
                        run.RunId,
                        run.SessionId,
                        EvaluationEvidenceBuilder.OutcomeName(run.Outcome),
                        EvaluationEvidenceBuilder.SettlementName(run.Settlement));
                }
                else if (result is AgentRunRejected<ValidatedOutput> rejected)
                {
                    disposition = EvaluationCaseDisposition.RunRejected;
                    diagnostics.Add(new EvaluationDiagnostic("run_rejected", $"The engine rejected the run with code '{rejected.Failure.Code}'."));
                }
                else
                {
                    disposition = EvaluationCaseDisposition.Faulted;
                    diagnostics.Add(new EvaluationDiagnostic("run_result_unknown", "The engine returned a result shape the runner does not recognize."));
                }
            }
            else
            {
                disposition = EvaluationCaseDisposition.RunRejected;
                var failure = (created as AgentSessionCreationFailed)?.Failure;
                diagnostics.Add(new EvaluationDiagnostic(
                    "session_rejected",
                    failure is null ? "The engine did not create the case session." : $"The engine refused to create the case session: {failure.Kind}."));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            disposition = Classify(timeout, runToken);
        }
        catch (Exception exception)
        {
            disposition = EvaluationCaseDisposition.Faulted;
            var errorType = ErrorType(exception);
            diagnostics.Add(new EvaluationDiagnostic("engine_exception", $"The engine threw {errorType} while running the case."));
            EvaluationObservation.Safe(() => EvaluationLog.CaseFaulted(context.Logger, context.RunId, evaluationCase.Id, repetition, errorType));
        }

        var manifest = EvaluationEvidenceBuilder.Manifest(agent, finished?.Usage);
        var usage = finished is null ? EvaluationUsageSummary.None : EvaluationEvidenceBuilder.Usage(finished.Usage);
        var latencyBeforeEvaluation = context.Time.GetElapsedTime(startedTimestamp);
        var evaluatorFaulted = false;
        if (finished is not null && disposition == EvaluationCaseDisposition.Evaluated)
        {
            if (token.IsCancellationRequested)
            {
                disposition = Classify(timeout, runToken);
            }
            else
            {
                var evaluationContext = new EvaluationContext(
                    context.RunId, context.Plan.Id, context.Plan.Version, evaluationCase, repetition, finished, manifest, usage, latencyBeforeEvaluation);
                foreach (var reference in evaluationCase.Evaluators)
                {
                    var evaluator = context.Validated.Evaluators[reference.Key];
                    var evaluated = await EvaluateOneAsync(context, evaluator, evaluationContext, token).ConfigureAwait(false);
                    evaluatorResults.Add(evaluated);
                    evaluatorFaulted |= evaluated.Outcome is EvaluatorFaulted;
                }

                if (token.IsCancellationRequested && evaluatorResults.Any(static result => result.Outcome is EvaluationCancelled))
                {
                    disposition = Classify(timeout, runToken);
                }
            }
        }

        var latency = context.Time.GetElapsedTime(startedTimestamp);
        var caseResult = new EvaluationCaseResult(
            context.RunId,
            context.Plan.Id,
            context.Plan.Version,
            evaluationCase.Id,
            ordinal,
            repetition,
            disposition,
            startedAt,
            latency,
            traceId,
            runRecord,
            manifest,
            usage,
            evaluationCase.Fixture,
            [.. evaluatorResults],
            [.. diagnostics]);

        var storeRecord = await RecordAsync(context, caseResult).ConfigureAwait(false);
        var dispositionName = EvaluationObservation.Name(disposition);
        var verdictName = EvaluationObservation.Name(caseResult.Verdict);
        EvaluationObservation.Safe(() =>
        {
            if (disposition == EvaluationCaseDisposition.Evaluated)
            {
                scope.Activity.SetSuccessful(dispositionName);
            }
            else
            {
                scope.Activity.SetFailed(dispositionName, dispositionName);
            }
        });
        EvaluationObservation.Safe(() => EvaluationLog.CaseCompleted(
            context.Logger,
            disposition == EvaluationCaseDisposition.Evaluated ? LogLevel.Information : LogLevel.Warning,
            context.RunId,
            evaluationCase.Id,
            repetition,
            dispositionName,
            verdictName));
        EvaluationObservation.Safe(() => EvaluationMetrics.RecordCase(dispositionName, latency));
        return new EvaluationCaseOutcome(caseResult, storeRecord, evaluatorFaulted);
    }

    private static async ValueTask<EvaluatorResult> EvaluateOneAsync(
        EvaluationCaseRunContext context,
        IEvaluator evaluator,
        EvaluationContext evaluationContext,
        CancellationToken token)
    {
        var descriptor = evaluator.Descriptor;
        var key = descriptor.Key.Value;
        var evaluationCase = evaluationContext.Case;
        var started = context.Time.GetTimestamp();
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.EvaluationEvaluate,
            ActivityKind.Internal,
            [
                new(AgentKitTagNames.EvaluationRunId, context.RunId.ToString()),
                new(AgentKitTagNames.EvaluationCaseId, evaluationCase.Id.Value),
                new(AgentKitTagNames.EvaluationCaseRepetition, evaluationContext.Repetition),
                new(AgentKitTagNames.EvaluationEvaluatorKey, key),
            ]);
        EvaluationOutcome outcome;
        if (!descriptor.Supports(evaluationCase.Criteria, evaluationCase.Fixture))
        {
            outcome = new EvaluationUnsupported("The evaluator cannot assess the criteria and fixture of this case.");
        }
        else if (token.IsCancellationRequested)
        {
            outcome = new EvaluationCancelled("The evaluation was cancelled before the evaluator ran.");
        }
        else
        {
            try
            {
                outcome = await evaluator.EvaluateAsync(evaluationContext, token).ConfigureAwait(false)
                    ?? new EvaluatorFaulted(nameof(InvalidOperationException), "The evaluator returned no outcome.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                outcome = new EvaluationCancelled("The evaluation was cancelled while the evaluator ran.");
            }
            catch (Exception exception)
            {
                var errorType = ErrorType(exception);
                outcome = new EvaluatorFaulted(errorType, "The evaluator threw an exception.");
                EvaluationObservation.Safe(() => EvaluationLog.EvaluatorFaulted(context.Logger, context.RunId, evaluationCase.Id, evaluationContext.Repetition, key, errorType));
            }
        }

        var name = outcome.Name;
        EvaluationObservation.Safe(() =>
        {
            if (outcome is EvaluatorFaulted faulted)
            {
                scope.Activity.SetFailed(name, faulted.ErrorType);
            }
            else
            {
                scope.Activity.SetSuccessful(name);
            }
        });
        EvaluationObservation.Safe(() => EvaluationLog.EvaluatorConcluded(context.Logger, context.RunId, evaluationCase.Id, evaluationContext.Repetition, key, name));
        EvaluationObservation.Safe(() => EvaluationMetrics.RecordEvaluator(key, name));
        return new EvaluatorResult(descriptor.Key, descriptor.Version, outcome, context.Time.GetElapsedTime(started));
    }

    private static async ValueTask<EvaluationStoreAppendRecord?> RecordAsync(EvaluationCaseRunContext context, EvaluationCaseResult result)
    {
        if (context.Validated.Store is not { } store)
        {
            return null;
        }

        EvaluationStoreResult answer;
        using var bound = new CancellationTokenSource(context.RecordingTimeout, context.Time);
        try
        {
            answer = await store.AppendAsync(result, bound.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            answer = new EvaluationStoreRejected(new EvaluationStoreFailure(EvaluationStoreFailureKind.Unavailable, "The recording write was cancelled before the store acknowledged it."));
        }
        catch (Exception)
        {
            answer = new EvaluationStoreRejected(new EvaluationStoreFailure(EvaluationStoreFailureKind.Unavailable, "The result store threw while recording the result."));
        }

        if (answer is EvaluationStoreRejected rejected)
        {
            var failure = rejected.Failure.Kind.ToString();
            EvaluationObservation.Safe(() => EvaluationLog.StoreAppendFailed(context.Logger, context.RunId, result.CaseId, result.Repetition, failure));
        }

        return new EvaluationStoreAppendRecord(result.CaseId, result.Repetition, answer);
    }

    private static EvaluationCaseDisposition Classify(CancellationTokenSource timeout, CancellationToken runToken) =>
        runToken.IsCancellationRequested || !timeout.IsCancellationRequested
            ? EvaluationCaseDisposition.Cancelled
            : EvaluationCaseDisposition.TimedOut;

    private static string ErrorType(Exception exception) => exception.GetType().FullName ?? exception.GetType().Name;
}
