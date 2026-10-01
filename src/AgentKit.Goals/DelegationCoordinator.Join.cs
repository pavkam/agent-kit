// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

internal sealed partial class DelegationCoordinator
{
    private const int _joinPageSize = 100;

    /// <inheritdoc/>
    public async ValueTask<GoalJoinDecision> JoinAsync(GoalJoinRequest request, HookDispatchContext? hooks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DelegationJoin,
            ActivityKind.Internal,
            [
                new(AgentKitTagNames.GoalId, request.ParentGoalId.ToString()),
                new(AgentKitTagNames.AgentId, request.ParentAgentId.ToString()),
                new(AgentKitTagNames.RunId, request.ParentRunId.ToString()),
                new(AgentKitTagNames.GoalJoinStrategy, request.StrategyKey.Value),
                new(AgentKitTagNames.TenantId, request.Authorization.Identity.TenantId.Value),
            ]);
        try
        {
            var decision = await JoinCoreAsync(request, cancellationToken).ConfigureAwait(false);
            var kind = decision switch
            {
                GoalJoinPending => "pending",
                GoalJoinSatisfied => "satisfied",
                _ => "unsatisfiable",
            };
            GoalCoordinationObservation.Safe(() => scope.Activity.SetSuccessful(kind));
            GoalCoordinationObservation.Safe(() => GoalCoordinationMetrics.RecordJoin(request.StrategyKey.Value, kind));
            GoalCoordinationObservation.Safe(() => GoalLog.JoinDecided(_logger, request.ParentGoalId, request.StrategyKey.Value, kind));
            if (decision is not GoalJoinPending)
            {
                await _events.PublishAsync(
                    new GoalEvent(
                        GoalEventKind.JoinDecided, request.ParentGoalId, parentGoalId: null, request.Authorization.Identity.TenantId, request.ParentAgentId,
                        request.ParentSessionId, request.Profile.Key, from: null, to: null, delegationId: null, _time.GetUtcNow()),
                    cancellationToken).ConfigureAwait(false);
            }

            return decision;
        }
        catch (OperationCanceledException)
        {
            GoalCoordinationObservation.Safe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            GoalCoordinationObservation.Safe(() => scope.Activity.SetFailed("faulted", errorType));
            GoalCoordinationObservation.Safe(() => GoalLog.OperationFailed(_logger, "join", errorType));
            throw;
        }
    }

    private async ValueTask<GoalJoinDecision> JoinCoreAsync(GoalJoinRequest request, CancellationToken cancellationToken)
    {
        var selection = await _joinStrategySelector.SelectAsync(
            new GoalJoinStrategySelectionRequest(request.Profile, request.StrategyKey), cancellationToken).ConfigureAwait(false);
        if (selection is not GoalJoinStrategySelected selected)
        {
            return new GoalJoinUnsatisfiable(((GoalJoinStrategySelectionRejected) selection).SafeMessage);
        }

        IAsyncDisposable? lease = null;
        try
        {
            while (true)
            {
                var children = await ReadJoinChildrenAsync(request, cancellationToken).ConfigureAwait(false);
                if (children is null)
                {
                    return new GoalJoinUnsatisfiable("The parent's children could not be read.");
                }

                var elapsed = request.WaitUntil is { } waitUntil && _time.GetUtcNow() >= waitUntil;
                var decision = await selected.Strategy.EvaluateAsync(
                    new GoalJoinEvaluationRequest(request, children.Value, elapsed), cancellationToken).ConfigureAwait(false);
                if (decision is not GoalJoinPending || request.WaitUntil is not { } until || elapsed)
                {
                    return decision;
                }

                lease ??= await _parking.ParkAsync(request.ParentSessionId, cancellationToken).ConfigureAwait(false);
                var remaining = until - _time.GetUtcNow();
                await Task.Delay(remaining < _options.JoinPollInterval ? remaining : _options.JoinPollInterval, _time, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<ImmutableArray<GoalJoinChild>?> ReadJoinChildrenAsync(GoalJoinRequest request, CancellationToken cancellationToken)
    {
        var children = ImmutableArray.CreateBuilder<GoalJoinChild>();
        long after = 0;
        while (true)
        {
            var page = await _goals.ReadChildrenAsync(
                new GoalChildrenCommand(request.Profile, request.ParentGoalId, after, _joinPageSize, request.Authorization), cancellationToken).ConfigureAwait(false);
            if (page is not GoalPage read)
            {
                return null;
            }

            foreach (var record in read.Items)
            {
                var (eligible, _) = GoalResultValidation.Validate(record, record.Delegation?.AcceptanceCriteria, _options.MaximumResultSummaryCharacters);
                children.Add(new GoalJoinChild(
                    record.ChildOrdinal ?? (children.Count + 1),
                    record.Goal.Id,
                    record.Goal.Status,
                    eligible && record.SettledSequence is not null,
                    record.Attempts.LastOrDefault()?.Outcome,
                    record.SettledSequence));
            }

            if (read.Next is not { } next)
            {
                return children.ToImmutable();
            }

            after = next;
        }
    }
}
