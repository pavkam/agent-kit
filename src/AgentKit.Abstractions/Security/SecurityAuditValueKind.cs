// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the safe, non-content representation retained for an audit field.</summary>
public enum SecurityAuditValueKind
{
    /// <summary>The value is a one-way fingerprint of redacted content or structure.</summary>
    Fingerprint,

    /// <summary>The value is a declared security-policy identifier.</summary>
    SecurityPolicyId,

    /// <summary>The value is a declared effecting-component identifier.</summary>
    ComponentId,

    /// <summary>The value is a defined security operation kind.</summary>
    OperationKind,

    /// <summary>The value is a defined security effect.</summary>
    Effect,
}
