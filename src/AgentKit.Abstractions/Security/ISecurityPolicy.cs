// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Contributes an additive, ordered policy result without issuing grants or performing effects.</summary>
public interface ISecurityPolicy
{
    /// <summary>Evaluates one normalized security request.</summary>
    /// <param name="request">The complete normalized request.</param>
    /// <param name="cancellationToken">Cancels policy evaluation.</param>
    /// <returns>An allow, deny, or abstain proposal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<SecurityPolicyResult> EvaluateAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default);
}
