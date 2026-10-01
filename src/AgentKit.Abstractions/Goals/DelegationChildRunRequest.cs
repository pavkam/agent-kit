// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one claimed child attempt a runner must execute to a terminal result.</summary>
/// <remarks>The attempt is already recorded as running in durable state before the runner is called, so a runner never decides whether to run; it only executes the attempt it is handed and reports the result.</remarks>
public sealed record DelegationChildRunRequest
{
    /// <summary>Initializes a validated run request.</summary>
    /// <param name="delegation">The canonical delegation, including the child's objective, acceptance criteria, scope, budget, deadline, and captured authorization.</param>
    /// <param name="childGoalId">The child goal whose attempt runs.</param>
    /// <param name="sessionId">The provisioned session the child executes in.</param>
    /// <param name="attemptId">The claimed attempt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="delegation"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    public DelegationChildRunRequest(DelegationRequest delegation, GoalId childGoalId, SessionId sessionId, GoalAttemptId attemptId)
    {
        ArgumentNullException.ThrowIfNull(delegation);
        ArgumentOutOfRangeException.ThrowIfEqual(childGoalId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(attemptId, default);
        Delegation = delegation;
        ChildGoalId = childGoalId;
        SessionId = sessionId;
        AttemptId = attemptId;
    }

    /// <summary>Gets the canonical delegation.</summary>
    public DelegationRequest Delegation { get; }

    /// <summary>Gets the child goal whose attempt runs.</summary>
    public GoalId ChildGoalId { get; }

    /// <summary>Gets the provisioned session the child executes in.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the claimed attempt.</summary>
    public GoalAttemptId AttemptId { get; }
}
