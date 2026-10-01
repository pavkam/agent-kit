// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Builds internally consistent goal, attempt, transition, and delegation values for tests.</summary>
/// <remarks>Identities are fresh per call unless supplied, so independent test cases never collide in a shared store. Every value passes the same constructors production code uses.</remarks>
public static class GoalTestData
{
    /// <summary>Gets the instant every builder uses unless a test supplies one.</summary>
    public static DateTimeOffset Now { get; } = DateTimeOffset.UnixEpoch.AddHours(1);

    /// <summary>Gets the profile reference every builder captures.</summary>
    public static GoalProfileReference Profile { get; } = new(new GoalProfileKey("goals"), new GoalProfileVersion(1));

    /// <summary>Creates a fresh non-default agent identity.</summary>
    /// <returns>A new agent identity.</returns>
    public static AgentId NewAgent() => new(Guid.NewGuid());

    /// <summary>Creates a fresh non-default session identity.</summary>
    /// <returns>A new session identity.</returns>
    public static SessionId NewSession() => new(Guid.NewGuid());

    /// <summary>Creates a fresh non-default run identity.</summary>
    /// <returns>A new run identity.</returns>
    public static RunId NewRun() => new(Guid.NewGuid());

    /// <summary>Creates a fresh non-default operation identity.</summary>
    /// <returns>A new operation identity.</returns>
    public static OperationId NewOperation() => new(Guid.NewGuid());

    /// <summary>Creates a deterministic identity in one tenant.</summary>
    /// <param name="tenant">The tenant text.</param>
    /// <param name="principal">The principal text.</param>
    /// <returns>A complete test identity.</returns>
    public static ExecutionIdentity Identity(string tenant = "tenant", string principal = "principal") =>
        TestExecutionIdentity.Create(new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human);

    /// <summary>Creates captured authorization for one goal owner.</summary>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="sessionId">The owning session.</param>
    /// <param name="runId">The run the operation belongs to.</param>
    /// <param name="identity">The identity, or the default identity when omitted.</param>
    /// <returns>Authorization whose scope is that owner.</returns>
    public static SecurityAuthorizationContext Authorization(AgentId agentId, SessionId sessionId, RunId runId, ExecutionIdentity? identity = null) =>
        TestSecurityEvidence.Authorization(agentId, sessionId, new InRunOperationCorrelation(NewOperation(), runId, null), identity ?? Identity());

    /// <summary>Creates a goal in one status with no attempt.</summary>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="sessionId">The owning session.</param>
    /// <param name="runId">The originating run.</param>
    /// <param name="parentId">The parent goal, or null for a root.</param>
    /// <param name="status">The status; proposed or ready for creation.</param>
    /// <param name="budget">The ceiling, or a generous default.</param>
    /// <param name="id">The goal identity, or a fresh one.</param>
    /// <returns>The goal.</returns>
    public static AgentGoal Goal(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        GoalId? parentId = null,
        GoalStatus status = GoalStatus.Proposed,
        GoalBudget? budget = null,
        GoalId? id = null) => new(
            id ?? new GoalId(Guid.NewGuid()),
            parentId,
            agentId,
            sessionId,
            runId,
            Profile.Key,
            Profile.Version,
            new AgentDefinitionRevision(1),
            status,
            new GoalDefinition("Do the bounded work.", [], ExtensionData.Empty),
            budget ?? new GoalBudget(10, 50, 4),
            activeAttemptId: null,
            new VersionToken("1"),
            Now,
            ExtensionData.Empty);

    /// <summary>Creates a delegation of one child from a parent goal.</summary>
    /// <param name="parent">The parent goal.</param>
    /// <param name="parentAttemptId">The parent's attempt.</param>
    /// <param name="parentRunId">The parent's run.</param>
    /// <param name="target">The target agent.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="authorization">The captured authorization, which must belong to the parent's agent and session.</param>
    /// <param name="budget">The requested child budget, or a small default.</param>
    /// <returns>The delegation request.</returns>
    public static DelegationRequest Delegation(
        AgentGoal parent,
        GoalAttemptId parentAttemptId,
        RunId parentRunId,
        AgentId target,
        string idempotencyKey,
        SecurityAuthorizationContext authorization,
        GoalBudget? budget = null) => new(
            new DelegationId(Guid.NewGuid()),
            parent.Id,
            parentAttemptId,
            parent.OwnerAgentId,
            parent.SessionId,
            parentRunId,
            Profile.Key,
            Profile.Version,
            new AgentDefinitionRevision(1),
            authorization,
            NewOperation(),
            target,
            new GoalDefinition("Research the subtopic.", [], ExtensionData.Empty),
            new AcceptanceCriteria(["Cite sources."], requiresEvidence: false),
            new DelegationScope([], []),
            new GoalBudgetReservation(budget ?? new GoalBudget(3, 10, 0)),
            Now.AddHours(1),
            DelegationCancellationMode.CancelWithParent,
            GoalJoinStrategyKeys.All,
            new IdempotencyKey(idempotencyKey));

    /// <summary>Creates a running attempt numbered one past the recorded attempts.</summary>
    /// <param name="goalId">The goal.</param>
    /// <param name="number">The one-based number.</param>
    /// <param name="agentId">The executing agent.</param>
    /// <param name="sessionId">The executing session.</param>
    /// <param name="runId">The executing run.</param>
    /// <returns>The running attempt.</returns>
    public static GoalAttempt Attempt(GoalId goalId, int number, AgentId agentId, SessionId sessionId, RunId runId) => new(
        new GoalAttemptId(Guid.NewGuid()),
        goalId,
        agentId,
        sessionId,
        runId,
        number,
        GoalAttemptStatus.Running,
        new GoalBudgetReservation(new GoalBudget(3, 10, 0)),
        outcome: null,
        Now,
        endedAt: null);

    /// <summary>Creates a successful outcome reference.</summary>
    /// <param name="runId">The producing run.</param>
    /// <param name="summary">The summary text.</param>
    /// <returns>The outcome.</returns>
    public static GoalOutcomeReference Outcome(RunId runId, string summary = "Done.") => new(
        runId,
        DelegationStatus.Succeeded,
        new StructuredGoalResult(summary, ExtensionData.Empty),
        [],
        GoalBudgetUsage.None,
        SideEffectCertainty.DefinitelyNotPerformed);

    /// <summary>Creates a transition from a stored aggregate to a new status.</summary>
    /// <param name="record">The aggregate the requester observed.</param>
    /// <param name="to">The requested status.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="reason">The recorded reason.</param>
    /// <returns>The transition addressed to the goal's owner.</returns>
    public static GoalTransition Transition(GoalRecord record, GoalStatus to, string idempotencyKey, GoalTransitionReason reason = GoalTransitionReason.Admitted) => new(
        record.Goal.Id,
        record.Goal.OwnerAgentId,
        record.Goal.SessionId,
        record.Goal.OriginatingRunId,
        NewOperation(),
        record.Goal.Status,
        to,
        TransitionActor.Coordinator,
        reason,
        record.Goal.Version,
        new IdempotencyKey(idempotencyKey),
        Now);
}
