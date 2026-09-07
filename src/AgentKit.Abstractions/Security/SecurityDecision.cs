// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents the closed terminal allow-or-deny result of immediate security evaluation.</summary>
public abstract record SecurityDecision
{
    private protected SecurityDecision(SecurityRequestId requestId, SecurityPolicyVersion policyVersion)
    {
        RequestId = requestId;
        PolicyVersion = policyVersion;
    }

    /// <summary>Gets the evaluated request identity.</summary>
    public SecurityRequestId RequestId { get; init; }
    /// <summary>Gets the effective policy version.</summary>
    public SecurityPolicyVersion PolicyVersion { get; init; }
}
