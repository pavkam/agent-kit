// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that policy allows a proposal to become durable state.</summary>
public sealed record MemoryPolicyAllowed: MemoryPolicyDecision
{
    /// <summary>Initializes an allowed decision.</summary>
    /// <param name="policyId">The non-blank identity of the policy that allowed the proposal.</param>
    /// <exception cref="ArgumentException"><paramref name="policyId"/> is default or blank.</exception>
    public MemoryPolicyAllowed(ComponentId policyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId.Value, nameof(policyId));
        PolicyId = policyId;
    }

    /// <summary>Gets the identity of the policy that allowed the proposal.</summary>
    public ComponentId PolicyId { get; }
}
