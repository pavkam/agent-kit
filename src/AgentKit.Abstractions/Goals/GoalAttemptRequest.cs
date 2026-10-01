// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks the coordinator to start a new attempt on a ready goal.</summary>
/// <remarks>
/// The coordinator builds the <see cref="GoalAttemptStart"/> and the ready-to-active transition, so the caller cannot skew
/// the status pair, and obtains a single-use grant bound to that exact change from the authority the captured
/// authorization names. The attempt executes in the named agent, session, and run, which for a delegated child is the target
/// agent's own. The goal itself is owned by the authorization's agent and session.
/// </remarks>
public sealed record GoalAttemptRequest
{
    /// <summary>Initializes a validated attempt request.</summary>
    /// <param name="profile">The captured profile the goal runs under.</param>
    /// <param name="goalId">The goal that receives the attempt.</param>
    /// <param name="expectedVersion">The goal version the caller observed.</param>
    /// <param name="attemptNumber">The positive attempt ordinal: one past the attempts the caller observed.</param>
    /// <param name="attemptId">The attempt identity to record, or <see langword="null"/> to have the coordinator allocate one.</param>
    /// <param name="attemptAgentId">The agent that executes the attempt.</param>
    /// <param name="attemptSessionId">The session that executes the attempt.</param>
    /// <param name="attemptRunId">The run that executes the attempt.</param>
    /// <param name="requestingRunId">The run requesting the start.</param>
    /// <param name="operationId">The operation requesting the start.</param>
    /// <param name="actor">The defined actor kind.</param>
    /// <param name="reservation">The budget reservation backing the attempt.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="authorization">The captured authorization whose scope owns the goal.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default, the attempt number is not positive, or the actor is undefined.</exception>
    /// <exception cref="ArgumentException">A token or key is blank.</exception>
    public GoalAttemptRequest(
        GoalProfileReference profile,
        GoalId goalId,
        VersionToken expectedVersion,
        int attemptNumber,
        GoalAttemptId? attemptId,
        AgentId attemptAgentId,
        SessionId attemptSessionId,
        RunId attemptRunId,
        RunId requestingRunId,
        OperationId operationId,
        TransitionActor actor,
        GoalBudgetReservation reservation,
        IdempotencyKey idempotencyKey,
        SecurityAuthorizationContext authorization)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfEqual(goalId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedVersion.Value, nameof(expectedVersion));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attemptNumber);
        if (attemptId is { } attempt)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(attempt, default, nameof(attemptId));
        }

        ArgumentOutOfRangeException.ThrowIfEqual(attemptAgentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(attemptSessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(attemptRunId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(requestingRunId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(actor);
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(authorization);
        Profile = profile;
        GoalId = goalId;
        ExpectedVersion = expectedVersion;
        AttemptNumber = attemptNumber;
        AttemptId = attemptId;
        AttemptAgentId = attemptAgentId;
        AttemptSessionId = attemptSessionId;
        AttemptRunId = attemptRunId;
        RequestingRunId = requestingRunId;
        OperationId = operationId;
        Actor = actor;
        Reservation = reservation;
        IdempotencyKey = idempotencyKey;
        Authorization = authorization;
    }

    /// <summary>Gets the captured profile reference.</summary>
    public GoalProfileReference Profile { get; }

    /// <summary>Gets the goal that receives the attempt.</summary>
    public GoalId GoalId { get; }

    /// <summary>Gets the goal version the caller observed.</summary>
    public VersionToken ExpectedVersion { get; }

    /// <summary>Gets the one-based attempt ordinal.</summary>
    public int AttemptNumber { get; }

    /// <summary>Gets the attempt identity to record, or <see langword="null"/> to have the coordinator allocate one.</summary>
    public GoalAttemptId? AttemptId { get; }

    /// <summary>Gets the agent that executes the attempt.</summary>
    public AgentId AttemptAgentId { get; }

    /// <summary>Gets the session that executes the attempt.</summary>
    public SessionId AttemptSessionId { get; }

    /// <summary>Gets the run that executes the attempt.</summary>
    public RunId AttemptRunId { get; }

    /// <summary>Gets the run requesting the start.</summary>
    public RunId RequestingRunId { get; }

    /// <summary>Gets the operation requesting the start.</summary>
    public OperationId OperationId { get; }

    /// <summary>Gets the actor kind.</summary>
    public TransitionActor Actor { get; }

    /// <summary>Gets the budget reservation backing the attempt.</summary>
    public GoalBudgetReservation Reservation { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the captured authorization whose scope owns the goal.</summary>
    public SecurityAuthorizationContext Authorization { get; }
}
