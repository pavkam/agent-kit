// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that required audit delivery failed before it was known to be durably accepted.</summary>
public sealed record SecurityAuditFailed: SecurityAuditDispatchResult
{
    /// <summary>Initializes a safe required-audit delivery failure.</summary>
    /// <param name="safeReason">A non-empty explanation that contains no protected content or implementation secret.</param>
    /// <exception cref="ArgumentNullException"><paramref name="safeReason"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SecurityAuditFailed(string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        SafeReason = safeReason;
    }

    /// <summary>Gets the safe reason callers must surface as a fail-closed rejection.</summary>
    public string SafeReason { get; }
}
