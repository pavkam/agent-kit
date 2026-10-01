// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks the coordinator to decide a parent goal's join over its durable children.</summary>
/// <remarks>
/// The coordinator reads the parent's children in recorded ordinal order, validates each settled result against its
/// acceptance criteria, and asks the named strategy for a decision. <see cref="WaitUntil"/> turns a pending decision into a
/// bounded wait: the coordinator polls durable state through the injected clock and parks the waiting run's worker
/// occupancy, never holding a session lock. Without it, a pending decision is returned immediately.
/// </remarks>
public sealed record GoalJoinRequest
{
    /// <summary>Initializes a validated join request.</summary>
    /// <param name="profile">The captured profile the parent runs under.</param>
    /// <param name="parentGoalId">The parent goal.</param>
    /// <param name="parentAgentId">The parent's owning agent.</param>
    /// <param name="parentSessionId">The parent's owning session.</param>
    /// <param name="parentRunId">The run that is waiting on the join.</param>
    /// <param name="authorization">The captured authorization for reading the parent's children.</param>
    /// <param name="strategyKey">The join strategy the parent declared.</param>
    /// <param name="quorumSize">The positive quorum size for a quorum strategy, or <see langword="null"/>.</param>
    /// <param name="waitUntil">The instant a pending join stops waiting, or <see langword="null"/> to return at once.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default or <paramref name="quorumSize"/> is not positive.</exception>
    /// <exception cref="ArgumentException">The strategy key is blank or the authorization does not belong to the parent.</exception>
    public GoalJoinRequest(
        GoalProfileReference profile,
        GoalId parentGoalId,
        AgentId parentAgentId,
        SessionId parentSessionId,
        RunId parentRunId,
        SecurityAuthorizationContext authorization,
        GoalJoinStrategyKey strategyKey,
        int? quorumSize,
        DateTimeOffset? waitUntil)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfEqual(parentGoalId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(parentAgentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(parentSessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(parentRunId, default);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfNotEqual(authorization.Scope.AgentId, parentAgentId, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Scope.SessionId, parentSessionId, nameof(authorization));
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyKey.Value, nameof(strategyKey));
        if (quorumSize is { } quorum)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quorum, nameof(quorumSize));
        }

        Profile = profile;
        ParentGoalId = parentGoalId;
        ParentAgentId = parentAgentId;
        ParentSessionId = parentSessionId;
        ParentRunId = parentRunId;
        Authorization = authorization;
        StrategyKey = strategyKey;
        QuorumSize = quorumSize;
        WaitUntil = waitUntil;
    }

    /// <summary>Gets the captured profile reference.</summary>
    public GoalProfileReference Profile { get; }

    /// <summary>Gets the parent goal.</summary>
    public GoalId ParentGoalId { get; }

    /// <summary>Gets the parent's owning agent.</summary>
    public AgentId ParentAgentId { get; }

    /// <summary>Gets the parent's owning session.</summary>
    public SessionId ParentSessionId { get; }

    /// <summary>Gets the run that is waiting on the join.</summary>
    public RunId ParentRunId { get; }

    /// <summary>Gets the captured authorization for reading the parent's children.</summary>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets the join strategy the parent declared.</summary>
    public GoalJoinStrategyKey StrategyKey { get; }

    /// <summary>Gets the quorum size for a quorum strategy, or <see langword="null"/>.</summary>
    public int? QuorumSize { get; }

    /// <summary>Gets the instant a pending join stops waiting, or <see langword="null"/>.</summary>
    public DateTimeOffset? WaitUntil { get; }
}
