// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports stale operation, cursor, fence, or eligibility evidence without committing any input promotion.</summary>
/// <remarks>The caller must obtain a fresh context before planning again. This outcome does not identify or consume pending input and cannot be treated as a successful empty promotion.</remarks>
public sealed record InputPromotionConflict: InputPromotionResult
{
    /// <summary>Initializes a stale-evidence promotion outcome.</summary>
    /// <param name="kind">The defined class of evidence that prevented the atomic promotion transition.</param>
    /// <param name="safeReason">A non-null, non-whitespace content-free explanation safe for callers and diagnostics.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public InputPromotionConflict(InputPromotionConflictKind kind, string safeReason)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind); ArgumentException.ThrowIfNullOrWhiteSpace(safeReason); Kind = kind; SafeReason = safeReason;
    }
    /// <summary>Gets the stable class of stale evidence.</summary>
    /// <value>A defined kind identifying the revalidation dimension that failed without exposing content.</value>
    public InputPromotionConflictKind Kind { get; }
    /// <summary>Gets the content-free explanation for the stale-evidence outcome.</summary>
    /// <value>A non-empty safe string that does not include input payload or other protected content.</value>
    public string SafeReason { get; }
}
