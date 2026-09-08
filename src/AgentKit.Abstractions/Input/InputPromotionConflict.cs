// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports stale operation, cursor, fence, or eligibility evidence without committing input.</summary>
public sealed record InputPromotionConflict: InputPromotionResult
{
    /// <summary>Initializes a promotion conflict.</summary>
    public InputPromotionConflict(InputPromotionConflictKind kind, string safeReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason); Kind = kind; SafeReason = safeReason;
    }
    /// <summary>Gets conflict class.</summary><value>The stable kind.</value>
    public InputPromotionConflictKind Kind { get; }
    /// <summary>Gets safe explanation.</summary><value>Content-free text.</value>
    public string SafeReason { get; }
}
