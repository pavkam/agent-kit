// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Evaluates one named budget policy at an admission or reservation boundary.</summary>
public interface IBudgetPolicy
{
    /// <summary>Evaluates the policy for one request.</summary>
    /// <param name="request">The immutable policy request.</param>
    /// <param name="cancellationToken">Cancels the evaluation; cancellation propagates.</param>
    /// <returns>The policy decision.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<BudgetPolicyDecision> EvaluateAsync(
        BudgetPolicyRequest request,
        CancellationToken cancellationToken = default);
}
