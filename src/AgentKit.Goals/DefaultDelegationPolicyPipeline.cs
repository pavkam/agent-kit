// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Evaluates exactly the policies a captured profile selects, in deterministic order, and combines their narrowing.</summary>
/// <remarks>
/// A policy may only narrow. After every allow the pipeline verifies that each narrowed scope and budget is contained in
/// the value it replaces, and turns a widening into a denial, so no policy can broaden a parent's authority. The first
/// denial stops evaluation. Policies are resolved through the container's additive registrations by the identity the profile
/// selects; a selected identity with no registration fails closed.
/// </remarks>
/// <param name="declarations">Every registered policy declaration.</param>
/// <param name="services">The root provider policies are constructed from.</param>
internal sealed class DefaultDelegationPolicyPipeline(IEnumerable<DelegationPolicyDeclaration> declarations, IServiceProvider services): IDelegationPolicyPipeline
{
    private readonly ImmutableArray<DelegationPolicyDeclaration> _declarations = [.. declarations];

    /// <inheritdoc/>
    public async ValueTask<DelegationPolicyDecision> EvaluateAsync(
        DelegationRequest request,
        DelegationPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var (ordered, problem) = DelegationPolicyOrder.Compute(context.Profile.PolicyIds, _declarations);
        if (problem is not null)
        {
            return new DelegationPolicyDenied(new DelegationRejection(DelegationRejectionKind.PolicyDenied, problem));
        }

        var scope = request.Scope;
        var budget = request.Budget.Budget;
        foreach (var declaration in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (services.GetService(declaration.PolicyType) is not IDelegationPolicy policy)
            {
                return new DelegationPolicyDenied(new DelegationRejection(DelegationRejectionKind.PolicyDenied, "A selected delegation policy could not be constructed."));
            }

            var decision = await policy.EvaluateAsync(request, context, cancellationToken).ConfigureAwait(false);
            switch (decision)
            {
                case DelegationPolicyDenied denied:
                    return denied;
                case DelegationPolicyAllowed allowed:
                    if (allowed.Scope is { } narrowedScope)
                    {
                        if (!IsWithin(narrowedScope, scope))
                        {
                            return Widening();
                        }

                        scope = narrowedScope;
                    }

                    if (allowed.Budget is { } narrowedBudget)
                    {
                        if (!IsWithin(narrowedBudget, budget))
                        {
                            return Widening();
                        }

                        budget = narrowedBudget;
                    }

                    break;
                default:
                    return new DelegationPolicyDenied(new DelegationRejection(DelegationRejectionKind.PolicyDenied, "A delegation policy returned an unsupported decision."));
            }
        }

        return new DelegationPolicyAllowed(
            scope == request.Scope ? null : scope,
            budget == request.Budget.Budget ? null : budget);
    }

    private static DelegationPolicyDenied Widening() =>
        new(new DelegationRejection(DelegationRejectionKind.PolicyDenied, "A delegation policy attempted to widen the delegation."));

    private static bool IsWithin(DelegationScope narrowed, DelegationScope current) =>
        narrowed.AllowedTools.All(current.AllowedTools.Contains) && narrowed.DataScopes.All(current.DataScopes.Contains);

    private static bool IsWithin(GoalBudget narrowed, GoalBudget current) =>
        narrowed.MaximumTurns <= current.MaximumTurns
        && narrowed.MaximumToolCalls <= current.MaximumToolCalls
        && narrowed.MaximumChildren <= current.MaximumChildren;
}
