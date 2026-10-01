// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one committed child-admission intent a host worker should drain.</summary>
/// <remarks>The intent is a wake-up hint carrying the delegation and the child aggregate as they were when the handoff committed. Durable state remains the truth: a worker re-reads the child and claims it with an atomic transition, so a duplicated, late, or lost hint never creates a second attempt or a second child.</remarks>
public sealed record DelegationIntent
{
    /// <summary>Initializes a validated intent.</summary>
    /// <param name="request">The canonical delegation, including its captured authorization.</param>
    /// <param name="child">The child goal as committed for handoff, in <see cref="GoalStatus.Ready"/>.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException">The child does not belong to the delegation.</exception>
    public DelegationIntent(DelegationRequest request, GoalRecord child)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(child);
        ArgumentException.ThrowIfNotEqual(child.Goal.ParentId, request.ParentGoalId, nameof(child));
        Request = request;
        Child = child;
    }

    /// <summary>Gets the canonical delegation.</summary>
    public DelegationRequest Request { get; }

    /// <summary>Gets the child goal as committed for handoff.</summary>
    public GoalRecord Child { get; }
}
