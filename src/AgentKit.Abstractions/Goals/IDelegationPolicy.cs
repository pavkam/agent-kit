// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Evaluates one additive rule over a delegation request and its durable context.</summary>
/// <remarks>Policies intersect parent authority, resource and data scope, and budget. They are pure with respect to durable state, thread-safe, and never consume grants or perform the delegation.</remarks>
public interface IDelegationPolicy
{
    /// <summary>Evaluates one delegation.</summary>
    /// <param name="request">The delegation request.</param>
    /// <param name="context">The immutable evaluation context.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>An allow decision, possibly narrowing, or a denial.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<DelegationPolicyDecision> EvaluateAsync(
        DelegationRequest request,
        DelegationPolicyContext context,
        CancellationToken cancellationToken = default);
}
