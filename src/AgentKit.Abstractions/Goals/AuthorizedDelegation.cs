// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries one already-authorized, already-created child to the dispatcher that performs the handoff.</summary>
/// <remarks>The dispatcher validates and consumes <see cref="Grant"/> itself immediately before it acts; subsequent child admission, state access, and communication obtain their own scoped grants. The request's budget already carries the reserved scope.</remarks>
public sealed record AuthorizedDelegation
{
    /// <summary>Initializes a validated authorized delegation.</summary>
    /// <param name="request">The canonical request, with its reserved budget and narrowed scope.</param>
    /// <param name="target">The selected target.</param>
    /// <param name="child">The durably created child goal, in <see cref="GoalStatus.Proposed"/>.</param>
    /// <param name="grant">The single-use delegation grant.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException">The child does not belong to the request or the target does not match it, or the grant lacks captured authorization.</exception>
    public AuthorizedDelegation(DelegationRequest request, DelegationTarget target, GoalRecord child, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        ArgumentException.ThrowIfNotEqual(target.AgentId, request.TargetAgentId, nameof(target));
        ArgumentException.ThrowIfNotEqual(child.Goal.ParentId, request.ParentGoalId, nameof(child));
        Request = request;
        Target = target;
        Child = child;
        Grant = grant;
    }

    /// <summary>Gets the canonical request.</summary>
    public DelegationRequest Request { get; }

    /// <summary>Gets the selected target.</summary>
    public DelegationTarget Target { get; }

    /// <summary>Gets the durably created child goal.</summary>
    public GoalRecord Child { get; }

    /// <summary>Gets the single-use delegation grant.</summary>
    public SecurityGrant Grant { get; }
}
