// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that policy issued and registered a bounded grant for the request.</summary>
public sealed record SecurityAllowed: SecurityDecision
{
    /// <summary>Initializes an allowed decision.</summary>
    /// <param name="requestId">The evaluated request.</param>
    /// <param name="policyVersion">The effective policy version.</param>
    /// <param name="grant">The issued bounded grant.</param>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public SecurityAllowed(SecurityRequestId requestId, SecurityPolicyVersion policyVersion, SecurityGrant grant)
        : base(requestId, policyVersion)
    {
        ArgumentNullException.ThrowIfNull(grant);
        Grant = grant;
    }

    /// <summary>Gets the issued bounded grant.</summary>
    public SecurityGrant Grant { get; init; }
}
