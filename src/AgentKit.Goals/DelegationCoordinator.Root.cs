// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

internal sealed partial class DelegationCoordinator
{
    private const int _rootMaterializationAttempts = 4;

    /// <summary>Loads the delegating parent, materializing a run's implicit root goal on first use, and validates that it may delegate.</summary>
    private async ValueTask<(GoalRecord? Parent, (DelegationRejectionKind Kind, string Message)? Refusal)> EnsureParentAsync(
        DelegationRequest request,
        GoalProfileSnapshot profile,
        CancellationToken cancellationToken)
    {
        var reference = profile.Reference;
        GoalRecord? record = null;
        for (var attempt = 0; attempt < _rootMaterializationAttempts; attempt++)
        {
            var loaded = await _goals.LoadAsync(new GoalLoadCommand(reference, request.ParentGoalId, request.Authorization), cancellationToken).ConfigureAwait(false);
            if (loaded is GoalLoaded found)
            {
                record = found.Record;
                if (record.Goal.Status == GoalStatus.Active || !RunRootGoal.IsRootOf(request.ParentGoalId, request.ParentRunId))
                {
                    break;
                }
            }
            else if (loaded is GoalLoadRejected { Failure.Kind: not GoalStoreFailureKind.NotFound } rejected)
            {
                return (null, (MapFailure(rejected.Failure.Kind), rejected.Failure.SafeMessage));
            }
            else if (!RunRootGoal.IsRootOf(request.ParentGoalId, request.ParentRunId))
            {
                return (null, (DelegationRejectionKind.InvalidRequest, "The parent goal does not exist."));
            }

            if (!await AdvanceRootAsync(request, profile, record, cancellationToken).ConfigureAwait(false))
            {
                break;
            }
        }

        return record is null
            ? (null, (DelegationRejectionKind.InvalidRequest, "The parent goal could not be established."))
            : record.Goal.Status is not (GoalStatus.Active or GoalStatus.Waiting)
                ? (null, (DelegationRejectionKind.InvalidRequest, "The parent goal is not active."))
                : record.Goal.OwnerAgentId != request.ParentAgentId || record.Goal.SessionId != request.ParentSessionId
                    ? (null, (DelegationRejectionKind.Unauthorized, "The parent goal belongs to another agent or session."))
                    : record.Goal.ActiveAttemptId != request.ParentAttemptId
                        ? (null, (DelegationRejectionKind.InvalidRequest, "The named parent attempt is not the goal's active attempt."))
                        : (record, null);
    }

    /// <summary>Takes one step toward an active root goal for the run; returns whether a step was made.</summary>
    private async ValueTask<bool> AdvanceRootAsync(DelegationRequest request, GoalProfileSnapshot profile, GoalRecord? record, CancellationToken cancellationToken)
    {
        var reference = profile.Reference;
        var now = _time.GetUtcNow();
        var rootKey = $"agentkit.goals.run-root:{request.ParentRunId}";
        if (record is null)
        {
            var goal = new AgentGoal(
                request.ParentGoalId, parentId: null, request.ParentAgentId, request.ParentSessionId, request.ParentRunId, request.ProfileKey,
                request.ProfileVersion, request.AgentDefinitionRevision, GoalStatus.Proposed,
                new GoalDefinition("Complete the run.", [], ExtensionData.Empty), profile.RootBudget, activeAttemptId: null,
                GoalRecordReducer.InitialVersion, now, GoalDepth.Stamp(ExtensionData.Empty, 0));
            var created = await _goals.CreateAsync(new GoalCreateCommand(goal, null, new IdempotencyKey(rootKey), request.Authorization), cancellationToken).ConfigureAwait(false);
            if (created is GoalCreated)
            {
                GoalCoordinationObservation.Safe(() => GoalLog.RootGoalMaterialized(_logger, goal.Id, request.ParentRunId));
            }

            return created is GoalCreated or GoalCreateRejected { Failure.Kind: GoalStoreFailureKind.IdempotencyConflict };
        }

        if (record.Goal.Status == GoalStatus.Proposed)
        {
            var transition = new GoalTransition(
                record.Goal.Id, record.Goal.OwnerAgentId, record.Goal.SessionId, request.ParentRunId, request.OperationId, GoalStatus.Proposed,
                GoalStatus.Ready, TransitionActor.Coordinator, GoalTransitionReason.Admitted, record.Goal.Version, new IdempotencyKey(rootKey + ":ready"), now);
            return await _goals.TransitionAsync(new GoalTransitionCommand(reference, transition, null, request.Authorization), cancellationToken).ConfigureAwait(false) is GoalTransitioned
                or GoalTransitionRejected { Failure.Kind: GoalStoreFailureKind.VersionConflict or GoalStoreFailureKind.IdempotencyConflict };
        }

        if (record.Goal.Status == GoalStatus.Ready)
        {
            var started = await _goals.StartAttemptAsync(
                new GoalAttemptRequest(
                    reference, record.Goal.Id, record.Goal.Version, record.Attempts.Length + 1, RunRootGoal.AttemptIdFor(request.ParentRunId),
                    request.ParentAgentId, request.ParentSessionId, request.ParentRunId, request.ParentRunId, request.OperationId,
                    TransitionActor.Coordinator, new GoalBudgetReservation(profile.RootBudget), new IdempotencyKey(rootKey + ":attempt"),
                    request.Authorization), cancellationToken).ConfigureAwait(false);
            return started is GoalAttemptStarted or GoalAttemptRejected { Failure.Kind: GoalStoreFailureKind.VersionConflict or GoalStoreFailureKind.IdempotencyConflict };
        }

        return false;
    }
}
