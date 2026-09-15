// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Provides explicit stable keys for first-party loop components.</summary>
public static class AgentLoopDefaults
{
    /// <summary>
    /// The string value of <see cref="ContinuationPolicyKey"/>, exposed as a compile-time constant so keyed
    /// constructor injection (<c>[FromKeyedServices]</c>) can name the canonical policy registration.
    /// </summary>
    public const string ContinuationPolicyKeyValue = "agentkit-default-continuation";

    /// <summary>Gets the key of the canonical stateless continuation policy.</summary>
    public static ComponentKey<IRunContinuationPolicy> ContinuationPolicyKey { get; } =
        new(ContinuationPolicyKeyValue);
}
