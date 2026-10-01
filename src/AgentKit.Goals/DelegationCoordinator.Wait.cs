// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

internal sealed partial class DelegationCoordinator
{
    /// <summary>Waits for a dispatched child from durable state, parking the waiting run's occupancy and applying the cancellation relationship if the wait is cancelled.</summary>
    private async Task<DelegationResult> WaitAsync(
        DelegationRequest request,
        DelegationRequest canonical,
        GoalRecord dispatched,
        AcceptanceCriteria criteria,
        GoalProfileReference reference,
        CancellationToken cancellationToken)
    {
        var record = dispatched;
        try
        {
            await using (await _parking.ParkAsync(request.ParentSessionId, cancellationToken).ConfigureAwait(false))
            {
                while (true)
                {
                    var loaded = await _goals.LoadAsync(new GoalLoadCommand(reference, record.Goal.Id, request.Authorization), cancellationToken).ConfigureAwait(false);
                    if (loaded is GoalLoaded found)
                    {
                        record = found.Record;
                        if (ChildResultProjection.IsSettled(record.Goal.Status))
                        {
                            break;
                        }
                    }

                    var remaining = request.Deadline - _time.GetUtcNow();
                    if (remaining <= TimeSpan.Zero)
                    {
                        break;
                    }

                    await Task.Delay(remaining < _options.JoinPollInterval ? remaining : _options.JoinPollInterval, _time, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (canonical.CancellationMode == DelegationCancellationMode.CancelWithParent)
            {
                await CancelChildAsync(canonical, record, CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }

        return await SettleAsync(request.Id, record, criteria, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<DelegationResult> SettleAsync(DelegationId delegationId, GoalRecord record, AcceptanceCriteria criteria, CancellationToken cancellationToken)
    {
        var result = ChildResultProjection.Project(delegationId, record, criteria, _options.MaximumResultSummaryCharacters);
        GoalCoordinationObservation.Safe(() => GoalLog.DelegationSettled(_logger, delegationId, result.Status));
        if (result.Status != DelegationStatus.Dispatched && record.Delegation is { } stored)
        {
            _ = await _budgets.SettleAsync(new GoalBudgetSettleRequest(stored.Budget, result.Usage), cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Records cancellation of a child that has not settled, settling its running attempt in the same atomic change.</summary>
    private async ValueTask CancelChildAsync(DelegationRequest canonical, GoalRecord child, CancellationToken cancellationToken)
    {
        try
        {
            var reference = new GoalProfileReference(canonical.ProfileKey, canonical.ProfileVersion);
            var loaded = await _goals.LoadAsync(new GoalLoadCommand(reference, child.Goal.Id, canonical.Authorization), cancellationToken).ConfigureAwait(false);
            if (loaded is not GoalLoaded { Record: var current } || ChildResultProjection.IsSettled(current.Goal.Status) || current.Goal.Status == GoalStatus.Cancelled)
            {
                return;
            }

            var now = _time.GetUtcNow();
            GoalAttemptChange? change = current.Goal.ActiveAttemptId is { } running
                ? new GoalAttemptSettlement(running, GoalAttemptStatus.Cancelled, null, now)
                : null;
            var transition = new GoalTransition(
                current.Goal.Id, current.Goal.OwnerAgentId, current.Goal.SessionId, canonical.ParentRunId, canonical.OperationId, current.Goal.Status,
                GoalStatus.Cancelled, TransitionActor.Coordinator, GoalTransitionReason.Cancelled, current.Goal.Version,
                new IdempotencyKey($"agentkit.goals.cancel:{current.Goal.Id}"), now);
            _ = await _goals.TransitionAsync(new GoalTransitionCommand(reference, transition, change, canonical.Authorization), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            GoalCoordinationObservation.Safe(() => GoalLog.OperationFailed(_logger, "cancel_child", exception.GetType().Name));
        }
    }
}
