// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is the immutable, content-free observation of one committed goal or delegation change.</summary>
/// <remarks>Events observe transitions that already committed and are delivered after the fact; a sink can neither veto nor mutate them. They carry identities and statuses only, never objectives, results, or prompts.</remarks>
public sealed record GoalEvent
{
    /// <summary>Initializes a validated event.</summary>
    /// <param name="kind">The defined event kind.</param>
    /// <param name="goalId">The goal concerned.</param>
    /// <param name="parentGoalId">The goal's parent, or <see langword="null"/>.</param>
    /// <param name="tenantId">The tenant the goal belongs to.</param>
    /// <param name="ownerAgentId">The goal's owning agent.</param>
    /// <param name="sessionId">The goal's owning session.</param>
    /// <param name="profileKey">The captured goal profile.</param>
    /// <param name="from">The prior status for a transition, or <see langword="null"/>.</param>
    /// <param name="to">The resulting status, or <see langword="null"/> when the event is not about a status.</param>
    /// <param name="delegationId">The delegation concerned, or <see langword="null"/>.</param>
    /// <param name="occurredAt">The instant of the change.</param>
    /// <exception cref="ArgumentOutOfRangeException">The kind, a status, or an identity is undefined or default.</exception>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> or <paramref name="profileKey"/> is blank.</exception>
    public GoalEvent(
        GoalEventKind kind,
        GoalId goalId,
        GoalId? parentGoalId,
        TenantId tenantId,
        AgentId ownerAgentId,
        SessionId sessionId,
        GoalProfileKey profileKey,
        GoalStatus? from,
        GoalStatus? to,
        DelegationId? delegationId,
        DateTimeOffset occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfEqual(goalId, default);
        if (parentGoalId is { } parent)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(parent, default, nameof(parentGoalId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentOutOfRangeException.ThrowIfEqual(ownerAgentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        if (from is { } previous)
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(previous, nameof(from));
        }

        if (to is { } next)
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(next, nameof(to));
        }

        if (delegationId is { } delegation)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(delegation, default, nameof(delegationId));
        }

        Kind = kind;
        GoalId = goalId;
        ParentGoalId = parentGoalId;
        TenantId = tenantId;
        OwnerAgentId = ownerAgentId;
        SessionId = sessionId;
        ProfileKey = profileKey;
        From = from;
        To = to;
        DelegationId = delegationId;
        OccurredAt = occurredAt;
    }

    /// <summary>Gets the event kind.</summary>
    public GoalEventKind Kind { get; }

    /// <summary>Gets the goal concerned.</summary>
    public GoalId GoalId { get; }

    /// <summary>Gets the goal's parent, or <see langword="null"/>.</summary>
    public GoalId? ParentGoalId { get; }

    /// <summary>Gets the tenant the goal belongs to.</summary>
    public TenantId TenantId { get; }

    /// <summary>Gets the goal's owning agent.</summary>
    public AgentId OwnerAgentId { get; }

    /// <summary>Gets the goal's owning session.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the captured goal profile.</summary>
    public GoalProfileKey ProfileKey { get; }

    /// <summary>Gets the prior status, or <see langword="null"/>.</summary>
    public GoalStatus? From { get; }

    /// <summary>Gets the resulting status, or <see langword="null"/>.</summary>
    public GoalStatus? To { get; }

    /// <summary>Gets the delegation concerned, or <see langword="null"/>.</summary>
    public DelegationId? DelegationId { get; }

    /// <summary>Gets the instant of the change.</summary>
    public DateTimeOffset OccurredAt { get; }
}
