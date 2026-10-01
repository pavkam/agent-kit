// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Is the persisted form of a <see cref="GoalTransition"/>.</summary>
/// <param name="GoalId">The goal changed.</param>
/// <param name="OwnerAgentId">The goal's owning agent.</param>
/// <param name="SessionId">The goal's owning session.</param>
/// <param name="RunId">The requesting run.</param>
/// <param name="OperationId">The requesting operation.</param>
/// <param name="From">The prior status.</param>
/// <param name="To">The new status.</param>
/// <param name="Actor">The actor kind.</param>
/// <param name="Reason">The reason.</param>
/// <param name="ExpectedVersion">The version the requester observed.</param>
/// <param name="IdempotencyKey">The replay key.</param>
/// <param name="OccurredAt">The instant of the change.</param>
internal sealed record GoalTransitionDocument(
    Guid GoalId,
    Guid OwnerAgentId,
    Guid SessionId,
    Guid RunId,
    Guid OperationId,
    GoalStatus From,
    GoalStatus To,
    TransitionActor Actor,
    GoalTransitionReason Reason,
    string ExpectedVersion,
    string IdempotencyKey,
    DateTimeOffset OccurredAt)
{
    /// <summary>Converts a transition to its persisted form.</summary>
    /// <param name="value">The non-null transition.</param>
    /// <returns>The document.</returns>
    internal static GoalTransitionDocument FromDomain(GoalTransition value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.GoalId.Value,
            value.OwnerAgentId.Value,
            value.SessionId.Value,
            value.RunId.Value,
            value.OperationId.Value,
            value.From,
            value.To,
            value.Actor,
            value.Reason,
            value.ExpectedVersion.Value,
            value.IdempotencyKey.Value,
            value.OccurredAt);
    }

    /// <summary>Restores the transition, re-running its validation including the validity table.</summary>
    /// <returns>The transition.</returns>
    internal GoalTransition ToDomain() => new(
        new GoalId(GoalId),
        new AgentId(OwnerAgentId),
        new SessionId(SessionId),
        new RunId(RunId),
        new OperationId(OperationId),
        From,
        To,
        Actor,
        Reason,
        new VersionToken(ExpectedVersion),
        new IdempotencyKey(IdempotencyKey),
        OccurredAt);
}
