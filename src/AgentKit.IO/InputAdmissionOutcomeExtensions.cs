// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Maps finite input-admission outcomes to stable diagnostic values.</summary>
internal static class InputAdmissionOutcomeExtensions
{
    extension(InputAdmissionOutcome outcome)
    {
        /// <summary>Returns the bounded lowercase value shared by admission logs, activities, and metrics.</summary>
        /// <returns>A stable lowercase terminal-outcome value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
        internal string ToStableValue() => outcome switch
        {
            InputAdmissionOutcome.Accepted => "accepted",
            InputAdmissionOutcome.Conflict => "conflict",
            InputAdmissionOutcome.CapacityExceeded => "capacity_exceeded",
            InputAdmissionOutcome.Rejected => "rejected",
            InputAdmissionOutcome.Cancelled => "cancelled",
            InputAdmissionOutcome.Failed => "failed",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The input admission outcome is undefined."),
        };
    }
}
