// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no safe complete promotion plan could be formed from the captured input context.</summary>
/// <remarks>This is a planning outcome, not an admission or promotion mutation. The safe reason must not disclose queued input content.</remarks>
public sealed record InputPromotionPlanRejected: InputPromotionPlanningResult
{
    /// <summary>Initializes a complete-plan rejection.</summary>
    /// <param name="kind">The defined rejection class explaining why planning could not form a safe complete selection.</param>
    /// <param name="requiredCount">The non-negative number of admissions a complete selection would require.</param>
    /// <param name="maximumPromotions">The positive configured upper bound for this plan.</param>
    /// <param name="safeReason">A non-null, non-whitespace content-free explanation safe for callers and diagnostics.</param>
    /// <exception cref="ArgumentOutOfRangeException">The kind is undefined, a count is invalid, or selection-limit rejection does not exceed the bound.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public InputPromotionPlanRejected(InputPromotionPlanRejectionKind kind, int requiredCount, int maximumPromotions, string safeReason)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind); ArgumentOutOfRangeException.ThrowIfNegative(requiredCount); ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPromotions);
        if (kind == InputPromotionPlanRejectionKind.SelectionLimitExceeded)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requiredCount, maximumPromotions);
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason); Kind = kind; RequiredCount = requiredCount;
        MaximumPromotions = maximumPromotions; SafeReason = safeReason;
    }
    /// <summary>Gets the defined complete-plan rejection class.</summary>
    /// <value>A stable kind describing the planning constraint without exposing queued input.</value>
    public InputPromotionPlanRejectionKind Kind { get; }
    /// <summary>Gets the count a complete selection would require.</summary>
    /// <value>A non-negative count; for a selection-limit rejection it exceeds <see cref="MaximumPromotions"/>.</value>
    public int RequiredCount { get; }
    /// <summary>Gets the configured upper bound for selected admissions.</summary>
    /// <value>A positive count used to keep planning and promotion bounded.</value>
    public int MaximumPromotions { get; }
    /// <summary>Gets the content-free explanation of the planning rejection.</summary>
    /// <value>A non-empty safe string that excludes user, model, and queued payload content.</value>
    public string SafeReason { get; }
}
