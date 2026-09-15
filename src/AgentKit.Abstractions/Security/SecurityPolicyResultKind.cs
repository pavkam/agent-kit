// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies one additive security policy contribution.</summary>
public enum SecurityPolicyResultKind
{
    /// <summary>The policy does not govern the request.</summary>
    Abstain,
    /// <summary>The policy proposes bounded allowance.</summary>
    Allow,
    /// <summary>The policy denies the request and prevents grant issue.</summary>
    Deny,
    /// <summary>The policy permits the request only after an exact authorized human approval.</summary>
    RequireApproval,
}
