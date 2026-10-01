// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Builds the immutable, content-free events the coordinators publish after a change commits.</summary>
internal static class GoalEventFactory
{
    /// <summary>Builds an event describing one goal aggregate.</summary>
    /// <param name="kind">The event kind.</param>
    /// <param name="record">The aggregate after the change.</param>
    /// <param name="tenant">The tenant the goal belongs to.</param>
    /// <param name="from">The prior status, or <see langword="null"/>.</param>
    /// <param name="occurredAt">The commit instant.</param>
    /// <returns>The event.</returns>
    internal static GoalEvent For(GoalEventKind kind, GoalRecord record, TenantId tenant, GoalStatus? from, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(record);
        var goal = record.Goal;
        return new GoalEvent(
            kind, goal.Id, goal.ParentId, tenant, goal.OwnerAgentId, goal.SessionId, goal.ProfileKey, from, goal.Status,
            record.Delegation?.Id, occurredAt);
    }
}
