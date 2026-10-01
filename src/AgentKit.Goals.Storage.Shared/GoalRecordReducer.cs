// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

using System.Globalization;

/// <summary>Holds the single implementation of goal creation, transition, attempt, and replay rules shared by every store adapter.</summary>
/// <remarks>
/// <para>
/// Every function is pure: adapters supply stored state and persist the result, so in-memory, session-backed, JSON, and
/// SQLite stores cannot disagree about which transitions are valid, how attempts are numbered, how a replay is detected,
/// or when a goal is considered settled.
/// </para>
/// <para>Versions are a decimal counter starting at one. Each applied transition increments it, which makes the optimistic-concurrency token deterministic and replayable from the transition log alone.</para>
/// </remarks>
internal static class GoalRecordReducer
{
    /// <summary>Gets the version every newly created goal starts at.</summary>
    internal static VersionToken InitialVersion { get; } = new("1");

    /// <summary>Builds the initial aggregate for a validated create request.</summary>
    /// <param name="request">The create request.</param>
    /// <param name="sequence">The positive store-wide creation sequence the adapter allocated.</param>
    /// <param name="childOrdinal">The one-based child ordinal, or <see langword="null"/> for a root goal.</param>
    /// <returns>The aggregate to persist, with its version normalized to <see cref="InitialVersion"/>.</returns>
    internal static GoalRecord Create(GoalCreateRequest request, long sequence, int? childOrdinal)
    {
        Debug.Assert(request is not null, "Adapters validate the request before reducing it.");
        Debug.Assert(sequence > 0, "Adapters allocate a positive creation sequence.");
        var goal = request.Goal.WithState(request.Goal.Status, null, InitialVersion);
        return new GoalRecord(goal, [], [], request.Delegation, sequence, childOrdinal, null);
    }

    /// <summary>Determines whether a stored goal was created by an equivalent request.</summary>
    /// <param name="stored">The stored aggregate.</param>
    /// <param name="request">The replayed create request.</param>
    /// <returns><see langword="true"/> when every identity-defining creation fact matches; later status changes, the creation instant, and the originating run do not matter, because a retried request is issued later and possibly from a recovered run.</returns>
    internal static bool IsCreationEquivalent(GoalRecord stored, GoalCreateRequest request)
    {
        Debug.Assert(stored is not null, "A replay check compares stored state.");
        Debug.Assert(request is not null, "A replay check compares a request.");
        var left = stored.Goal;
        var right = request.Goal;
        return left.Id == right.Id
            && left.ParentId == right.ParentId
            && left.OwnerAgentId == right.OwnerAgentId
            && left.SessionId == right.SessionId
            && left.ProfileKey == right.ProfileKey
            && left.ProfileVersion == right.ProfileVersion
            && left.AgentDefinitionRevision == right.AgentDefinitionRevision
            && left.Definition == right.Definition
            && left.Budget == right.Budget
            && left.Extensions == right.Extensions
            && IsDelegationEquivalent(stored.Delegation, request.Delegation);
    }

    private static bool IsDelegationEquivalent(DelegationRequest? stored, DelegationRequest? requested) =>
        (stored, requested) switch
        {
            (null, null) => true,
            (not null, not null) => stored.ParentGoalId == requested.ParentGoalId
                && stored.TargetAgentId == requested.TargetAgentId
                && stored.ChildGoal == requested.ChildGoal
                && stored.AcceptanceCriteria == requested.AcceptanceCriteria
                && stored.Scope == requested.Scope
                && stored.IdempotencyKey == requested.IdempotencyKey,
            _ => false,
        };

    /// <summary>Validates that a child may be created under a parent.</summary>
    /// <param name="parent">The parent aggregate, or <see langword="null"/> when it does not exist for the tenant.</param>
    /// <param name="child">The child goal being created.</param>
    /// <param name="existingChildren">The number of children the parent already has.</param>
    /// <returns>A typed failure, or <see langword="null"/> when the child may be created.</returns>
    internal static GoalStoreFailure? ValidateParent(GoalRecord? parent, AgentGoal child, int existingChildren)
    {
        Debug.Assert(child is not null, "A child check names the child goal.");
        Debug.Assert(existingChildren >= 0, "A child count is never negative.");
        return parent is null
            ? new GoalStoreFailure(GoalStoreFailureKind.NotFound, "The parent goal does not exist.")
            : parent.Goal.OwnerAgentId != child.OwnerAgentId || parent.Goal.SessionId != child.SessionId
                ? new GoalStoreFailure(GoalStoreFailureKind.ScopeMismatch, "A child goal must be owned by its parent's agent and session.")
                : parent.Goal.Status is not (GoalStatus.Active or GoalStatus.Waiting)
                    ? new GoalStoreFailure(GoalStoreFailureKind.LimitExceeded, "The parent goal is not open to children.")
                    : existingChildren >= parent.Goal.Budget.MaximumChildren
                        ? new GoalStoreFailure(GoalStoreFailureKind.LimitExceeded, "The parent goal has reached its child ceiling.")
                        : null;
    }

    /// <summary>Determines whether reaching a status assigns the goal a store-wide settlement sequence.</summary>
    /// <param name="to">The target status.</param>
    /// <returns><see langword="true"/> for completed, failed, and cancelled.</returns>
    internal static bool AssignsSettledSequence(GoalStatus to) =>
        to is GoalStatus.Completed or GoalStatus.Failed or GoalStatus.Cancelled;

    /// <summary>Applies one transition request to one stored aggregate.</summary>
    /// <param name="current">The stored aggregate.</param>
    /// <param name="request">The transition request.</param>
    /// <param name="nextSettledSequence">The sequence the adapter will assign if this transition settles the goal.</param>
    /// <returns>The new aggregate, a replay of the stored state, or a typed rejection.</returns>
    internal static GoalReduction ApplyTransition(GoalRecord current, GoalTransitionRequest request, long nextSettledSequence)
    {
        Debug.Assert(request is not null, "A reduction runs for a request.");
        return ApplyTransition(current, request.Transition, request.Attempt, nextSettledSequence);
    }

    /// <summary>Applies one transition and its attempt change to one stored aggregate.</summary>
    /// <param name="current">The stored aggregate.</param>
    /// <param name="transition">The transition to apply.</param>
    /// <param name="change">The atomic attempt mutation, or <see langword="null"/>.</param>
    /// <param name="nextSettledSequence">The sequence assigned if this transition settles the goal; replay passes the recorded value.</param>
    /// <returns>The new aggregate, a replay of the stored state, or a typed rejection.</returns>
    internal static GoalReduction ApplyTransition(GoalRecord current, GoalTransition transition, GoalAttemptChange? change, long nextSettledSequence)
    {
        Debug.Assert(current is not null, "A reduction runs over stored state.");
        Debug.Assert(transition is not null, "A reduction runs for a transition.");
        Debug.Assert(nextSettledSequence > 0, "Adapters supply a positive settlement sequence.");
        if (current.Transitions.FirstOrDefault(prior => prior.IdempotencyKey == transition.IdempotencyKey) is { } earlier)
        {
            return IsSameTransition(earlier, transition) && IsAttemptChangeApplied(current, change)
                ? GoalReduction.Replayed(current)
                : GoalReduction.Rejected(GoalStoreFailureKind.IdempotencyConflict, "The transition idempotency key was reused with a different request.");
        }

        var goal = current.Goal;
        if (transition.ExpectedVersion != goal.Version || transition.From != goal.Status)
        {
            return GoalReduction.Rejected(GoalStoreFailureKind.VersionConflict, "The goal changed since the requester observed it.");
        }

        GoalAttemptId? active;
        var attempts = current.Attempts;
        if (transition.To == GoalStatus.Active && transition.From == GoalStatus.Ready)
        {
            if (change is not GoalAttemptStart start || !IsNextAttempt(current, start.Attempt))
            {
                return GoalReduction.Rejected(GoalStoreFailureKind.InvalidTransition, "Activating a ready goal requires starting the next attempt.");
            }

            attempts = attempts.Add(start.Attempt);
            active = start.Attempt.Id;
        }
        else if (transition.To is GoalStatus.Active or GoalStatus.Waiting)
        {
            if (change is not null || (transition.To == GoalStatus.Active && goal.ActiveAttemptId is null))
            {
                return GoalReduction.Rejected(
                    GoalStoreFailureKind.InvalidTransition,
                    "Parking or resuming a goal keeps its running attempt and takes no attempt change.");
            }

            active = goal.ActiveAttemptId;
        }
        else if (transition.To is GoalStatus.Completed or GoalStatus.Failed or GoalStatus.Cancelled or GoalStatus.Blocked
            && goal.ActiveAttemptId is { } running)
        {
            if (change is not GoalAttemptSettlement settlement
                || settlement.AttemptId != running
                || !SettlesConsistently(transition.To, settlement))
            {
                return GoalReduction.Rejected(GoalStoreFailureKind.InvalidTransition, "Leaving an active goal requires settling its running attempt consistently.");
            }

            var index = attempts.IndexOf(attempts.First(attempt => attempt.Id == running));
            var open = attempts[index];
            if (settlement.EndedAt < open.StartedAt)
            {
                return GoalReduction.Rejected(GoalStoreFailureKind.InvalidTransition, "An attempt cannot end before it started.");
            }

            attempts = attempts.SetItem(index, new GoalAttempt(
                open.Id, open.GoalId, open.AgentId, open.SessionId, open.RunId, open.Number, settlement.Status,
                open.Reservation, settlement.Outcome, open.StartedAt, settlement.EndedAt));
            active = null;
        }
        else
        {
            if (change is not null)
            {
                return GoalReduction.Rejected(GoalStoreFailureKind.InvalidTransition, "This transition takes no attempt change.");
            }

            active = null;
        }

        var settled = current.SettledSequence;
        if (AssignsSettledSequence(transition.To))
        {
            settled = nextSettledSequence;
        }
        else if (transition.To == GoalStatus.Ready)
        {
            settled = null;
        }

        var next = goal.WithState(transition.To, active, NextVersion(goal.Version));
        return GoalReduction.Applied(new GoalRecord(
            next, attempts, current.Transitions.Add(transition), current.Delegation, current.Sequence, current.ChildOrdinal, settled));
    }

    /// <summary>Returns the version that follows an existing decimal version token.</summary>
    /// <param name="version">The current token, which every store produces from <see cref="InitialVersion"/>.</param>
    /// <returns>The incremented token.</returns>
    internal static VersionToken NextVersion(VersionToken version)
    {
        Debug.Assert(long.TryParse(version.Value, NumberStyles.None, CultureInfo.InvariantCulture, out _), "Stores only ever hold decimal version tokens.");
        return new VersionToken((long.Parse(version.Value, NumberStyles.None, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Determines whether a replayed transition is the one an earlier request applied.</summary>
    /// <param name="earlier">The recorded transition.</param>
    /// <param name="replayed">The replayed transition.</param>
    /// <returns><see langword="true"/> when the status change, actor, reason, observed version, and owner match; the instant, requesting run, and operation of a retry legitimately differ.</returns>
    private static bool IsSameTransition(GoalTransition earlier, GoalTransition replayed) =>
        earlier.GoalId == replayed.GoalId
        && earlier.OwnerAgentId == replayed.OwnerAgentId
        && earlier.SessionId == replayed.SessionId
        && earlier.From == replayed.From
        && earlier.To == replayed.To
        && earlier.Actor == replayed.Actor
        && earlier.Reason == replayed.Reason
        && earlier.ExpectedVersion == replayed.ExpectedVersion
        && earlier.IdempotencyKey == replayed.IdempotencyKey;

    private static bool IsNextAttempt(GoalRecord current, GoalAttempt attempt)
    {
        Debug.Assert(attempt is not null, "The caller supplies a start attempt.");
        return attempt.GoalId == current.Goal.Id
            && attempt.Status == GoalAttemptStatus.Running
            && attempt.Number == current.Attempts.Length + 1
            && current.Attempts.All(existing => existing.Id != attempt.Id);
    }

    private static bool SettlesConsistently(GoalStatus to, GoalAttemptSettlement settlement)
    {
        Debug.Assert(settlement is not null, "The caller supplies a settlement.");
        return to switch
        {
            GoalStatus.Completed => settlement.Status == GoalAttemptStatus.Succeeded
                && settlement.Outcome is { Status: DelegationStatus.Succeeded },
            GoalStatus.Failed => settlement.Status == GoalAttemptStatus.Failed,
            GoalStatus.Cancelled => settlement.Status == GoalAttemptStatus.Cancelled,
            GoalStatus.Blocked => settlement.Status == GoalAttemptStatus.Blocked,
            GoalStatus.Proposed or GoalStatus.Ready or GoalStatus.Active or GoalStatus.Waiting => false,
            _ => false,
        };
    }

    private static bool IsAttemptChangeApplied(GoalRecord current, GoalAttemptChange? change) => change switch
    {
        null => true,
        GoalAttemptStart start => current.Attempts.Any(attempt => attempt.Id == start.Attempt.Id),
        GoalAttemptSettlement settlement => current.Attempts.Any(attempt => attempt.Id == settlement.AttemptId && attempt.Status == settlement.Status),
        _ => false,
    };
}
