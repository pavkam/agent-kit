// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Denies every proposal, so nothing becomes durable state until a host selects a policy profile whose policies explicitly allow retention.</summary>
/// <remarks>The policy is stateless and thread-safe. It is the only policy of <see cref="MemoryPolicyProfileKeys.FailClosed"/>.</remarks>
internal sealed class FailClosedMemoryPolicy: IMemoryPolicy
{
    /// <summary>The identity the policy's denials carry.</summary>
    internal static ComponentId Id { get; } = new("agentkit.fail-closed");

    /// <inheritdoc/>
    public ValueTask<MemoryPolicyDecision> EvaluateAsync(MemoryProposal proposal, MemoryPolicyContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult<MemoryPolicyDecision>(new MemoryPolicyDenied(Id, "fail-closed", "No memory policy explicitly allows retention."));
    }
}
