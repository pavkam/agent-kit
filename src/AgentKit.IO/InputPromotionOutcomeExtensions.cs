// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Maps finite input-promotion outcomes to stable diagnostic values.</summary>
internal static class InputPromotionOutcomeExtensions
{
    extension(InputPromotionOutcome outcome)
    {
        /// <summary>Returns the bounded lowercase value shared by promotion logs, activities, and metrics.</summary>
        /// <returns>A stable lowercase terminal-outcome value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
        internal string ToStableValue()
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
            return outcome switch
            {
                InputPromotionOutcome.Promoted => "promoted",
                InputPromotionOutcome.Conflict => "conflict",
                InputPromotionOutcome.Rejected => "rejected",
                InputPromotionOutcome.Cancelled => "cancelled",
                InputPromotionOutcome.Failed => "failed",
                _ => throw new UnreachableException(),
            };
        }
    }
}
