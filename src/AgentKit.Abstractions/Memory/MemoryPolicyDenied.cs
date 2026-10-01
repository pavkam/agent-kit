// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that policy refuses a proposal, with a stable code and a content-safe message.</summary>
public sealed record MemoryPolicyDenied: MemoryPolicyDecision
{
    /// <summary>Initializes a denied decision.</summary>
    /// <param name="policyId">The non-blank identity of the policy that denied the proposal.</param>
    /// <param name="code">The non-blank stable machine-readable denial code.</param>
    /// <param name="safeMessage">The non-blank content-safe explanation.</param>
    /// <exception cref="ArgumentException">An argument is blank.</exception>
    public MemoryPolicyDenied(ComponentId policyId, string code, string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId.Value, nameof(policyId));
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        PolicyId = policyId;
        Code = code;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the identity of the policy that denied the proposal.</summary>
    public ComponentId PolicyId { get; }

    /// <summary>Gets the stable denial code.</summary>
    public string Code { get; }

    /// <summary>Gets the content-safe explanation.</summary>
    public string SafeMessage { get; }
}
