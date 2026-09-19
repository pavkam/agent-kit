// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that one mid-run input-promotion request did not commit.</summary>
/// <remarks>This covers stale lane revision, session version, or branch cursor; a lane without a matching accepted run; an operation-state revision mismatch; and any selected admission no longer pending. No admission is consumed and no history entry is appended.</remarks>
public sealed record SessionInputPromotionRejected: SessionInputPromotionResult
{
    /// <summary>Initializes a pre-mutation promotion rejection.</summary>
    /// <param name="safeReason">The non-null, non-whitespace content-free explanation safe for callers and diagnostics.</param>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public SessionInputPromotionRejected(string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        SafeReason = safeReason;
    }

    /// <summary>Gets the content-free explanation for the rejection.</summary>
    /// <value>A non-empty safe string that does not include admission payload or other protected content.</value>
    public string SafeReason { get; }
}
