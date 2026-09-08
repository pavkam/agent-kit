// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Maps authority-selection outcomes to stable bounded diagnostic values.</summary>
internal static class SecurityAuthoritySelectionOutcomeExtensions
{
    extension(SecurityAuthoritySelectionOutcome outcome)
    {
        /// <summary>Gets the stable diagnostic value for one defined outcome.</summary>
        /// <returns>A lower-case bounded value suitable for metrics and activity status.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
        public string ToStableValue() => outcome switch
        {
            SecurityAuthoritySelectionOutcome.Selected => "selected",
            SecurityAuthoritySelectionOutcome.Unavailable => "unavailable",
            SecurityAuthoritySelectionOutcome.Cancelled => "cancelled",
            SecurityAuthoritySelectionOutcome.Failed => "failed",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The security authority selection outcome is undefined."),
        };
    }
}
