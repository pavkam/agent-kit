// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Provides explicit stable keys for first-party loop components.</summary>
public static class AgentLoopDefaults
{
    /// <summary>Gets the key of the canonical stateless continuation policy.</summary>
    public static ComponentKey<IRunContinuationPolicy> ContinuationPolicyKey { get; } =
        new("agentkit-default-continuation");
}
