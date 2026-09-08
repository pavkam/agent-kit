// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no safe complete plan could be formed from captured input.</summary>
public sealed record InputPromotionPlanRejected: InputPromotionPlanningResult
{
    /// <summary>Initializes plan rejection.</summary>
    /// <param name="kind">The stable rejection class.</param><param name="requiredCount">The nonnegative required selection count.</param>
    /// <param name="maximumPromotions">The positive configured bound.</param><param name="safeReason">Content-free explanation.</param>
    public InputPromotionPlanRejected(InputPromotionPlanRejectionKind kind, int requiredCount, int maximumPromotions, string safeReason)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requiredCount); ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPromotions);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason); Kind = kind; RequiredCount = requiredCount;
        MaximumPromotions = maximumPromotions; SafeReason = safeReason;
    }
    /// <summary>Gets rejection class.</summary><value>The stable kind.</value>
    public InputPromotionPlanRejectionKind Kind { get; }
    /// <summary>Gets required selection count.</summary><value>A nonnegative count.</value>
    public int RequiredCount { get; }
    /// <summary>Gets configured plan bound.</summary><value>A positive count.</value>
    public int MaximumPromotions { get; }
    /// <summary>Gets safe explanation.</summary><value>Content-free text.</value>
    public string SafeReason { get; }
}
