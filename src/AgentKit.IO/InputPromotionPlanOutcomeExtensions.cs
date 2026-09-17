// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Maps finite input-promotion planning outcomes to stable diagnostic values.</summary>
internal static class InputPromotionPlanOutcomeExtensions
{
    extension(InputPromotionPlanOutcome outcome)
    {
        /// <summary>Returns the bounded lowercase value shared by promotion logs, activities, and metrics.</summary>
        /// <returns>A stable lowercase terminal-outcome value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
        internal string ToStableValue() => outcome switch
        {
            InputPromotionPlanOutcome.Planned => "planned",
            InputPromotionPlanOutcome.Rejected => "rejected",
            InputPromotionPlanOutcome.Cancelled => "cancelled",
            InputPromotionPlanOutcome.Failed => "failed",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The input promotion plan outcome is undefined."),
        };
    }
}
