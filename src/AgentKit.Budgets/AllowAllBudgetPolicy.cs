// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>A permissive default policy that allows every evaluated request.</summary>
internal sealed class AllowAllBudgetPolicy: IBudgetPolicy
{
    /// <inheritdoc/>
    public ValueTask<BudgetPolicyDecision> EvaluateAsync(BudgetPolicyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<BudgetPolicyDecision>(new BudgetPolicyAllowed());
    }
}
