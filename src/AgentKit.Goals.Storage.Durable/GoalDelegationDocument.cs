// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

using AgentKit.Storage.Json;

/// <summary>Is the persisted form of a <see cref="DelegationRequest"/>, including the captured authorization a worker needs after process loss.</summary>
/// <remarks>The captured authorization is stored through the shared portable evidence shapes, so a restarted worker selects exactly the authority and security profile the delegation was made under rather than the agent's latest definition.</remarks>
/// <param name="Id">The delegation identity.</param>
/// <param name="ParentGoalId">The parent goal.</param>
/// <param name="ParentAttemptId">The parent's attempt.</param>
/// <param name="ParentAgentId">The delegating agent.</param>
/// <param name="ParentSessionId">The delegating session.</param>
/// <param name="ParentRunId">The delegating run.</param>
/// <param name="ProfileKey">The captured profile key.</param>
/// <param name="ProfileVersion">The captured profile revision.</param>
/// <param name="AgentDefinitionRevision">The captured definition revision.</param>
/// <param name="Authorization">The captured authorization.</param>
/// <param name="OperationId">The requesting operation.</param>
/// <param name="TargetAgentId">The agent asked to do the work.</param>
/// <param name="Objective">The child's objective.</param>
/// <param name="ContextReferences">The child's context references.</param>
/// <param name="DefinitionExtensions">The child definition's extension data.</param>
/// <param name="Criteria">The acceptance criteria.</param>
/// <param name="RequiresEvidence">Whether accepted results need evidence.</param>
/// <param name="AllowedTools">The narrowed tool scope.</param>
/// <param name="DataScopes">The narrowed data scope.</param>
/// <param name="Budget">The reserved budget.</param>
/// <param name="Deadline">The child deadline.</param>
/// <param name="CancellationMode">How parent cancellation propagates.</param>
/// <param name="JoinStrategyKey">The declared join strategy.</param>
/// <param name="IdempotencyKey">The replay key.</param>
internal sealed record GoalDelegationDocument(
    Guid Id,
    Guid ParentGoalId,
    Guid ParentAttemptId,
    Guid ParentAgentId,
    Guid ParentSessionId,
    Guid ParentRunId,
    string ProfileKey,
    long ProfileVersion,
    long AgentDefinitionRevision,
    JsonSecurityAuthorizationContext Authorization,
    Guid OperationId,
    Guid TargetAgentId,
    string Objective,
    ImmutableArray<string> ContextReferences,
    ImmutableArray<GoalExtensionDocument> DefinitionExtensions,
    ImmutableArray<string> Criteria,
    bool RequiresEvidence,
    ImmutableArray<string> AllowedTools,
    ImmutableArray<string> DataScopes,
    GoalBudgetReservationDocument Budget,
    DateTimeOffset Deadline,
    DelegationCancellationMode CancellationMode,
    string JoinStrategyKey,
    string IdempotencyKey)
{
    /// <summary>Converts a delegation to its persisted form.</summary>
    /// <param name="value">The non-null delegation.</param>
    /// <returns>The document.</returns>
    internal static GoalDelegationDocument FromDomain(DelegationRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.Id.Value,
            value.ParentGoalId.Value,
            value.ParentAttemptId.Value,
            value.ParentAgentId.Value,
            value.ParentSessionId.Value,
            value.ParentRunId.Value,
            value.ProfileKey.Value,
            value.ProfileVersion.Value,
            value.AgentDefinitionRevision.Value,
            JsonSecurityAuthorizationContext.FromDomain(value.Authorization),
            value.OperationId.Value,
            value.TargetAgentId.Value,
            value.ChildGoal.Objective,
            value.ChildGoal.ContextReferences,
            GoalExtensionDocument.FromDomain(value.ChildGoal.Extensions),
            value.AcceptanceCriteria.Criteria,
            value.AcceptanceCriteria.RequiresEvidence,
            [.. value.Scope.AllowedTools.Select(static tool => tool.Value)],
            value.Scope.DataScopes,
            GoalBudgetReservationDocument.FromDomain(value.Budget),
            value.Deadline,
            value.CancellationMode,
            value.JoinStrategyKey.Value,
            value.IdempotencyKey.Value);
    }

    /// <summary>Restores the delegation, re-running every domain validation.</summary>
    /// <returns>The delegation.</returns>
    internal DelegationRequest ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Authorization);
        ArgumentNullException.ThrowIfNull(Budget);
        return new(
            new DelegationId(Id),
            new GoalId(ParentGoalId),
            new GoalAttemptId(ParentAttemptId),
            new AgentId(ParentAgentId),
            new SessionId(ParentSessionId),
            new RunId(ParentRunId),
            new GoalProfileKey(ProfileKey),
            new GoalProfileVersion(ProfileVersion),
            new AgentDefinitionRevision(AgentDefinitionRevision),
            Authorization.ToDomain(),
            new OperationId(OperationId),
            new AgentId(TargetAgentId),
            new GoalDefinition(Objective, ContextReferences.IsDefault ? [] : ContextReferences, GoalExtensionDocument.ToDomain(DefinitionExtensions)),
            new AcceptanceCriteria(Criteria.IsDefault ? [] : Criteria, RequiresEvidence),
            new DelegationScope(
                AllowedTools.IsDefault ? [] : [.. AllowedTools.Select(static tool => new ToolId(tool))],
                DataScopes.IsDefault ? [] : DataScopes),
            Budget.ToDomain(),
            Deadline,
            CancellationMode,
            new GoalJoinStrategyKey(JoinStrategyKey),
            new IdempotencyKey(IdempotencyKey));
    }
}
