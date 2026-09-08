// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that required audit delivery exceeded its configured deadline and durable acceptance is unknown.</summary>
/// <remarks>
/// A timeout does not prove the sink rejected, discarded, or failed to persist the immutable record. Callers fail
/// closed for the protected transition and retain the existing record identity for reconciliation; they do not infer
/// that repeating a protected effect or minting another audit record is safe.
/// </remarks>
public sealed record SecurityAuditTimedOut: SecurityAuditDispatchResult
{
    /// <summary>Initializes a safe required-audit timeout result.</summary>
    /// <param name="safeReason">A non-empty explanation that contains no protected content, sink payload, or implementation secret.</param>
    /// <exception cref="ArgumentNullException"><paramref name="safeReason"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SecurityAuditTimedOut(string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        SafeReason = safeReason;
    }

    /// <summary>Gets the safe explanation callers surface while treating durable acceptance as unknown.</summary>
    public string SafeReason { get; }
}
