// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records one explicit, durable goal status change with its actor, reason, prior version, and time.</summary>
/// <remarks>
/// <para>
/// <see cref="IsValid"/> is the single static validity table: the constructor rejects any pair it does not allow, and
/// stores re-check the pair against the goal's stored status before applying it. <see cref="OwnerAgentId"/> and
/// <see cref="SessionId"/> must equal the goal's owner; a transition addressed to another agent's or session's goal is
/// rejected by every store. <see cref="IdempotencyKey"/> makes a replayed transition return its original outcome.
/// </para>
/// <para>
/// <see cref="RunId"/> and <see cref="OperationId"/> are causal evidence of the run and operation requesting the change;
/// they do not select the goal.
/// </para>
/// </remarks>
public sealed record GoalTransition
{
    private static readonly GoalStatus[][] _targets =
    [
        /* Proposed  */ [GoalStatus.Ready, GoalStatus.Blocked, GoalStatus.Failed, GoalStatus.Cancelled],
        /* Ready     */ [GoalStatus.Active, GoalStatus.Blocked, GoalStatus.Failed, GoalStatus.Cancelled],
        /* Active    */ [GoalStatus.Waiting, GoalStatus.Completed, GoalStatus.Failed, GoalStatus.Blocked, GoalStatus.Cancelled],
        /* Waiting   */ [GoalStatus.Active, GoalStatus.Completed, GoalStatus.Failed, GoalStatus.Blocked, GoalStatus.Cancelled],
        /* Completed */ [],
        /* Failed    */ [GoalStatus.Ready, GoalStatus.Cancelled],
        /* Cancelled */ [],
        /* Blocked   */ [GoalStatus.Ready, GoalStatus.Failed, GoalStatus.Cancelled],
    ];

    /// <summary>Initializes a validated transition.</summary>
    /// <param name="goalId">The goal being changed.</param>
    /// <param name="ownerAgentId">The goal's owning agent.</param>
    /// <param name="sessionId">The goal's owning session.</param>
    /// <param name="runId">The run requesting the change.</param>
    /// <param name="operationId">The operation requesting the change.</param>
    /// <param name="from">The status the requester observed.</param>
    /// <param name="to">The requested status.</param>
    /// <param name="actor">The defined actor kind.</param>
    /// <param name="reason">The defined reason.</param>
    /// <param name="expectedVersion">The version the requester observed.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="occurredAt">The instant of the change.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default, an enum is undefined, or the pair is not allowed by <see cref="IsValid"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="expectedVersion"/> or <paramref name="idempotencyKey"/> is blank.</exception>
    public GoalTransition(
        GoalId goalId,
        AgentId ownerAgentId,
        SessionId sessionId,
        RunId runId,
        OperationId operationId,
        GoalStatus from,
        GoalStatus to,
        TransitionActor actor,
        GoalTransitionReason reason,
        VersionToken expectedVersion,
        IdempotencyKey idempotencyKey,
        DateTimeOffset occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(goalId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(ownerAgentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(from);
        ArgumentOutOfRangeException.ThrowIfUndefined(to);
        ArgumentOutOfRangeException.ThrowIfUndefined(actor);
        ArgumentOutOfRangeException.ThrowIfUndefined(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedVersion.Value, nameof(expectedVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        if (!IsValid(from, to))
        {
            throw new ArgumentOutOfRangeException(nameof(to), to, $"A goal cannot move from {from} to {to}.");
        }

        GoalId = goalId;
        OwnerAgentId = ownerAgentId;
        SessionId = sessionId;
        RunId = runId;
        OperationId = operationId;
        From = from;
        To = to;
        Actor = actor;
        Reason = reason;
        ExpectedVersion = expectedVersion;
        IdempotencyKey = idempotencyKey;
        OccurredAt = occurredAt;
    }

    /// <summary>Gets the goal being changed.</summary>
    public GoalId GoalId { get; }

    /// <summary>Gets the goal's owning agent.</summary>
    public AgentId OwnerAgentId { get; }

    /// <summary>Gets the goal's owning session.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the run requesting the change.</summary>
    public RunId RunId { get; }

    /// <summary>Gets the operation requesting the change.</summary>
    public OperationId OperationId { get; }

    /// <summary>Gets the status the requester observed.</summary>
    public GoalStatus From { get; }

    /// <summary>Gets the requested status.</summary>
    public GoalStatus To { get; }

    /// <summary>Gets the actor kind.</summary>
    public TransitionActor Actor { get; }

    /// <summary>Gets the recorded reason.</summary>
    public GoalTransitionReason Reason { get; }

    /// <summary>Gets the version the requester observed.</summary>
    public VersionToken ExpectedVersion { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the instant of the change.</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Determines whether a goal may move directly from one status to another.</summary>
    /// <param name="from">The current status.</param>
    /// <param name="to">The requested status.</param>
    /// <returns><see langword="true"/> when the pair is in the validity table; a status never moves to itself.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A status is undefined.</exception>
    public static bool IsValid(GoalStatus from, GoalStatus to)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(from);
        ArgumentOutOfRangeException.ThrowIfUndefined(to);
        return Array.IndexOf(_targets[(int) from], to) >= 0;
    }

    /// <summary>Lists the statuses reachable from one status.</summary>
    /// <param name="from">The current status.</param>
    /// <returns>The allowed targets in a stable order; empty for a terminal status.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="from"/> is undefined.</exception>
    public static ImmutableArray<GoalStatus> ValidTargets(GoalStatus from)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(from);
        return [.. _targets[(int) from]];
    }

    /// <summary>Determines whether a status is terminal, so that no later transition is allowed.</summary>
    /// <param name="status">The status to classify.</param>
    /// <returns><see langword="true"/> for <see cref="GoalStatus.Completed"/> and <see cref="GoalStatus.Cancelled"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined.</exception>
    public static bool IsTerminal(GoalStatus status)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        return _targets[(int) status].Length == 0;
    }
}
