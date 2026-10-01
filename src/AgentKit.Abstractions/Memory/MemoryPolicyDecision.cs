// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is the abstract outcome of evaluating one memory proposal against policy.</summary>
/// <remarks>Policy decides whether a proposal may become durable state; it never writes. A denial is final for that proposal.</remarks>
public abstract record MemoryPolicyDecision
{
    /// <summary>Restricts derivation to the contracts package.</summary>
    private protected MemoryPolicyDecision()
    {
    }
}
