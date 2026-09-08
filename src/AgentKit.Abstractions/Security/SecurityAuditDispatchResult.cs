// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents the closed terminal result of delivering a security audit intent.</summary>
public abstract record SecurityAuditDispatchResult
{
    /// <summary>Prevents external result kinds from bypassing required-audit handling.</summary>
    private protected SecurityAuditDispatchResult()
    {
    }
}
