// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Egress;

/// <summary>The terminal outcome of one provider-egress attempt: a response handle or a typed refusal.</summary>
/// <remarks>
/// Provider egress reports every expected outcome, including denial, unavailable enforcement, and transport failure,
/// as a value. Only caller-argument faults throw.
/// </remarks>
public abstract record ProviderEgressResult
{
    private protected ProviderEgressResult()
    {
    }
}
