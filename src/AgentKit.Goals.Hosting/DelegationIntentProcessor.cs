// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting;

/// <summary>Drains one committed delegation intent: claims the child attempt durably, runs it, and settles the result.</summary>
/// <remarks>
/// <para>
/// The processor is idempotent over durable state. It claims a ready child with one atomic transition that records the attempt
/// before anything runs, so a duplicated signal, a rescan, or a second worker finds the child already active and never starts a
/// second run. The claim is what makes execution at-most-once per attempt; a crash after the claim leaves a running attempt
/// that the next incarnation settles as failed with unknown effects rather than silently rerunning it, because the effects of
/// the lost run are unknown.
/// </para>
/// <para>
/// The delegation deadline is enforced by cancelling the run, and a worker that is stopping settles the claimed attempt as
/// failed with unknown effects. Every mutation goes through the goal coordinator under the delegation's captured authorization,
/// so the worker never widens authority and never writes a store directly.
/// </para>
/// </remarks>
internal sealed class DelegationIntentProcessor
{
    private readonly IGoalCoordinator _goals;
    private readonly IDelegationChildRunner _runner;
    private readonly DelegationWorkerSlots _slots;
    private readonly TimeProvider _time;
    private readonly ILogger<DelegationIntentProcessor> _logger;
    private readonly ConcurrentDictionary<GoalId, byte> _inFlight = new();
    private readonly DateTimeOffset _startedAt;

    /// <summary>Initializes the processor.</summary>
    /// <param name="goals">The goal coordinator every mutation goes through.</param>
    /// <param name="runner">The runner that provisions sessions and executes claimed attempts.</param>
    /// <param name="slots">The slot pool that bounds concurrent children.</param>
    /// <param name="time">The injected clock for the deadline and the incarnation start.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public DelegationIntentProcessor(
        IGoalCoordinator goals,
        IDelegationChildRunner runner,
        DelegationWorkerSlots slots,
        TimeProvider time,
        ILogger<DelegationIntentProcessor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(time);
        _goals = goals;
        _runner = runner;
        _slots = slots;
        _time = time;
        _logger = logger ?? NullLogger<DelegationIntentProcessor>.Instance;
        _startedAt = time.GetUtcNow();
    }

    /// <summary>Drains one intent to a bounded outcome.</summary>
    /// <param name="request">The canonical delegation of the child.</param>
    /// <param name="childGoalId">The child goal.</param>
    /// <param name="cancellationToken">Stops the worker; a stop settles a claimed attempt before propagating.</param>
    /// <returns>A bounded outcome name used for metrics and diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="childGoalId"/> is default.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    internal async Task<string> ProcessAsync(DelegationRequest request, GoalId childGoalId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfEqual(childGoalId, default);
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DelegationWorkerDrain,
            ActivityKind.Internal,
            [
                new(AgentKitTagNames.GoalId, childGoalId.ToString()),
                new(AgentKitTagNames.DelegationId, request.Id.ToString()),
                new(AgentKitTagNames.AgentId, request.TargetAgentId.ToString()),
                new(AgentKitTagNames.TenantId, request.Authorization.Identity.TenantId.Value),
            ]);
        string outcome;
        var owned = _inFlight.TryAdd(childGoalId, 0);
        try
        {
            outcome = owned
                ? await DrainAsync(request, childGoalId, cancellationToken).ConfigureAwait(false)
                : "duplicate";
        }
        catch (OperationCanceledException)
        {
            WorkerObservation.Safe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            WorkerObservation.Safe(() => GoalWorkerMetrics.RecordDrain("cancelled"));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            WorkerObservation.Safe(() => scope.Activity.SetFailed("faulted", errorType));
            WorkerObservation.Safe(() => GoalWorkerLog.DrainFaulted(_logger, childGoalId, errorType));
            WorkerObservation.Safe(() => GoalWorkerMetrics.RecordDrain("faulted"));
            throw;
        }
        finally
        {
            if (owned)
            {
                _ = _inFlight.TryRemove(childGoalId, out _);
            }
        }

        WorkerObservation.Safe(() => scope.Activity.SetSuccessful(outcome));
        WorkerObservation.Safe(() => GoalWorkerMetrics.RecordDrain(outcome));
        return outcome;
    }

    private async Task<string> DrainAsync(DelegationRequest request, GoalId childGoalId, CancellationToken cancellationToken)
    {
        var reference = new GoalProfileReference(request.ProfileKey, request.ProfileVersion);
        await using var lease = await _slots.AcquireAsync(cancellationToken).ConfigureAwait(false);
        var loaded = await _goals.LoadAsync(new GoalLoadCommand(reference, childGoalId, request.Authorization), cancellationToken).ConfigureAwait(false);
        if (loaded is not GoalLoaded { Record: var record })
        {
            return Skipped(childGoalId, "unavailable");
        }

        var now = _time.GetUtcNow();
        switch (record.Goal.Status)
        {
            case GoalStatus.Ready:
                break;
            case GoalStatus.Active when record.ActiveAttempt is { } running && (running.StartedAt < _startedAt || request.Deadline <= now):
                return await RecoverStaleAsync(request, reference, record, running, cancellationToken).ConfigureAwait(false);
            case GoalStatus.Proposed:
            case GoalStatus.Active:
            case GoalStatus.Waiting:
            case GoalStatus.Completed:
            case GoalStatus.Failed:
            case GoalStatus.Cancelled:
            case GoalStatus.Blocked:
            default:
                return Skipped(childGoalId, "not_ready");
        }

        if (request.Deadline <= now)
        {
            return await FailWithoutAttemptAsync(request, reference, record, GoalTransitionReason.DeadlineExceeded, "expired", cancellationToken).ConfigureAwait(false);
        }

        var attemptNumber = record.Attempts.Length + 1;
        var session = await _runner.ProvisionSessionAsync(request, childGoalId, attemptNumber, cancellationToken).ConfigureAwait(false);
        if (session is not { } sessionId)
        {
            return await FailWithoutAttemptAsync(request, reference, record, GoalTransitionReason.AttemptFailed, "unprovisioned", cancellationToken).ConfigureAwait(false);
        }

        var claimed = await _goals.StartAttemptAsync(
            new GoalAttemptRequest(
                reference, childGoalId, record.Goal.Version, attemptNumber, attemptId: null, request.TargetAgentId, sessionId, new RunId(Guid.NewGuid()),
                request.ParentRunId, request.OperationId, TransitionActor.Worker, request.Budget,
                new IdempotencyKey($"agentkit.goals.claim:{childGoalId}:{attemptNumber}"), request.Authorization),
            cancellationToken).ConfigureAwait(false);
        if (claimed is not GoalAttemptStarted { Record: var active, Attempt: var attempt })
        {
            return Skipped(childGoalId, "claim_lost");
        }

        lease.Bind(sessionId);
        WorkerObservation.Safe(() => GoalWorkerLog.AttemptClaimed(_logger, childGoalId, attemptNumber, request.TargetAgentId));
        using var deadline = new CancellationTokenSource(request.Deadline - _time.GetUtcNow(), _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        DelegationChildRunResult result;
        try
        {
            result = await _runner.RunAsync(
                new DelegationChildRunRequest(request, childGoalId, sessionId, attempt.Id), linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _ = await SettleAsync(request, reference, active, attempt, Unknown(DelegationStatus.Failed), GoalTransitionReason.LeaseLost, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (OperationCanceledException)
        {
            result = Unknown(DelegationStatus.Failed);
            return await SettleAsync(request, reference, active, attempt, result, GoalTransitionReason.DeadlineExceeded, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            WorkerObservation.Safe(() => GoalWorkerLog.DrainFaulted(_logger, childGoalId, errorType));
            result = Unknown(DelegationStatus.Failed);
            return await SettleAsync(request, reference, active, attempt, result, GoalTransitionReason.AttemptFailed, cancellationToken).ConfigureAwait(false);
        }

        var reason = result.Status switch
        {
            DelegationStatus.Succeeded => GoalTransitionReason.Completed,
            DelegationStatus.Cancelled when deadline.IsCancellationRequested => GoalTransitionReason.DeadlineExceeded,
            DelegationStatus.Blocked => GoalTransitionReason.AuthorityRequired,
            DelegationStatus.Cancelled => GoalTransitionReason.Cancelled,
            DelegationStatus.Failed or DelegationStatus.Dispatched => GoalTransitionReason.AttemptFailed,
            _ => GoalTransitionReason.AttemptFailed,
        };
        return await SettleAsync(request, reference, active, attempt, result, reason, cancellationToken).ConfigureAwait(false);
    }

    private static DelegationChildRunResult Unknown(DelegationStatus status) =>
        new(runId: null, status, summary: null, GoalBudgetUsage.None, SideEffectCertainty.Unknown);

    private string Skipped(GoalId childGoalId, string reason)
    {
        WorkerObservation.Safe(() => GoalWorkerLog.IntentSkipped(_logger, childGoalId, reason));
        return "skipped";
    }

    private async Task<string> SettleAsync(
        DelegationRequest request,
        GoalProfileReference reference,
        GoalRecord active,
        GoalAttempt attempt,
        DelegationChildRunResult result,
        GoalTransitionReason reason,
        CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var status = result.Status;
        var attemptStatus = status switch
        {
            DelegationStatus.Succeeded => GoalAttemptStatus.Succeeded,
            DelegationStatus.Cancelled => GoalAttemptStatus.Cancelled,
            DelegationStatus.Blocked => GoalAttemptStatus.Blocked,
            DelegationStatus.Failed or DelegationStatus.Dispatched => GoalAttemptStatus.Failed,
            _ => GoalAttemptStatus.Failed,
        };
        var target = status switch
        {
            DelegationStatus.Succeeded => GoalStatus.Completed,
            DelegationStatus.Cancelled => GoalStatus.Cancelled,
            DelegationStatus.Blocked => GoalStatus.Blocked,
            DelegationStatus.Failed or DelegationStatus.Dispatched => GoalStatus.Failed,
            _ => GoalStatus.Failed,
        };
        var outcome = new GoalOutcomeReference(
            result.RunId ?? attempt.RunId,
            status,
            status == DelegationStatus.Succeeded && result.Summary is { } summary ? new StructuredGoalResult(summary, ExtensionData.Empty) : null,
            [],
            result.Usage,
            result.SideEffectCertainty);
        var transition = new GoalTransition(
            active.Goal.Id, active.Goal.OwnerAgentId, active.Goal.SessionId, request.ParentRunId, request.OperationId, GoalStatus.Active, target,
            TransitionActor.Worker, reason, active.Goal.Version, new IdempotencyKey($"agentkit.goals.settle:{active.Goal.Id}:{attempt.Number}"), now);
        var settled = await _goals.TransitionAsync(
            new GoalTransitionCommand(reference, transition, new GoalAttemptSettlement(attempt.Id, attemptStatus, outcome, now), request.Authorization),
            cancellationToken).ConfigureAwait(false);
        if (settled is GoalTransitionRejected)
        {
            return Skipped(active.Goal.Id, "settle_rejected");
        }

        WorkerObservation.Safe(() => GoalWorkerLog.ChildSettled(_logger, active.Goal.Id, status));
        return status switch
        {
            DelegationStatus.Succeeded => "completed",
            DelegationStatus.Cancelled => "cancelled",
            DelegationStatus.Blocked => "blocked",
            DelegationStatus.Failed or DelegationStatus.Dispatched => "failed",
            _ => "failed",
        };
    }

    private async Task<string> RecoverStaleAsync(DelegationRequest request, GoalProfileReference reference, GoalRecord record, GoalAttempt running, CancellationToken cancellationToken)
    {
        WorkerObservation.Safe(() => GoalWorkerLog.StaleAttemptRecovered(_logger, record.Goal.Id));
        var outcome = await SettleAsync(request, reference, record, running, Unknown(DelegationStatus.Failed), GoalTransitionReason.LeaseLost, cancellationToken).ConfigureAwait(false);
        return outcome == "failed" ? "recovered" : outcome;
    }

    private async Task<string> FailWithoutAttemptAsync(
        DelegationRequest request,
        GoalProfileReference reference,
        GoalRecord record,
        GoalTransitionReason reason,
        string outcome,
        CancellationToken cancellationToken)
    {
        var transition = new GoalTransition(
            record.Goal.Id, record.Goal.OwnerAgentId, record.Goal.SessionId, request.ParentRunId, request.OperationId, GoalStatus.Ready, GoalStatus.Failed,
            TransitionActor.Worker, reason, record.Goal.Version, new IdempotencyKey($"agentkit.goals.fail:{record.Goal.Id}:{record.Attempts.Length + 1}"), _time.GetUtcNow());
        _ = await _goals.TransitionAsync(new GoalTransitionCommand(reference, transition, null, request.Authorization), cancellationToken).ConfigureAwait(false);
        WorkerObservation.Safe(() => GoalWorkerLog.ChildSettled(_logger, record.Goal.Id, DelegationStatus.Failed));
        return outcome;
    }
}
