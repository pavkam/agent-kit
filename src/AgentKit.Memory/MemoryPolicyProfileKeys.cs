// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Names the memory policy profiles the package defines.</summary>
public static class MemoryPolicyProfileKeys
{
    /// <summary>Gets the policy profile whose only policy denies every proposal.</summary>
    /// <value>The key <c>agentkit.fail-closed</c>. A memory profile selects it by default, so no proposal becomes durable state until the host selects a policy profile whose policies explicitly allow retention.</value>
    public static MemoryPolicyProfileKey FailClosed { get; } = new("agentkit.fail-closed");
}
