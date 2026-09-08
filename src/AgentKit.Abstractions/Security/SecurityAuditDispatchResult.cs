// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents a typed terminal result of delivering a security audit intent.</summary>
public abstract record SecurityAuditDispatchResult
{
    /// <summary>Initializes base state for a derived security-audit delivery result type.</summary>
    private protected SecurityAuditDispatchResult()
    {
    }
}
