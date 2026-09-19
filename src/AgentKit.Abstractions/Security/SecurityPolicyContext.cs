// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries the immutable evidence a policy needs to evaluate one request without granting it any authority.</summary>
/// <remarks>This context is evidence only. It never issues a grant, and a policy must still return an explicit result through <see cref="ISecurityPolicy.EvaluateAsync"/>.</remarks>
public sealed record SecurityPolicyContext
{
    /// <summary>Initializes a policy evaluation context.</summary>
    /// <param name="authorization">The captured authorization evidence for the operation being evaluated.</param>
    /// <param name="revocationVersion">The live revocation epoch observed at evaluation time.</param>
    /// <param name="evaluatedAt">The instant evaluation began.</param>
    /// <exception cref="ArgumentNullException"><paramref name="authorization"/> is null.</exception>
    public SecurityPolicyContext(SecurityAuthorizationContext authorization, SecurityRevocationVersion revocationVersion, DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        Authorization = authorization;
        RevocationVersion = revocationVersion;
        EvaluatedAt = evaluatedAt;
    }

    /// <summary>Gets the captured authorization evidence.</summary>
    public SecurityAuthorizationContext Authorization { get; }
    /// <summary>Gets the live revocation epoch observed at evaluation time.</summary>
    public SecurityRevocationVersion RevocationVersion { get; }
    /// <summary>Gets the instant evaluation began.</summary>
    public DateTimeOffset EvaluatedAt { get; }
}
