// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Evaluates exactly the policies a captured profile selects, in deterministic order, and combines their decisions.</summary>
public interface IDelegationPolicyPipeline
{
    /// <summary>Evaluates the profile's policies in order; the first denial stops evaluation.</summary>
    /// <param name="request">The delegation request.</param>
    /// <param name="context">The immutable evaluation context, which names the captured profile.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>A denial, or an allow decision carrying the cumulative narrowing.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<DelegationPolicyDecision> EvaluateAsync(
        DelegationRequest request,
        DelegationPolicyContext context,
        CancellationToken cancellationToken = default);
}
