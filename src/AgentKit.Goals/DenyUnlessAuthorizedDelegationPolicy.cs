// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Is the baseline delegation policy every profile evaluates first: it denies unless the delegation narrows what its parent holds.</summary>
/// <remarks>
/// A delegation is allowed only when it fits inside the parent's captured authority and the profile's limits: the deadline
/// lies ahead, the delegation depth and child count stay within the profile's ceilings, and the child's turn, tool-call,
/// and child budget can only be smaller than the parent's. The policy reads immutable context and never consumes a grant; the
/// security authority still authorizes the delegation afterwards, so this policy can only narrow, never authorize.
/// </remarks>
internal sealed class DenyUnlessAuthorizedDelegationPolicy: IDelegationPolicy
{
    /// <summary>Gets the stable identity under which the baseline policy is registered and always selected.</summary>
    internal static ComponentId PolicyId { get; } = new("agentkit.goals.deny-unless-authorized");

    /// <inheritdoc/>
    public ValueTask<DelegationPolicyDecision> EvaluateAsync(
        DelegationRequest request,
        DelegationPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var rejection = Evaluate(request, context);
        return ValueTask.FromResult<DelegationPolicyDecision>(
            rejection is null ? new DelegationPolicyAllowed() : new DelegationPolicyDenied(rejection));
    }

    private static DelegationRejection? Evaluate(DelegationRequest request, DelegationPolicyContext context)
    {
        var parent = context.Parent.Goal;
        var requested = request.Budget.Budget;
        return request.Deadline <= context.Now
            ? new DelegationRejection(DelegationRejectionKind.DeadlineElapsed, "The delegation deadline had already passed.")
            : context.ParentDepth + 1 > context.Profile.MaximumDelegationDepth
                ? new DelegationRejection(DelegationRejectionKind.LimitExceeded, "The delegation depth ceiling would be exceeded.")
                : context.ExistingChildren >= context.Profile.MaximumChildrenPerGoal
                    ? new DelegationRejection(DelegationRejectionKind.LimitExceeded, "The parent goal has reached its child ceiling.")
                    : requested.MaximumTurns > parent.Budget.MaximumTurns
                        || requested.MaximumToolCalls > parent.Budget.MaximumToolCalls
                        || requested.MaximumChildren >= parent.Budget.MaximumChildren
                        ? new DelegationRejection(DelegationRejectionKind.Unauthorized, "The requested child budget is not narrower than the parent's.")
                        : null;
    }
}
