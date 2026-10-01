// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The abstract decision of one delegation policy or of the whole pipeline.</summary>
public abstract record DelegationPolicyDecision
{
    /// <summary>Restricts derivation to the contracts package.</summary>
    private protected DelegationPolicyDecision()
    {
    }
}
