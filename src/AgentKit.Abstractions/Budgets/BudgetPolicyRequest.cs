// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Immutable evidence presented to one budget policy evaluation.</summary>
public sealed record BudgetPolicyRequest
{
    /// <summary>Initializes a policy evaluation request.</summary>
    /// <param name="profile">The resolved profile whose policy list includes the evaluator.</param>
    /// <param name="scopeRequest">The scope creation or child-scope request under evaluation.</param>
    /// <param name="proposedReservation">The reservation under evaluation, when the policy runs at reservation time.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> or <paramref name="scopeRequest"/> is null.</exception>
    public BudgetPolicyRequest(
        BudgetProfileSnapshot profile,
        BudgetScopeRequest scopeRequest,
        BudgetReservationRequest? proposedReservation = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(scopeRequest);
        Profile = profile;
        ScopeRequest = scopeRequest;
        ProposedReservation = proposedReservation;
    }

    /// <summary>Gets the resolved profile whose ordered policies include the evaluator.</summary>
    public BudgetProfileSnapshot Profile { get; }

    /// <summary>Gets the scope request under evaluation.</summary>
    public BudgetScopeRequest ScopeRequest { get; }

    /// <summary>Gets the reservation under evaluation, when present.</summary>
    public BudgetReservationRequest? ProposedReservation { get; }
}
