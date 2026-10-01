// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The canonical, immutable request to create one delegated child goal.</summary>
/// <remarks>
/// <para>
/// The request captures everything delayed dispatch needs: the parent goal, attempt, agent, session, and run, the goal
/// profile and definition revision, and the <see cref="SecurityAuthorizationContext"/> the delegation runs under, so a
/// worker selects the same authority and security profile rather than the agent's latest definition. It is never
/// extended with live hook state; the live caller passes that separately to the coordinator.
/// </para>
/// <para>
/// <see cref="Scope"/> and <see cref="Budget"/> can only narrow what the parent holds. <see cref="IdempotencyKey"/> makes
/// a retried request resolve to the one existing child instead of creating a duplicate.
/// </para>
/// </remarks>
public sealed record DelegationRequest
{
    /// <summary>Initializes a validated delegation request.</summary>
    /// <param name="id">The delegation identity.</param>
    /// <param name="parentGoalId">The parent goal.</param>
    /// <param name="parentAttemptId">The parent's active attempt.</param>
    /// <param name="parentAgentId">The delegating agent.</param>
    /// <param name="parentSessionId">The delegating session.</param>
    /// <param name="parentRunId">The delegating run.</param>
    /// <param name="profileKey">The captured goal profile.</param>
    /// <param name="profileVersion">The captured positive profile revision.</param>
    /// <param name="agentDefinitionRevision">The captured agent-definition revision.</param>
    /// <param name="authorization">The captured authorization the delegation runs under.</param>
    /// <param name="operationId">The operation requesting the delegation.</param>
    /// <param name="targetAgentId">The agent asked to do the work.</param>
    /// <param name="childGoal">The child's bounded objective.</param>
    /// <param name="acceptanceCriteria">What the result must satisfy.</param>
    /// <param name="scope">The narrowed tool and data scope.</param>
    /// <param name="budget">The requested child budget.</param>
    /// <param name="deadline">The instant after which the child must not still be running.</param>
    /// <param name="cancellationMode">How parent cancellation propagates.</param>
    /// <param name="joinStrategyKey">The join strategy the parent declared.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default or the profile version is not positive.</exception>
    /// <exception cref="ArgumentException">A key is blank, or the authorization names a different agent, session, or (when in-run) run than the parent.</exception>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    public DelegationRequest(
        DelegationId id,
        GoalId parentGoalId,
        GoalAttemptId parentAttemptId,
        AgentId parentAgentId,
        SessionId parentSessionId,
        RunId parentRunId,
        GoalProfileKey profileKey,
        GoalProfileVersion profileVersion,
        AgentDefinitionRevision agentDefinitionRevision,
        SecurityAuthorizationContext authorization,
        OperationId operationId,
        AgentId targetAgentId,
        GoalDefinition childGoal,
        AcceptanceCriteria acceptanceCriteria,
        DelegationScope scope,
        GoalBudgetReservation budget,
        DateTimeOffset deadline,
        DelegationCancellationMode cancellationMode,
        GoalJoinStrategyKey joinStrategyKey,
        IdempotencyKey idempotencyKey)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentOutOfRangeException.ThrowIfEqual(parentGoalId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(parentAttemptId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(parentAgentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(parentSessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(parentRunId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profileVersion.Value, nameof(profileVersion));
        ArgumentOutOfRangeException.ThrowIfNegative(agentDefinitionRevision.Value, nameof(agentDefinitionRevision));
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfNotEqual(authorization.Scope.AgentId, parentAgentId, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Scope.SessionId, parentSessionId, nameof(authorization));
        if (authorization.Scope.Correlation is InRunOperationCorrelation inRun)
        {
            ArgumentException.ThrowIfNotEqual(inRun.RunId, parentRunId, nameof(authorization));
        }

        ArgumentOutOfRangeException.ThrowIfEqual(operationId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(targetAgentId, default);
        ArgumentNullException.ThrowIfNull(childGoal);
        ArgumentNullException.ThrowIfNull(acceptanceCriteria);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentOutOfRangeException.ThrowIfUndefined(cancellationMode);
        ArgumentException.ThrowIfNullOrWhiteSpace(joinStrategyKey.Value, nameof(joinStrategyKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        Id = id;
        ParentGoalId = parentGoalId;
        ParentAttemptId = parentAttemptId;
        ParentAgentId = parentAgentId;
        ParentSessionId = parentSessionId;
        ParentRunId = parentRunId;
        ProfileKey = profileKey;
        ProfileVersion = profileVersion;
        AgentDefinitionRevision = agentDefinitionRevision;
        Authorization = authorization;
        OperationId = operationId;
        TargetAgentId = targetAgentId;
        ChildGoal = childGoal;
        AcceptanceCriteria = acceptanceCriteria;
        Scope = scope;
        Budget = budget;
        Deadline = deadline;
        CancellationMode = cancellationMode;
        JoinStrategyKey = joinStrategyKey;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Creates the request with its identity, narrowed scope, and reserved budget replaced.</summary>
    /// <param name="id">The delegation identity.</param>
    /// <param name="scope">The scope, which a policy may only have narrowed.</param>
    /// <param name="budget">The budget, carrying the scope that holds its reservation.</param>
    /// <returns>A request identical to this one except for the three replaced values.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> or <paramref name="budget"/> is null.</exception>
    public DelegationRequest With(DelegationId id, DelegationScope scope, GoalBudgetReservation budget) =>
        new(id, ParentGoalId, ParentAttemptId, ParentAgentId, ParentSessionId, ParentRunId, ProfileKey, ProfileVersion,
            AgentDefinitionRevision, Authorization, OperationId, TargetAgentId, ChildGoal, AcceptanceCriteria, scope, budget,
            Deadline, CancellationMode, JoinStrategyKey, IdempotencyKey);

    /// <summary>Gets the delegation identity.</summary>
    public DelegationId Id { get; }

    /// <summary>Gets the parent goal.</summary>
    public GoalId ParentGoalId { get; }

    /// <summary>Gets the parent's active attempt.</summary>
    public GoalAttemptId ParentAttemptId { get; }

    /// <summary>Gets the delegating agent.</summary>
    public AgentId ParentAgentId { get; }

    /// <summary>Gets the delegating session.</summary>
    public SessionId ParentSessionId { get; }

    /// <summary>Gets the delegating run.</summary>
    public RunId ParentRunId { get; }

    /// <summary>Gets the captured goal profile key.</summary>
    public GoalProfileKey ProfileKey { get; }

    /// <summary>Gets the captured goal profile revision.</summary>
    public GoalProfileVersion ProfileVersion { get; }

    /// <summary>Gets the captured agent-definition revision.</summary>
    public AgentDefinitionRevision AgentDefinitionRevision { get; }

    /// <summary>Gets the captured authorization the delegation runs under.</summary>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets the operation requesting the delegation.</summary>
    public OperationId OperationId { get; }

    /// <summary>Gets the agent asked to do the work.</summary>
    public AgentId TargetAgentId { get; }

    /// <summary>Gets the child's bounded objective.</summary>
    public GoalDefinition ChildGoal { get; }

    /// <summary>Gets what the result must satisfy.</summary>
    public AcceptanceCriteria AcceptanceCriteria { get; }

    /// <summary>Gets the narrowed tool and data scope.</summary>
    public DelegationScope Scope { get; }

    /// <summary>Gets the requested child budget.</summary>
    public GoalBudgetReservation Budget { get; }

    /// <summary>Gets the instant after which the child must not still be running.</summary>
    public DateTimeOffset Deadline { get; }

    /// <summary>Gets how parent cancellation propagates.</summary>
    public DelegationCancellationMode CancellationMode { get; }

    /// <summary>Gets the join strategy the parent declared.</summary>
    public GoalJoinStrategyKey JoinStrategyKey { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }
}
