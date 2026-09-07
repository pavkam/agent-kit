// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that policy denied the request without issuing authority.</summary>
public sealed record SecurityDenied: SecurityDecision
{
    /// <summary>Initializes a denied decision.</summary>
    /// <param name="requestId">The evaluated request.</param>
    /// <param name="policyVersion">The effective policy version.</param>
    /// <param name="denial">The stable non-sensitive denial.</param>
    /// <exception cref="ArgumentNullException"><paramref name="denial"/> is null.</exception>
    public SecurityDenied(SecurityRequestId requestId, SecurityPolicyVersion policyVersion, SecurityDenial denial)
        : base(requestId, policyVersion)
    {
        ArgumentNullException.ThrowIfNull(denial);
        Denial = denial;
    }

    /// <summary>Gets the denial evidence.</summary>
    public SecurityDenial Denial { get; init; }
}
