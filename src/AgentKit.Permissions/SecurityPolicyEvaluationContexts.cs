// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Builds <see cref="SecurityPolicyContext"/> values for authority evaluation.</summary>
internal static class SecurityPolicyEvaluationContexts
{
    /// <summary>Creates evaluation evidence for one request from the authorization context the request captured.</summary>
    /// <param name="request">The request being evaluated.</param>
    /// <param name="revocationVersion">The authority's live revocation epoch.</param>
    /// <param name="evaluatedAt">The instant evaluation began.</param>
    /// <returns>A context suitable for <see cref="ISecurityPolicy.EvaluateAsync"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    internal static SecurityPolicyContext Create(
        SecurityRequest request,
        SecurityRevocationVersion revocationVersion,
        DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new SecurityPolicyContext(request.Authorization, revocationVersion, evaluatedAt);
    }
}
