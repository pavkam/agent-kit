// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Applies the canonical strict precedence to one immutable continuation snapshot.</summary>
/// <remarks>This stateless policy performs no I/O and returns proposals only; the session owner revalidates and commits transitions.</remarks>
internal sealed class DefaultRunContinuationPolicy: IRunContinuationPolicy
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultRunContinuationPolicy> _logger;

    /// <summary>Initializes the deterministic policy and its observation dependencies.</summary>
    /// <param name="timeProvider">The injected clock used only to measure policy duration.</param>
    /// <param name="logger">The optional structured logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    public DefaultRunContinuationPolicy(
        TimeProvider timeProvider,
        ILogger<DefaultRunContinuationPolicy>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultRunContinuationPolicy>.Instance;
    }

    /// <inheritdoc/>
    public ValueTask<RunContinuationDecision> DecideAsync(
        RunContinuationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var started = TryGetTimestamp();
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.RunContinuationEvaluate,
            ActivityKind.Internal,
            tags: new ActivityTagsCollection
            {
                { AgentKitTagNames.AgentId, context.AgentId.ToString() },
                { AgentKitTagNames.SessionId, context.SessionId.ToString() },
                { AgentKitTagNames.RunId, context.RunId.ToString() },
                { AgentKitTagNames.OperationId, context.OperationId.ToString() },
                { AgentKitTagNames.ContinuationBoundary, context.Boundary.GetType().Name },
            });
        var activity = activityScope.Activity;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decision = Decide(context);
            var decisionName = decision.GetType().Name;
            _ = activity?.SetTag(AgentKitTagNames.ContinuationDecision, decisionName);
            if (decision is ContinueRun continuation)
            {
                _ = activity?.SetTag(
                    AgentKitTagNames.ContinuationReason,
                    continuation.Reason.SelectedCause.GetType().Name);
            }

            if (decision is HaltRun halt)
            {
                activity.SetFailed(decisionName, halt.Outcome.GetType().Name);
            }
            else
            {
                activity.SetSuccessful(decisionName);
            }
            Observe(context, decisionName, started);
            return ValueTask.FromResult(decision);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            ObserveCancellation(context, started);
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("failed", errorType);
            ObserveFailure(context, errorType, started);
            throw;
        }
    }

    private static RunContinuationDecision Decide(RunContinuationContext context)
    {
        Debug.Assert(context is not null, "The public boundary validates the continuation context.");

        if (context.State is AgentRunState.Cancelling
            or AgentRunState.Failing
            or AgentRunState.Settling
            or AgentRunState.Settled)
        {
            return Invalid($"State '{context.State}' is not an ordinary continuation boundary.");
        }

        if (!IsSafeBoundary(context.State, context.Boundary))
        {
            return Invalid($"State '{context.State}' cannot evaluate boundary '{context.Boundary.GetType().Name}'.");
        }

        if (context.RequiredStopOutcome is { } stop)
        {
            return new HaltRun(stop);
        }

        if (context.Boundary is CommittedTurnContinuationBoundary
            {
                OutputDecision: OutputRejected rejected,
            })
        {
            return new HaltRun(new AgentRunOutputRejected(rejected));
        }

        if (context.Boundary is CommittedTurnContinuationBoundary
            {
                OutputDecision: OutputConfigurationRejected configurationRejected,
            })
        {
            return new HaltRun(new AgentRunOutputRejected(configurationRejected));
        }

        if (context.Boundary is CommittedTurnContinuationBoundary
            {
                RequiresOutputValidation: true,
                OutputDecision: not OutputAccepted and not OutputRetryRequired,
            })
        {
            return Invalid("Required output validation is missing a terminal processor decision.");
        }

        if (context.Boundary is CommittedTurnContinuationBoundary { OutputDecision: OutputRetryRequired retry }
            && !context.Causes.OfType<OutputRepairContinuationCause>().Any(cause => cause.Decision.Equals(retry)))
        {
            return Invalid("An output retry decision requires matching repair continuation evidence.");
        }

        if (context.Boundary is CommittedTurnContinuationBoundary { ToolResults.IsEmpty: false } toolBoundary
            && !context.Causes.OfType<CommittedToolResultsContinuationCause>()
                .Any(cause => cause.ToolResults.SequenceEqual(toolBoundary.ToolResults)))
        {
            return Invalid("Committed tool results require matching continuation evidence.");
        }

        if (!context.Causes.IsEmpty)
        {
            var selected = context.Causes
                .Select(static (cause, index) => (Cause: cause, Index: index, Priority: GetPriority(cause)))
                .OrderBy(static item => item.Priority)
                .ThenBy(static item => item.Index)
                .First();
            var pending = context.Causes.RemoveAt(selected.Index);
            return new ContinueRun(new ContinuationReason(selected.Cause, pending));
        }

        return context.Boundary switch
        {
            CommittedTurnContinuationBoundary { ToolResults.IsEmpty: false } =>
                Invalid("Committed tool results require explicit continuation evidence."),
            CommittedTurnContinuationBoundary { RequiresOutputValidation: true, OutputDecision: not OutputAccepted } =>
                Invalid("Required output validation is missing an accepted terminal decision."),
            CommittedTurnContinuationBoundary { OutputDecision: OutputAccepted accepted } committed =>
                new CompleteRun(new AgentRunCompleted(committed.Response) { Output = accepted.Output }),
            CommittedTurnContinuationBoundary committed =>
                new CompleteRun(new AgentRunCompleted(committed.Response)),
            IdleContinuationBoundary =>
                new CompleteRun(new AgentRunIdle()),
            RetryContinuationBoundary =>
                Invalid("A retry boundary requires explicit retry evidence."),
            DeferredContinuationBoundary =>
                Invalid("A deferred boundary requires matching completion evidence."),
            _ => Invalid("The continuation boundary is not recognized."),
        };
    }

    private static int GetPriority(RunContinuationCause cause) => cause switch
    {
        PromotedInputContinuationCause => 0,
        CommittedToolResultsContinuationCause => 1,
        OutputRepairContinuationCause => 2,
        DeferredCompletionContinuationCause => 3,
        CompactionRetryContinuationCause => 4,
        ExplicitPolicyContinuationCause => 5,
        _ => int.MaxValue,
    };

    private static bool IsSafeBoundary(AgentRunState state, RunContinuationBoundary boundary) =>
        (state, boundary) switch
        {
            (AgentRunState.Driving or AgentRunState.Completing,
                CommittedTurnContinuationBoundary or IdleContinuationBoundary) => true,
            (AgentRunState.WaitingRetry, RetryContinuationBoundary) => true,
            (AgentRunState.SuspendedDeferred, DeferredContinuationBoundary) => true,
            _ => false,
        };

    private static HaltRun Invalid(string safeMessage) =>
        new HaltRun(new AgentRunInvalidState(safeMessage));

    private long? TryGetTimestamp()
    {
        try
        {
            return _timeProvider.GetTimestamp();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void Observe(RunContinuationContext context, string decision, long? started)
    {
        try
        {
            LoopLog.ContinuationEvaluated(_logger, context.RunId, context.Boundary.GetType().Name, decision);
            RecordMetrics(context.Boundary.GetType().Name, decision, started);
        }
        catch (Exception)
        {
            // Observation must never change the semantic proposal.
        }
    }

    private void ObserveCancellation(RunContinuationContext context, long? started)
    {
        try
        {
            LoopLog.ContinuationCancelled(_logger, context.RunId);
            RecordMetrics(context.Boundary.GetType().Name, "cancelled", started);
        }
        catch (Exception)
        {
            // Observation must never replace cancellation.
        }
    }

    private void ObserveFailure(RunContinuationContext context, string errorType, long? started)
    {
        try
        {
            LoopLog.ContinuationFailed(_logger, context.RunId, errorType);
            RecordMetrics(context.Boundary.GetType().Name, "failed", started);
        }
        catch (Exception)
        {
            // Observation must never replace the original failure.
        }
    }

    private void RecordMetrics(string boundary, string outcome, long? started)
    {
        var tags = new TagList
        {
            { AgentKitTagNames.ContinuationBoundary, boundary },
            { AgentKitTagNames.Outcome, outcome },
        };
        LoopMetrics.ContinuationEvaluations.Add(1, tags);
        if (started is { } timestamp)
        {
            LoopMetrics.ContinuationEvaluationDuration.Record(
                _timeProvider.GetElapsedTime(timestamp).TotalSeconds,
                tags);
        }
    }
}
